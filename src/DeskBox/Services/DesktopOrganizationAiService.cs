using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DeskBox.Models;

namespace DeskBox.Services;

public sealed partial class DesktopOrganizationAiService
{
    public const string CredentialKeyPrefix = "desktop-organization-ai:";
    private const int MaxSampleFilesPerWidget = 8;

    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(150)
    };

    private static string LocalSecretsFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeskBox",
        "ai-local-secrets.json");

    private readonly ICredentialStore _credentialStore;
    private readonly HttpClient _httpClient;
    private readonly bool _useLocalProfileFallback;

    public DesktopOrganizationAiService()
        : this(new PasswordVaultCredentialStore(), SharedHttpClient, useLocalProfileFallback: true)
    {
    }

    internal DesktopOrganizationAiService(
        ICredentialStore credentialStore,
        HttpClient? httpClient = null,
        bool useLocalProfileFallback = false)
    {
        _credentialStore = credentialStore;
        _httpClient = httpClient ?? SharedHttpClient;
        _useLocalProfileFallback = useLocalProfileFallback;
    }

    public static bool TryApplyLocalProfileOverrides(DesktopOrganizationSettingsSlice slice)
    {
        try
        {
            string path = LocalSecretsFilePath;
            if (!File.Exists(path))
            {
                return false;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            string? localBaseUrl = root.TryGetProperty("BaseUrl", out var urlProp) ? urlProp.GetString() : null;
            string? localModel = root.TryGetProperty("Model", out var modelProp) ? modelProp.GetString() : null;
            string? localProvider = root.TryGetProperty("ProviderId", out var provProp) ? provProp.GetString() : null;

            bool changed = false;
            if (!string.IsNullOrWhiteSpace(localBaseUrl) &&
                (string.IsNullOrWhiteSpace(slice.DesktopOrganizationAiBaseUrl) ||
                 string.Equals(slice.DesktopOrganizationAiBaseUrl, "https://api.deepseek.com/v1", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(slice.DesktopOrganizationAiBaseUrl, "https://generativelanguage.googleapis.com/v1beta/openai", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(slice.DesktopOrganizationAiBaseUrl, "https://generativelanguage.googleapis.com/v1beta/openai/", StringComparison.OrdinalIgnoreCase)))
            {
                slice.DesktopOrganizationAiBaseUrl = localBaseUrl.Trim();
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(localModel) &&
                (string.IsNullOrWhiteSpace(slice.DesktopOrganizationAiModel) ||
                 string.Equals(slice.DesktopOrganizationAiModel, "deepseek-chat", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(slice.DesktopOrganizationAiModel, "gemini-2.5-flash", StringComparison.OrdinalIgnoreCase)))
            {
                slice.DesktopOrganizationAiModel = localModel.Trim();
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(localProvider) && changed)
            {
                slice.DesktopOrganizationAiProviderId = localProvider.Trim();
            }

            return changed;
        }
        catch
        {
            return false;
        }
    }

    private static string? TryReadLocalSecretApiKey()
    {
        try
        {
            string path = LocalSecretsFilePath;
            if (!File.Exists(path))
            {
                return null;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("ApiKey", out var keyProp))
            {
                string? key = keyProp.GetString();
                if (!string.IsNullOrWhiteSpace(key))
                {
                    return key.Trim();
                }
            }
        }
        catch
        {
            // Ignore malformed local secret file
        }

        return null;
    }

    public static string GetCredentialKey(string providerId)
    {
        string normalized = string.IsNullOrWhiteSpace(providerId)
            ? DesktopOrganizationAiProviderIds.Gemini
            : providerId.Trim().ToLowerInvariant();
        return $"{CredentialKeyPrefix}{normalized}";
    }

    public async Task<string?> GetApiKeyAsync(
        string providerId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string? stored = await _credentialStore.GetSecretAsync(
                GetCredentialKey(providerId),
                cancellationToken);
            if (!string.IsNullOrWhiteSpace(stored))
            {
                return stored;
            }
        }
        catch (Exception ex)
        {
            App.Log($"[DesktopOrganizationAi] Failed to read API key for {providerId}: {ex.Message}");
        }

        if (_useLocalProfileFallback)
        {
            string? localKey = TryReadLocalSecretApiKey();
            if (!string.IsNullOrWhiteSpace(localKey))
            {
                return localKey;
            }
        }

        return null;
    }

    public async Task SaveApiKeyAsync(
        string providerId,
        string? apiKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string key = GetCredentialKey(providerId);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                await _credentialStore.RemoveSecretAsync(key, cancellationToken);
            }
            else
            {
                await _credentialStore.SetSecretAsync(key, apiKey.Trim(), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            App.Log($"[DesktopOrganizationAi] Failed to save API key for {providerId}: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> TestConnectionAsync(
        DesktopOrganizationAiOptions options,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string endpoint = ResolveChatCompletionsEndpoint(options.BaseUrl);
            string model = string.IsNullOrWhiteSpace(options.Model)
                ? DesktopOrganizationAiProviderPresets.GetById(options.ProviderId).DefaultModel
                : options.Model.Trim();

            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(model))
            {
                return (false, "API Base URL or model name is empty.");
            }

            string? apiKey = options.ApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                apiKey = await GetApiKeyAsync(options.ProviderId, cancellationToken);
            }

            var preset = DesktopOrganizationAiProviderPresets.GetById(options.ProviderId);
            if (preset.RequiresApiKey && string.IsNullOrWhiteSpace(apiKey))
            {
                return (false, "API Key is required for the selected provider.");
            }

            var requestDto = new AiChatCompletionRequest
            {
                Model = model,
                Temperature = 0.0,
                Messages =
                [
                    new AiChatMessage
                    {
                        Role = "user",
                        Content = "Reply with OK only."
                    }
                ]
            };

            string responseContent = await SendChatCompletionAsync(
                endpoint,
                apiKey,
                requestDto,
                cancellationToken);

            return (true, string.IsNullOrWhiteSpace(responseContent) ? "OK" : responseContent.Trim());
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<DesktopOrganizationPlan> GenerateAiPlanAsync(
        DesktopOrganizationScanResult scan,
        string storageRootPath,
        IReadOnlyCollection<WidgetConfig> widgets,
        IReadOnlyCollection<DesktopOrganizationRule> rules,
        DesktopOrganizationAiOptions options,
        Func<string, string>? categoryDisplayNameResolver = null,
        bool includePersonalDesktop = true,
        bool includePublicDesktop = false,
        IProgress<DesktopOrganizationAiStreamProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        categoryDisplayNameResolver ??= id => id;
        string normalizedRoot = Path.GetFullPath(storageRootPath);
        string storageMode = DesktopOrganizationStorageModes.Normalize(options.StorageMode);

        var selectedItems = scan.Items.Select(item =>
            (item.SourceScope == DesktopOrganizationSourceScope.Public ? includePublicDesktop : includePersonalDesktop)
                ? item
                : item with { ExclusionReason = DesktopOrganizationExclusionReason.SourceNotSelected }).ToList();

        var eligibleItems = selectedItems.Where(item => item.IsEligible).ToList();
        var excludedItems = selectedItems.Where(item => !item.IsEligible).ToList();

        if (eligibleItems.Count == 0)
        {
            return new DesktopOrganizationPlan
            {
                DesktopPath = scan.DesktopPath,
                PublicDesktopPath = scan.PublicDesktopPath,
                PublicDesktopUnavailable = scan.PublicDesktopUnavailable,
                IncludePersonalDesktop = includePersonalDesktop,
                IncludePublicDesktop = includePublicDesktop,
                SourceItems = scan.Items.ToList(),
                StorageRootPath = normalizedRoot,
                StorageMode = storageMode,
                Targets = [],
                ExcludedItems = excludedItems,
                IsAiPlan = true
            };
        }

        string? apiKey = options.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            apiKey = await GetApiKeyAsync(options.ProviderId, cancellationToken);
        }

        string endpoint = ResolveChatCompletionsEndpoint(options.BaseUrl);
        string model = string.IsNullOrWhiteSpace(options.Model)
            ? DesktopOrganizationAiProviderPresets.GetById(options.ProviderId).DefaultModel
            : options.Model.Trim();

        var inspectionLogs = new List<string>();
        progress?.Report(new DesktopOrganizationAiStreamProgress
        {
            Stage = "inspecting",
            StageSummary = options.EnableDeepInspection
                ? $"🔬 正在深度探查 {eligibleItems.Count} 个桌面项的快捷方式目标、文档摘要与子文件夹..."
                : $"📚 正在整理 {eligibleItems.Count} 个桌面项元数据（知识库快速模式）...",
            ThinkingText = $"> 💭 思考：正在扫描并准备 {eligibleItems.Count} 个桌面项的上下文...",
            ElapsedSeconds = stopwatch.Elapsed.TotalSeconds
        });

        AiDesktopScanPromptPayload promptPayload = await Task.Run(() => BuildPromptPayload(
            eligibleItems,
            widgets,
            rules,
            options,
            inspectionLogs,
            progress,
            stopwatch), cancellationToken);

        string systemPrompt = BuildSystemPrompt(options);
        string userJson = JsonSerializer.Serialize(
            promptPayload,
            DesktopOrganizationAiJsonContext.Default.AiDesktopScanPromptPayload);

        progress?.Report(new DesktopOrganizationAiStreamProgress
        {
            Stage = "connecting",
            StageSummary = $"🚀 已提取 {inspectionLogs.Count} 条线索，正在连接 {model} 进行深度思考...",
            ThinkingText = ComposeLiveThinkingText(inspectionLogs, string.Empty, string.Empty, model, stopwatch.Elapsed.TotalSeconds),
            InspectionLogs = inspectionLogs.ToList(),
            ElapsedSeconds = stopwatch.Elapsed.TotalSeconds
        });

        var chatRequest = new AiChatCompletionRequest
        {
            Model = model,
            Temperature = 0.2,
            Stream = true,
            Messages =
            [
                new AiChatMessage { Role = "system", Content = systemPrompt },
                new AiChatMessage { Role = "user", Content = userJson }
            ]
        };

        (string rawReply, string thinkingTrace) = await SendChatCompletionStreamingAsync(
            endpoint,
            apiKey,
            chatRequest,
            inspectionLogs,
            progress,
            stopwatch,
            cancellationToken);

        AiOrganizationSchemaResponse aiResponse = ParseAiResponseJson(rawReply);

        return BuildPlanFromAiResponse(
            scan,
            normalizedRoot,
            eligibleItems,
            excludedItems,
            widgets,
            options,
            aiResponse,
            categoryDisplayNameResolver,
            includePersonalDesktop,
            includePublicDesktop,
            thinkingTrace,
            userJson,
            rawReply);
    }

    internal static DesktopOrganizationPlan BuildPlanFromAiResponse(
        DesktopOrganizationScanResult scan,
        string normalizedRoot,
        IReadOnlyList<DesktopOrganizationFileSnapshot> eligibleItems,
        IReadOnlyList<DesktopOrganizationFileSnapshot> excludedItems,
        IReadOnlyCollection<WidgetConfig> widgets,
        DesktopOrganizationAiOptions options,
        AiOrganizationSchemaResponse aiResponse,
        Func<string, string> categoryDisplayNameResolver,
        bool includePersonalDesktop = true,
        bool includePublicDesktop = false,
        string? thinkingTrace = null,
        string? rawPromptJson = null,
        string? rawResponseJson = null)
    {
        string storageMode = DesktopOrganizationStorageModes.Normalize(options.StorageMode);
        bool isInPlaceDesktop = string.Equals(storageMode, DesktopOrganizationStorageModes.InPlaceDesktop, StringComparison.OrdinalIgnoreCase);
        bool isDesktopSubfolders = string.Equals(storageMode, DesktopOrganizationStorageModes.DesktopSubfolders, StringComparison.OrdinalIgnoreCase);
        string effectiveRoot = isInPlaceDesktop || isDesktopSubfolders
            ? Path.GetFullPath(scan.DesktopPath)
            : normalizedRoot;

        var activeFileWidgets = widgets
            .Where(widget =>
                widget.WidgetKind == WidgetKind.File &&
                !widget.IsDisabled &&
                !string.IsNullOrWhiteSpace(widget.MappedFolderPath))
            .ToList();

        var widgetsById = activeFileWidgets
            .ToDictionary(widget => widget.Id, StringComparer.Ordinal);

        var reservedDirectories = activeFileWidgets
            .Select(widget => Path.GetFullPath(widget.MappedFolderPath!))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var assignedIndices = new HashSet<int>();
        var targets = new List<DesktopOrganizationTargetPlan>();
        var bucketNameToTargetMap = new Dictionary<string, DesktopOrganizationTargetPlan>(StringComparer.OrdinalIgnoreCase);

        // Build exact index -> fileName map from the prompt snapshot so saved drafts NEVER shift indices on restart
        Dictionary<int, string> promptIndexToNameMap = ExtractPromptIndexToNameMap(rawPromptJson);
        var eligibleIndexByName = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < eligibleItems.Count; i++)
        {
            string itemName = eligibleItems[i].Name;
            if (!eligibleIndexByName.TryGetValue(itemName, out var list))
            {
                list = [];
                eligibleIndexByName[itemName] = list;
            }
            list.Add(i);
        }

        int TryClaimByName(string expectedName)
        {
            if (string.IsNullOrWhiteSpace(expectedName))
            {
                return -1;
            }

            if (eligibleIndexByName.TryGetValue(expectedName.Trim(), out var candidateIndices))
            {
                foreach (int candIdx in candidateIndices)
                {
                    if (assignedIndices.Add(candIdx))
                    {
                        return candIdx;
                    }
                }
            }

            return -1;
        }

        int bucketOrder = 0;
        foreach (AiTargetBucketDto bucket in aiResponse.Buckets)
        {
            var bucketItems = new List<DesktopOrganizationFileSnapshot>();

            // 1. First match by explicit persisted ItemNames if present
            if (bucket.ItemNames is { Count: > 0 })
            {
                foreach (string savedName in bucket.ItemNames)
                {
                    int matchedIdx = TryClaimByName(savedName);
                    if (matchedIdx >= 0)
                    {
                        bucketItems.Add(eligibleItems[matchedIdx]);
                    }
                }
            }

            // 2. Next match ItemIndices via promptIndexToNameMap (exact name from when the AI generated this plan)
            foreach (int index in bucket.ItemIndices.Distinct())
            {
                if (promptIndexToNameMap.TryGetValue(index, out string? expectedFileName) &&
                    !string.IsNullOrWhiteSpace(expectedFileName))
                {
                    int matchedIdx = TryClaimByName(expectedFileName);
                    if (matchedIdx >= 0)
                    {
                        bucketItems.Add(eligibleItems[matchedIdx]);
                    }
                }
                else if (promptIndexToNameMap.Count == 0 &&
                         index >= 0 && index < eligibleItems.Count &&
                         assignedIndices.Add(index))
                {
                    bucketItems.Add(eligibleItems[index]);
                }
            }

            // Persist resolved exact filenames back onto the bucket DTO for future draft saves
            bucket.ItemNames = bucketItems
                .Select(item => item.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (bucketItems.Count == 0)
            {
                continue;
            }

            WidgetConfig? matchedExistingWidget = null;
            if (options.EnableSmartWidgetReuse)
            {
                if (!string.IsNullOrWhiteSpace(bucket.ExistingWidgetId) &&
                    widgetsById.TryGetValue(bucket.ExistingWidgetId.Trim(), out var byId))
                {
                    matchedExistingWidget = byId;
                }
                else if (!string.IsNullOrWhiteSpace(bucket.BucketName))
                {
                    matchedExistingWidget = activeFileWidgets.FirstOrDefault(w =>
                        string.Equals(w.Name.Trim(), bucket.BucketName.Trim(), StringComparison.OrdinalIgnoreCase));
                }
            }

            string dominantCategory = bucketItems
                .GroupBy(item => item.CategoryId, StringComparer.Ordinal)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault() ?? DesktopOrganizationCategoryIds.Other;

            var recommendedCategories = bucket.RecommendedCategoryIds
                .Where(c => DesktopOrganizationCategoryIds.DefaultOrder.Contains(c, StringComparer.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var recommendedExtensions = bucket.RecommendedExtensions
                .Select(DesktopOrganizationClassifier.NormalizeExtension)
                .Where(ext => !string.IsNullOrWhiteSpace(ext))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (recommendedExtensions.Count == 0 && options.AutoBindRoutingRules)
            {
                recommendedExtensions = bucketItems
                    .Select(item => DesktopOrganizationClassifier.NormalizeExtension(item.Extension))
                    .Where(ext => !string.IsNullOrWhiteSpace(ext))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(8)
                    .ToList();
            }

            string description = BuildBucketDescription(
                bucket.Reason,
                options.AutoBindRoutingRules,
                recommendedExtensions,
                matchedExistingWidget is not null);

            DesktopOrganizationTargetPlan targetPlan;
            if (matchedExistingWidget is not null)
            {
                int existingTargetIndex = targets.FindIndex(t =>
                    string.Equals(t.TargetWidgetId, matchedExistingWidget.Id, StringComparison.Ordinal));
                if (existingTargetIndex >= 0)
                {
                    var prev = targets[existingTargetIndex];
                    targets[existingTargetIndex] = prev.CloneWith(
                        prev.TargetWidgetId,
                        prev.SuggestedDisplayName,
                        prev.TargetDirectoryPath,
                        createsWidget: false,
                        prev.Items.Concat(bucketItems));
                    bucketNameToTargetMap[bucket.BucketName] = targets[existingTargetIndex];
                    continue;
                }

                targetPlan = new DesktopOrganizationTargetPlan
                {
                    SourceBucketId = $"ai:existing:{matchedExistingWidget.Id}",
                    CategoryId = dominantCategory,
                    TargetWidgetId = matchedExistingWidget.Id,
                    SuggestedDisplayName = matchedExistingWidget.Name,
                    TargetDirectoryPath = Path.GetFullPath(matchedExistingWidget.MappedFolderPath!),
                    CreatesWidget = false,
                    Items = bucketItems,
                    Description = description,
                    RecommendedCategoryIds = recommendedCategories,
                    RecommendedExtensions = recommendedExtensions,
                    AutoBindRule = false
                };
            }
            else
            {
                string displayName = string.IsNullOrWhiteSpace(bucket.BucketName)
                    ? categoryDisplayNameResolver(dominantCategory)
                    : bucket.BucketName.Trim();

                string directory = isInPlaceDesktop
                    ? effectiveRoot
                    : FileService.GetAvailablePath(
                        Path.Combine(effectiveRoot, SanitizeFolderName(displayName)),
                        reservedDirectories);
                if (!isInPlaceDesktop)
                {
                    reservedDirectories.Add(directory);
                }

                string newWidgetId = Guid.NewGuid().ToString("N");
                targetPlan = new DesktopOrganizationTargetPlan
                {
                    SourceBucketId = $"ai:bucket:{bucketOrder++}:{SanitizeFolderName(displayName)}",
                    CategoryId = dominantCategory,
                    TargetWidgetId = newWidgetId,
                    SuggestedDisplayName = displayName,
                    TargetDirectoryPath = directory,
                    CreatesWidget = true,
                    Items = bucketItems,
                    Description = description,
                    RecommendedCategoryIds = recommendedCategories,
                    RecommendedExtensions = recommendedExtensions,
                    AutoBindRule = options.AutoBindRoutingRules
                };
            }

            targets.Add(targetPlan);
            if (!string.IsNullOrWhiteSpace(bucket.BucketName))
            {
                bucketNameToTargetMap[bucket.BucketName.Trim()] = targetPlan;
            }
            bucketNameToTargetMap[targetPlan.SuggestedDisplayName] = targetPlan;
        }

        // Zero-loss safeguard: any eligible item not assigned by the LLM is grouped by category
        var unassigned = new List<DesktopOrganizationFileSnapshot>();
        for (int i = 0; i < eligibleItems.Count; i++)
        {
            if (!assignedIndices.Contains(i))
            {
                unassigned.Add(eligibleItems[i]);
            }
        }

        if (unassigned.Count > 0)
        {
            foreach (var group in unassigned.GroupBy(item => item.CategoryId, StringComparer.Ordinal))
            {
                string displayName = categoryDisplayNameResolver(group.Key);
                var existingTarget = targets.FirstOrDefault(t =>
                    string.Equals(t.SuggestedDisplayName, displayName, StringComparison.OrdinalIgnoreCase) ||
                    (t.CreatesWidget && string.Equals(t.CategoryId, group.Key, StringComparison.Ordinal) && targets.Count >= 6));

                if (existingTarget is not null)
                {
                    int idx = targets.IndexOf(existingTarget);
                    targets[idx] = existingTarget.CloneWith(
                        existingTarget.TargetWidgetId,
                        existingTarget.SuggestedDisplayName,
                        existingTarget.TargetDirectoryPath,
                        existingTarget.CreatesWidget,
                        existingTarget.Items.Concat(group));
                }
                else
                {
                    string directory = isInPlaceDesktop
                        ? effectiveRoot
                        : FileService.GetAvailablePath(
                            Path.Combine(effectiveRoot, SanitizeFolderName(displayName)),
                            reservedDirectories);
                    if (!isInPlaceDesktop)
                    {
                        reservedDirectories.Add(directory);
                    }

                    targets.Add(new DesktopOrganizationTargetPlan
                    {
                        SourceBucketId = $"ai:fallback:{group.Key}",
                        CategoryId = group.Key,
                        TargetWidgetId = Guid.NewGuid().ToString("N"),
                        SuggestedDisplayName = displayName,
                        TargetDirectoryPath = directory,
                        CreatesWidget = true,
                        Items = group.ToList(),
                        AutoBindRule = options.AutoBindRoutingRules
                    });
                }
            }
        }

        var groupSuggestions = new List<DesktopOrganizationAiGroupSuggestion>();
        if (options.EnableWidgetGroupSuggestions && aiResponse.WidgetGroups.Count > 0)
        {
            var claimedBucketIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (AiWidgetGroupSuggestionDto suggestionDto in aiResponse.WidgetGroups)
            {
                var matchedTargets = new List<DesktopOrganizationTargetPlan>();
                foreach (string name in suggestionDto.BucketNames)
                {
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    if (bucketNameToTargetMap.TryGetValue(name.Trim(), out var target) &&
                        !claimedBucketIds.Contains(target.SourceBucketId))
                    {
                        matchedTargets.Add(target);
                        claimedBucketIds.Add(target.SourceBucketId);
                    }
                }

                if (matchedTargets.Count >= 2)
                {
                    groupSuggestions.Add(new DesktopOrganizationAiGroupSuggestion
                    {
                        GroupTitle = string.IsNullOrWhiteSpace(suggestionDto.GroupTitle)
                            ? string.Join(" + ", matchedTargets.Select(t => t.SuggestedDisplayName).Take(2))
                            : suggestionDto.GroupTitle.Trim(),
                        TargetBucketIds = matchedTargets.Select(t => t.SourceBucketId).ToList(),
                        TargetWidgetIds = matchedTargets.Select(t => t.TargetWidgetId).ToList(),
                        TargetDisplayNames = matchedTargets.Select(t => t.SuggestedDisplayName).ToList(),
                        Reason = suggestionDto.Reason?.Trim() ?? string.Empty,
                        IsSelected = true
                    });
                }
            }
        }

        return new DesktopOrganizationPlan
        {
            DesktopPath = scan.DesktopPath,
            PublicDesktopPath = scan.PublicDesktopPath,
            PublicDesktopUnavailable = scan.PublicDesktopUnavailable,
            IncludePersonalDesktop = includePersonalDesktop,
            IncludePublicDesktop = includePublicDesktop,
            SourceItems = scan.Items.ToList(),
            StorageRootPath = effectiveRoot,
            StorageMode = storageMode,
            Targets = targets,
            ExcludedItems = excludedItems.ToList(),
            IsAiPlan = true,
            AiSummary = aiResponse.Summary?.Trim(),
            AiThinkingTrace = thinkingTrace,
            AiRawPromptJson = rawPromptJson,
            AiRawResponseJson = rawResponseJson,
            AiSchemaSnapshot = aiResponse,
            AiGroupSuggestions = groupSuggestions
        };
    }

    internal static AiOrganizationSchemaResponse ParseAiResponseJson(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            throw new InvalidOperationException("The AI model returned an empty response.");
        }

        string cleaned = StripReasoningTags(rawText.Trim());

        int firstBrace = cleaned.IndexOf('{');
        int lastBrace = cleaned.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            cleaned = cleaned.Substring(firstBrace, lastBrace - firstBrace + 1);
        }

        AiOrganizationSchemaResponse? parsed = JsonSerializer.Deserialize(
            cleaned,
            DesktopOrganizationAiJsonContext.Default.AiOrganizationSchemaResponse);

        if (parsed is null || parsed.Buckets.Count == 0)
        {
            throw new InvalidOperationException("The AI response did not contain valid organization buckets.");
        }

        return parsed;
    }

    internal static Dictionary<int, string> ExtractPromptIndexToNameMap(string? rawPromptJson)
    {
        var map = new Dictionary<int, string>();
        if (string.IsNullOrWhiteSpace(rawPromptJson))
        {
            return map;
        }

        try
        {
            var payload = JsonSerializer.Deserialize(
                rawPromptJson,
                DesktopOrganizationAiJsonContext.Default.AiDesktopScanPromptPayload);
            if (payload?.DesktopItems is not null)
            {
                foreach (var item in payload.DesktopItems)
                {
                    if (!string.IsNullOrWhiteSpace(item.Name))
                    {
                        map[item.Index] = item.Name.Trim();
                    }
                }
            }
        }
        catch
        {
        }

        return map;
    }

    private static string StripReasoningTags(string text)
    {
        const string openTag = "<think>";
        const string closeTag = "</think>";
        while (true)
        {
            int start = text.IndexOf(openTag, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                break;
            }

            int end = text.IndexOf(closeTag, start, StringComparison.OrdinalIgnoreCase);
            if (end < 0)
            {
                break;
            }

            text = text.Remove(start, end - start + closeTag.Length);
        }

        return text.Trim();
    }

    private static AiDesktopScanPromptPayload BuildPromptPayload(
        IReadOnlyList<DesktopOrganizationFileSnapshot> eligibleItems,
        IReadOnlyCollection<WidgetConfig> widgets,
        IReadOnlyCollection<DesktopOrganizationRule> rules,
        DesktopOrganizationAiOptions options,
        List<string>? inspectionLogs = null,
        IProgress<DesktopOrganizationAiStreamProgress>? progress = null,
        System.Diagnostics.Stopwatch? stopwatch = null)
    {
        var existingWidgets = new List<AiExistingWidgetContextDto>();
        if (options.EnableSmartWidgetReuse)
        {
            foreach (WidgetConfig widget in widgets.Where(w =>
                         w.WidgetKind == WidgetKind.File &&
                         !w.IsDisabled &&
                         !string.IsNullOrWhiteSpace(w.MappedFolderPath)))
            {
                var samples = new List<string>();
                try
                {
                    if (widget.Metadata.TryGetValue("InPlaceDesktopBucket", out string? isInPlace) &&
                        string.Equals(isInPlace, "true", StringComparison.OrdinalIgnoreCase) &&
                        widget.Items.Count > 0)
                    {
                        samples = widget.Items
                            .Select(item => Path.GetFileName(item.Path))
                            .Where(name => !string.IsNullOrWhiteSpace(name))
                            .Take(MaxSampleFilesPerWidget)
                            .Cast<string>()
                            .ToList();
                    }
                    else if (Directory.Exists(widget.MappedFolderPath))
                    {
                        samples = Directory.EnumerateFileSystemEntries(widget.MappedFolderPath)
                            .Select(Path.GetFileName)
                            .Where(name => !string.IsNullOrWhiteSpace(name))
                            .Take(MaxSampleFilesPerWidget)
                            .Cast<string>()
                            .ToList();
                    }
                }
                catch
                {
                }

                var routedExts = rules
                    .Where(r => r.IsEnabled && string.Equals(r.TargetWidgetId, widget.Id, StringComparison.Ordinal))
                    .SelectMany(r => r.Extensions)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                existingWidgets.Add(new AiExistingWidgetContextDto
                {
                    WidgetId = widget.Id,
                    Title = widget.Name,
                    SampleFiles = samples,
                    RoutedExtensions = routedExts
                });
            }
        }

        var itemDtos = new List<AiDesktopItemContextDto>(eligibleItems.Count);
        for (int i = 0; i < eligibleItems.Count; i++)
        {
            DesktopOrganizationFileSnapshot item = eligibleItems[i];
            string? deepClue = null;
            if (options.EnableDeepInspection)
            {
                deepClue = DesktopOrganizationDeepInspector.InspectItemClue(item);
            }

            if (options.EnableWebSearch)
            {
                string? webClue = DesktopOrganizationDeepInspector.LookupWebKnowledgeAsync(item, deepClue).GetAwaiter().GetResult();
                if (!string.IsNullOrWhiteSpace(webClue))
                {
                    deepClue = string.IsNullOrWhiteSpace(deepClue)
                        ? webClue
                        : $"{deepClue} | {webClue}";
                }
            }

            if (!string.IsNullOrWhiteSpace(deepClue))
            {
                inspectionLogs?.Add($"[{item.Name}] → {deepClue}");
                if (inspectionLogs is not null && inspectionLogs.Count % 4 == 0 && progress is not null && stopwatch is not null)
                {
                    progress.Report(new DesktopOrganizationAiStreamProgress
                    {
                        Stage = "inspecting",
                        StageSummary = options.EnableWebSearch
                            ? $"🌐 正在深度探查并联网检索百科 ({i + 1}/{eligibleItems.Count})：{item.Name}"
                            : $"🔬 正在深度探查 ({i + 1}/{eligibleItems.Count})：{item.Name}",
                        ThinkingText = ComposeLiveThinkingText(inspectionLogs, string.Empty, string.Empty, options.Model, stopwatch.Elapsed.TotalSeconds),
                        InspectionLogs = inspectionLogs.ToList(),
                        ElapsedSeconds = stopwatch.Elapsed.TotalSeconds
                    });
                }
            }

            itemDtos.Add(new AiDesktopItemContextDto
            {
                Index = i,
                Name = item.Name,
                Extension = item.Extension,
                BaseCategory = item.CategoryId,
                Subtype = item.SubtypeId,
                IsFolder = item.IsDirectory,
                SizeKb = Math.Max(0, item.Size / 1024),
                ModifiedDate = item.LastWriteTimeUtc == DateTime.MinValue
                    ? string.Empty
                    : item.LastWriteTimeUtc.ToString("yyyy-MM-dd"),
                DeepClue = deepClue
            });
        }

        if (!options.EnableDeepInspection && !options.EnableWebSearch)
        {
            inspectionLogs?.Add($"📚 已启用「知识库快速判断模式」（仅发送 {eligibleItems.Count} 个文件名与扩展名，不深入读取文件内容）");
        }
        else if (inspectionLogs is { Count: 0 })
        {
            inspectionLogs.Add($"🔬 已扫描 {eligibleItems.Count} 个桌面项目文件名与基础属性");
        }

        return new AiDesktopScanPromptPayload
        {
            UserPreferencePrompt = string.IsNullOrWhiteSpace(options.CustomPrompt) ? null : options.CustomPrompt.Trim(),
            EnableSmartWidgetReuse = options.EnableSmartWidgetReuse,
            AutoBindRoutingRules = options.AutoBindRoutingRules,
            EnableWidgetGroupSuggestions = options.EnableWidgetGroupSuggestions,
            DeepInspectionEnabled = options.EnableDeepInspection || options.EnableWebSearch,
            ExistingFileWidgets = existingWidgets,
            DesktopItems = itemDtos
        };
    }

    private static string BuildSystemPrompt(DesktopOrganizationAiOptions options)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are the intelligent desktop organization architect for DeskBox (a Windows desktop organizer with File Widgets and optional Widget Groups).");
        sb.AppendLine("Analyze the user's desktop items (`desktopItems`) and existing DeskBox file widgets (`existingFileWidgets`), then design a clean, domain-pure organization plan in strict JSON format.");
        sb.AppendLine();
        sb.AppendLine("### Core Rules:");
        sb.AppendLine("1. **Every single item index** from `desktopItems` (0 to N-1) MUST be assigned to exactly one bucket in `buckets`. Never omit or duplicate an index.");
        sb.AppendLine("2. **Dynamic Bucket Count & Strict Domain Purity (NO MASHUP BUCKETS!)**:");
        sb.AppendLine("   - Create **6 to 14 buckets** depending on how many distinct categories exist on the desktop.");
        sb.AppendLine("   - **NEVER mash unrelated domains together into one catch-all bucket!** Specifically:");
        sb.AppendLine("     * **Social & Voice Chat** (e.g. QQ, 企业微信, Discord, Telegram, KOOK, Oopz, YY语音, 黑盒语音, 微博) MUST be in its own dedicated bucket (e.g. `社交通讯`), NEVER merged with video players or downloaders!");
        sb.AppendLine("     * **Media & Live Streaming** (e.g. 网易云音乐, 腾讯视频, 抖音, 斗鱼直播, 哔哩哔哩直播姬) MUST be in its own dedicated bucket (e.g. `影音娱乐`), NEVER merged with browsers or network downloaders!");
        sb.AppendLine("     * **Browsers, Network & Cloud Downloaders** (e.g. Chrome, Tor Browser, Clash, UU远程, 百度网盘, 阿里云盘, 迅雷, Gopeed, Aria2, AList, IPFS) MUST be in its own dedicated bucket (e.g. `网络与下载`), NEVER merged with social chat or music/video apps!");
        sb.AppendLine("     * **Third-Party Game Launchers & Platforms** (e.g. **CurseForge** [Minecraft third-party launcher & modpack platform], **Modrinth App**, **Migurinth**, **Ubisoft Connect**, **小黑盒**, **3A社区**, **东东电竞**, **雷电模拟器**) MUST be recognized as Game Launchers/Platforms and grouped into `游戏与启动平台` (or `游戏平台与启动器`), rather than being misclassified as generic game cheats/trainers!");
        sb.AppendLine("     * **Game Trainers, Plugins & Accelerators** (e.g. 风灵月影修改器, Plus Trainer, 奇游/雷神/迅雷/虎牙/给梨加速器, 炉石/云顶插件) should be in their own `游戏修改与加速` bucket.");
        if (options.EnableDeepInspection || options.EnableWebSearch)
        {
            sb.AppendLine("   - **Deep Inspection & Web Encyclopedia Clues (`deepClue`)**: Carefully read each item's `deepClue` (especially `🌐 联网百科:` and `🌐 联网搜索:`) which provides verified online encyclopedia knowledge for specialized software and games.");
        }
        sb.AppendLine("3. **Natural, Single-Concept Naming (STOP overusing 'A与B' formula!)**:");
        sb.AppendLine("   - **DO NOT** name every bucket with the mechanical `'A与B'` pattern (e.g. forbidden: `'社交影音与网络下载'`, `'学业论文与办公文档'`, `'设计工程与视听素材'`).");
        sb.AppendLine("   - Prefer **short, crisp, single-concept Chinese titles (4 to 6 characters)**, such as: `'AI 编程开发'`, `'数据库与运维'`, `'代码与调试数据'`, `'游戏与启动平台'`, `'游戏修改与加速'`, `'社交通讯'`, `'影音娱乐'`, `'网络与下载'`, `'学业与办公'`, `'系统硬件工具'`, `'设计创作素材'`, `'Steam 运行组件'`.");
        if (options.EnableSmartWidgetReuse)
        {
            sb.AppendLine("4. **Smart Reuse of Existing Widgets**: Inspect `existingFileWidgets`. If desktop items semantically belong to an existing widget (matching its `title`, `sampleFiles`, or `routedExtensions`), set `existingWidgetId` to that widget's exact `widgetId` and `bucketName` to its `title`.");
        }
        else
        {
            sb.AppendLine("4. **New Widgets Only**: Always set `existingWidgetId` to null.");
        }
        sb.AppendLine("5. **Routing Rules Recommendation**: For each bucket, suggest `recommendedCategoryIds` (valid values: Shortcuts, Documents, Images, Media, Packages, Other) and `recommendedExtensions` (e.g. [\".pdf\", \".xlsx\"]) so DeskBox can auto-organize future files.");
        if (options.EnableWidgetGroupSuggestions)
        {
            sb.AppendLine("6. **Optional DeskBox Widget Groups**: In `widgetGroups`, you may suggest 1-3 groups that merge 2-4 closely related buckets into a tabbed DeskBox Widget Group (`groupTitle`, `bucketNames`, `reason`).");
        }
        else
        {
            sb.AppendLine("6. **No Widget Groups**: Leave `widgetGroups` as an empty array `[]`.");
        }
        sb.AppendLine("7. **Honor User Preference**: If `userPreferencePrompt` is provided, prioritize following the user's custom instructions.");
        sb.AppendLine();
        sb.AppendLine("### Required JSON Output Schema (Output ONLY valid JSON, no markdown prose):");
        sb.AppendLine("""
{
  "summary": "Brief 1-2 sentence overview of the organization strategy",
  "buckets": [
    {
      "bucketName": "Short Crisp Name (e.g. 社交通讯 / 游戏与启动平台)",
      "existingWidgetId": null,
      "reason": "Brief explanation of why these files are grouped together",
      "itemIndices": [0, 1, 2],
      "recommendedCategoryIds": ["Shortcuts"],
      "recommendedExtensions": [".lnk"]
    }
  ],
  "widgetGroups": [
    {
      "groupTitle": "Combined Group Name",
      "bucketNames": ["Bucket A", "Bucket B"],
      "reason": "Why merging these widgets into a tabbed group saves desktop space"
    }
  ]
}
""");
        return sb.ToString();
    }

    private async Task<string> SendChatCompletionAsync(
        string endpoint,
        string? apiKey,
        AiChatCompletionRequest payload,
        CancellationToken cancellationToken)
    {
        string requestJson = JsonSerializer.Serialize(
            payload,
            DesktopOrganizationAiJsonContext.Default.AiChatCompletionRequest);

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        }

        using HttpResponseMessage response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseContentRead,
            cancellationToken);

        string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            string errorDetail = responseBody;
            try
            {
                var errObj = JsonSerializer.Deserialize(
                    responseBody,
                    DesktopOrganizationAiJsonContext.Default.AiChatCompletionResponse);
                if (!string.IsNullOrWhiteSpace(errObj?.Error?.Message))
                {
                    errorDetail = errObj.Error.Message;
                }
            }
            catch
            {
            }

            throw new HttpRequestException($"AI service returned HTTP {(int)response.StatusCode}: {errorDetail}");
        }

        AiChatCompletionResponse? completion = JsonSerializer.Deserialize(
            responseBody,
            DesktopOrganizationAiJsonContext.Default.AiChatCompletionResponse);

        string? messageContent = completion?.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(messageContent))
        {
            throw new InvalidOperationException("AI service returned an empty message content.");
        }

        return messageContent;
    }

    public static string ResolveChatCompletionsEndpoint(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";
        }

        string trimmed = baseUrl.Trim().TrimEnd('/');
        if (trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        if (trimmed.Contains("generativelanguage.googleapis.com", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.EndsWith("/openai", StringComparison.OrdinalIgnoreCase))
        {
            if (trimmed.EndsWith("/v1beta", StringComparison.OrdinalIgnoreCase) ||
                trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            {
                return $"{trimmed}/openai/chat/completions";
            }

            return $"{trimmed}/v1beta/openai/chat/completions";
        }

        if (trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase) ||
            trimmed.EndsWith("/v4", StringComparison.OrdinalIgnoreCase) ||
            trimmed.EndsWith("/openai", StringComparison.OrdinalIgnoreCase))
        {
            return $"{trimmed}/chat/completions";
        }

        return $"{trimmed}/v1/chat/completions";
    }

    private static string BuildBucketDescription(
        string? reason,
        bool autoBindRules,
        IReadOnlyList<string> extensions,
        bool isExistingWidget)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(reason))
        {
            parts.Add(reason.Trim());
        }

        if (!isExistingWidget && autoBindRules && extensions.Count > 0)
        {
            parts.Add($"[{string.Join(", ", extensions.Take(5))}]");
        }

        return string.Join(" ", parts);
    }

    private static string SanitizeFolderName(string name)
    {
        string sanitized = string.Concat(name.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '_' : character)).Trim();
        return string.IsNullOrWhiteSpace(sanitized)
            ? DesktopOrganizationCategoryIds.Other
            : sanitized;
    }
}
