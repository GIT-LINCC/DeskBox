using DeskBox.Models;
using DeskBox.Platform;

namespace DeskBox.Services;

public sealed class DesktopOrganizationCoordinator
{
    private readonly SettingsService _settingsService;
    private readonly WidgetManager _widgetManager;
    private readonly OrganizerService _organizerService;
    private readonly LocalizationService _localizationService;
    private readonly DesktopOrganizationScanner _scanner;
    private readonly DesktopOrganizationPlanner _planner;
    private readonly DesktopOrganizationAiService _aiService;
    private readonly DesktopOrganizationPlacementPlanner _placementPlanner = new();
    private readonly DesktopOrganizationTransaction _transaction;

    public DesktopOrganizationCoordinator(
        SettingsService settingsService,
        FileService fileService,
        WidgetManager widgetManager,
        OrganizerService organizerService,
        LocalizationService localizationService,
        DesktopOrganizationAiService? aiService = null)
    {
        _settingsService = settingsService;
        _widgetManager = widgetManager;
        _organizerService = organizerService;
        _localizationService = localizationService;
        var classifier = new DesktopOrganizationClassifier();
        _scanner = new DesktopOrganizationScanner(classifier);
        _planner = new DesktopOrganizationPlanner(new DesktopOrganizationRuleResolver());
        _aiService = aiService ?? new DesktopOrganizationAiService();
        _transaction = new DesktopOrganizationTransaction(settingsService, fileService)
        {
            AutoOrganizationSuppressions = organizerService.AutoOrganizationSuppressions
        };
    }

    public DesktopOrganizationAiService AiService => _aiService;

    public DesktopOrganizationAiOptions GetCurrentAiOptions()
    {
        var slice = _settingsService.Settings.DesktopOrganization;
        DesktopOrganizationAiService.TryApplyLocalProfileOverrides(slice);
        return new DesktopOrganizationAiOptions
        {
            ProviderId = slice.DesktopOrganizationAiProviderId,
            BaseUrl = slice.DesktopOrganizationAiBaseUrl,
            Model = slice.DesktopOrganizationAiModel,
            CustomPrompt = slice.DesktopOrganizationAiCustomPrompt,
            EnableSmartWidgetReuse = slice.DesktopOrganizationAiEnableSmartReuse,
            AutoBindRoutingRules = slice.DesktopOrganizationAiAutoBindRules,
            EnableWidgetGroupSuggestions = slice.DesktopOrganizationAiEnableWidgetGroups,
            EnableDeepInspection = slice.DesktopOrganizationAiEnableDeepInspection,
            EnableWebSearch = slice.DesktopOrganizationAiEnableWebSearch,
            StorageMode = DesktopOrganizationStorageModes.Normalize(slice.DesktopOrganizationStorageMode)
        };
    }

    public async Task SaveAiOptionsAsync(
        DesktopOrganizationAiOptions options,
        string? apiKeyToUpdate = null,
        bool updateApiKey = false)
    {
        var slice = _settingsService.Settings.DesktopOrganization;
        slice.DesktopOrganizationAiProviderId = options.ProviderId;
        slice.DesktopOrganizationAiBaseUrl = options.BaseUrl?.Trim() ?? string.Empty;
        slice.DesktopOrganizationAiModel = options.Model?.Trim() ?? string.Empty;
        slice.DesktopOrganizationAiCustomPrompt = options.CustomPrompt?.Trim() ?? string.Empty;
        slice.DesktopOrganizationAiEnableSmartReuse = options.EnableSmartWidgetReuse;
        slice.DesktopOrganizationAiAutoBindRules = options.AutoBindRoutingRules;
        slice.DesktopOrganizationAiEnableWidgetGroups = options.EnableWidgetGroupSuggestions;
        slice.DesktopOrganizationAiEnableDeepInspection = options.EnableDeepInspection;
        slice.DesktopOrganizationAiEnableWebSearch = options.EnableWebSearch;
        slice.DesktopOrganizationStorageMode = DesktopOrganizationStorageModes.Normalize(options.StorageMode);

        if (updateApiKey)
        {
            await _aiService.SaveApiKeyAsync(options.ProviderId, apiKeyToUpdate);
        }

        await _settingsService.SaveAsync(notifySubscribers: false);
    }

    public async Task<DesktopOrganizationPlan> BuildAiPlanAsync(
        DesktopOrganizationAiOptions? optionsOverride = null,
        bool includePersonalDesktop = true,
        bool includePublicDesktop = false,
        bool includeSlowItems = false,
        IReadOnlyCollection<string>? optionalIncludedPaths = null,
        IProgress<DesktopOrganizationAiStreamProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        DesktopOrganizationScanResult scan =
            await _scanner.ScanAsync(includeSlowItems, cancellationToken);

        if (optionalIncludedPaths is { Count: > 0 })
        {
            var included = optionalIncludedPaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(Path.GetFullPath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            scan = new DesktopOrganizationScanResult
            {
                DesktopPath = scan.DesktopPath,
                PublicDesktopPath = scan.PublicDesktopPath,
                PublicDesktopUnavailable = scan.PublicDesktopUnavailable,
                Items = scan.Items.Select(item =>
                    item.CanOptIn && included.Contains(item.SourcePath)
                        ? item with { ExclusionReason = DesktopOrganizationExclusionReason.None }
                        : item).ToList()
            };
        }

        string root = SettingsService.NormalizeManagedStorageRootPath(
            _settingsService.Settings.FileWidget.DefaultManagedStorageRootPath);
        var options = optionsOverride ?? GetCurrentAiOptions();

        DesktopOrganizationPlan plan = await _aiService.GenerateAiPlanAsync(
            scan,
            root,
            _settingsService.Settings.WidgetLayout.Widgets,
            _settingsService.Settings.DesktopOrganization.DesktopOrganizationRules,
            options,
            ResolveCategoryName,
            includePersonalDesktop,
            includePublicDesktop && !scan.PublicDesktopUnavailable,
            progress,
            cancellationToken);

        AssignNonOverlappingBounds(plan);
        return plan;
    }

    public async Task<DesktopOrganizationPlan> BuildPlanFromSavedDraftAsync(
        DesktopOrganizationAiSavedDraft draft,
        bool includePersonalDesktop = true,
        bool includePublicDesktop = false,
        bool includeSlowItems = false,
        IReadOnlyCollection<string>? optionalIncludedPaths = null,
        CancellationToken cancellationToken = default)
    {
        DesktopOrganizationScanResult scan =
            await _scanner.ScanAsync(includeSlowItems, cancellationToken);

        var schema = draft.SchemaSnapshot ?? DesktopOrganizationAiService.ParseAiResponseJson(draft.RawResponseJson);
        var promptMap = DesktopOrganizationAiService.ExtractPromptIndexToNameMap(draft.RawPromptJson);
        var draftFileNames = new HashSet<string>(promptMap.Values, StringComparer.OrdinalIgnoreCase);
        foreach (var b in schema.Buckets)
        {
            if (b.ItemNames is { Count: > 0 })
            {
                foreach (string name in b.ItemNames)
                {
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        draftFileNames.Add(name.Trim());
                    }
                }
            }
        }

        var includedPathsSet = optionalIncludedPaths is { Count: > 0 }
            ? optionalIncludedPaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(Path.GetFullPath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (includedPathsSet.Count > 0 || draftFileNames.Count > 0)
        {
            scan = new DesktopOrganizationScanResult
            {
                DesktopPath = scan.DesktopPath,
                PublicDesktopPath = scan.PublicDesktopPath,
                PublicDesktopUnavailable = scan.PublicDesktopUnavailable,
                Items = scan.Items.Select(item =>
                    item.CanOptIn && (includedPathsSet.Contains(item.SourcePath) || draftFileNames.Contains(item.Name))
                        ? item with { ExclusionReason = DesktopOrganizationExclusionReason.None }
                        : item).ToList()
            };
        }

        var selectedItems = scan.Items.Select(item =>
            (item.SourceScope == DesktopOrganizationSourceScope.Public ? includePublicDesktop : includePersonalDesktop)
                ? item
                : item with { ExclusionReason = DesktopOrganizationExclusionReason.SourceNotSelected }).ToList();

        var eligibleItems = selectedItems.Where(item => item.IsEligible).ToList();
        var excludedItems = selectedItems.Where(item => !item.IsEligible).ToList();

        string root = SettingsService.NormalizeManagedStorageRootPath(
            _settingsService.Settings.FileWidget.DefaultManagedStorageRootPath);
        var options = GetCurrentAiOptions();
        options.StorageMode = DesktopOrganizationStorageModes.Normalize(draft.StorageMode);
        DesktopOrganizationPlan plan = DesktopOrganizationAiService.BuildPlanFromAiResponse(
            scan,
            root,
            eligibleItems,
            excludedItems,
            _settingsService.Settings.WidgetLayout.Widgets,
            options,
            schema,
            ResolveCategoryName,
            includePersonalDesktop,
            includePublicDesktop && !scan.PublicDesktopUnavailable,
            draft.ThinkingTrace,
            draft.RawPromptJson,
            draft.RawResponseJson);

        AssignNonOverlappingBounds(plan);
        return plan;
    }

    public async Task<DesktopOrganizationPlan> BuildPlanAsync(
        bool includeSlowItems = false,
        CancellationToken cancellationToken = default)
    {
        DesktopOrganizationScanResult scan =
            await _scanner.ScanAsync(includeSlowItems, cancellationToken);
        string root = SettingsService.NormalizeManagedStorageRootPath(
            _settingsService.Settings.DefaultManagedStorageRootPath);
        DesktopOrganizationPlan plan = _planner.CreatePlan(
            scan,
            root,
            _settingsService.Settings.Widgets,
            _settingsService.Settings.DesktopOrganizationRules,
            ResolveCategoryName);

        AssignNonOverlappingBounds(plan);
        return plan;
    }

    /// <summary>
    /// Compiles the user's preview selections into an immutable execution
    /// plan. The scan plan is never mutated, so changing a combo box cannot
    /// leak into a later refresh or into another execution attempt.
    /// </summary>
    public DesktopOrganizationPlan CreateExecutionPlan(
        DesktopOrganizationPlan previewPlan,
        IReadOnlyCollection<DesktopOrganizationTargetSelection> selections,
        IReadOnlySet<string>? excludedSourcePaths = null)
    {
        var selectionByBucket = selections
            .Where(selection => !string.IsNullOrWhiteSpace(selection.SourceBucketId))
            .ToDictionary(selection => selection.SourceBucketId, StringComparer.Ordinal);
        var widgetsById = _settingsService.Settings.Widgets
            .Where(widget =>
                widget.WidgetKind == WidgetKind.File &&
                !widget.IsDisabled &&
                !string.IsNullOrWhiteSpace(widget.MappedFolderPath))
            .ToDictionary(widget => widget.Id, StringComparer.Ordinal);
        var targetsByDestination = new Dictionary<string, DesktopOrganizationTargetPlan>(StringComparer.Ordinal);
        var retainedByChoice = new List<DesktopOrganizationFileSnapshot>();

        foreach (DesktopOrganizationTargetPlan source in previewPlan.Targets)
        {
            if (selectionByBucket.TryGetValue(source.SourceBucketId, out DesktopOrganizationTargetSelection? selection) &&
                !selection.IsSelected)
            {
                retainedByChoice.AddRange(source.Items.Select(item => item with
                {
                    ExclusionReason = DesktopOrganizationExclusionReason.UserChoice
                }));
                continue;
            }

            var includedItems = source.Items.Where(item => excludedSourcePaths?.Contains(item.SourcePath) != true).ToList();
            retainedByChoice.AddRange(source.Items.Where(item => excludedSourcePaths?.Contains(item.SourcePath) == true)
                .Select(item => item with { ExclusionReason = DesktopOrganizationExclusionReason.UserChoice }));
            DesktopOrganizationTargetPlan target = source.CloneWith(source.TargetWidgetId,
                source.SuggestedDisplayName, source.TargetDirectoryPath, source.CreatesWidget, includedItems);
            bool shouldResolveExistingDestination =
                selection?.DestinationMode == DesktopOrganizationDestinationMode.ExistingWidget ||
                !source.CreatesWidget;
            if (shouldResolveExistingDestination)
            {
                string? requestedWidgetId = selection?.DestinationMode == DesktopOrganizationDestinationMode.ExistingWidget
                    ? selection.ExistingWidgetId
                    : source.TargetWidgetId;
                if (string.IsNullOrWhiteSpace(requestedWidgetId) ||
                    !widgetsById.TryGetValue(requestedWidgetId, out WidgetConfig? widget) ||
                    string.IsNullOrWhiteSpace(widget.MappedFolderPath))
                {
                    throw new InvalidOperationException(
                        _localizationService.T("DesktopOrganization.Error.TargetUnavailable"));
                }

                target = source.CloneWith(
                    widget.Id,
                    widget.Name,
                    Path.GetFullPath(widget.MappedFolderPath),
                    createsWidget: false,
                    includedItems);
            }

            if (targetsByDestination.TryGetValue(target.TargetWidgetId, out DesktopOrganizationTargetPlan? merged))
            {
                targetsByDestination[target.TargetWidgetId] = merged.CloneWith(
                    merged.TargetWidgetId,
                    merged.SuggestedDisplayName,
                    merged.TargetDirectoryPath,
                    merged.CreatesWidget,
                    merged.Items.Concat(target.Items));
            }
            else
            {
                targetsByDestination.Add(target.TargetWidgetId, target);
            }
        }

        var executionPlan = new DesktopOrganizationPlan
        {
            Id = Guid.NewGuid().ToString("N"),
            DesktopPath = previewPlan.DesktopPath,
            PublicDesktopPath = previewPlan.PublicDesktopPath,
            IncludePersonalDesktop = previewPlan.IncludePersonalDesktop,
            IncludePublicDesktop = previewPlan.IncludePublicDesktop,
            SourceItems = previewPlan.SourceItems,
            PublicDesktopUnavailable = previewPlan.PublicDesktopUnavailable,
            StorageRootPath = previewPlan.StorageRootPath,
            StorageMode = previewPlan.StorageMode,
            Targets = targetsByDestination.Values
                .Where(target => target.Items.Count > 0)
                .ToList(),
            ExcludedItems = previewPlan.ExcludedItems
                .Concat(retainedByChoice)
                .ToList(),
            IsAiPlan = previewPlan.IsAiPlan,
            AiSummary = previewPlan.AiSummary,
            AiThinkingTrace = previewPlan.AiThinkingTrace,
            AiRawPromptJson = previewPlan.AiRawPromptJson,
            AiRawResponseJson = previewPlan.AiRawResponseJson,
            AiSchemaSnapshot = previewPlan.AiSchemaSnapshot,
            AiGroupSuggestions = previewPlan.AiGroupSuggestions
                .Where(g => g.IsSelected)
                .Select(g =>
                {
                    var mappedWidgetIds = g.TargetBucketIds
                        .Select(bucketId => previewPlan.Targets.FirstOrDefault(t => t.SourceBucketId == bucketId))
                        .Where(t => t is not null)
                        .Select(t =>
                        {
                            if (selectionByBucket.TryGetValue(t!.SourceBucketId, out var sel) &&
                                sel.DestinationMode == DesktopOrganizationDestinationMode.ExistingWidget &&
                                !string.IsNullOrWhiteSpace(sel.ExistingWidgetId))
                            {
                                return sel.ExistingWidgetId!;
                            }
                            return t.TargetWidgetId;
                        })
                        .Where(id => targetsByDestination.ContainsKey(id))
                        .Distinct(StringComparer.Ordinal)
                        .ToList();

                    return new DesktopOrganizationAiGroupSuggestion
                    {
                        Id = g.Id,
                        GroupTitle = g.GroupTitle,
                        TargetBucketIds = g.TargetBucketIds.ToList(),
                        TargetWidgetIds = mappedWidgetIds,
                        TargetDisplayNames = g.TargetDisplayNames.ToList(),
                        Reason = g.Reason,
                        IsSelected = g.IsSelected
                    };
                })
                .Where(g => g.TargetWidgetIds.Count >= 2)
                .ToList()
        };

        AssignNonOverlappingBounds(executionPlan);
        return executionPlan;
    }

    public DesktopOrganizationPlan CreatePreviewPlanWithOptionalItems(
        DesktopOrganizationPlan basePlan,
        IReadOnlyCollection<string> includedSourcePaths,
        bool? includePersonalDesktop = null,
        bool? includePublicDesktop = null)
    {
        var included = includedSourcePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        DesktopOrganizationFileSnapshot[] allItems = (basePlan.SourceItems.Count > 0
                ? basePlan.SourceItems
                : basePlan.Targets.SelectMany(target => target.Items).Concat(basePlan.ExcludedItems))
            .GroupBy(item => item.SourcePath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Select(item => item.CanOptIn && included.Contains(item.SourcePath)
                ? item with { ExclusionReason = DesktopOrganizationExclusionReason.None }
                : item)
            .ToArray();
        var scan = new DesktopOrganizationScanResult
        {
            DesktopPath = basePlan.DesktopPath,
            PublicDesktopPath = basePlan.PublicDesktopPath,
            PublicDesktopUnavailable = basePlan.PublicDesktopUnavailable,
            Items = allItems.ToList()
        };
        DesktopOrganizationPlan plan = _planner.CreatePlan(
            scan,
            basePlan.StorageRootPath,
            _settingsService.Settings.Widgets,
            _settingsService.Settings.DesktopOrganizationRules,
            ResolveCategoryName,
            includePersonalDesktop ?? basePlan.IncludePersonalDesktop,
            includePublicDesktop ?? basePlan.IncludePublicDesktop);
        AssignNonOverlappingBounds(plan);
        return plan;
    }

    public IReadOnlyList<DesktopOrganizationDestinationOption> GetDestinationOptions()
    {
        return _settingsService.Settings.Widgets
            .Where(widget =>
                widget.WidgetKind == WidgetKind.File &&
                !widget.IsDisabled &&
                !string.IsNullOrWhiteSpace(widget.MappedFolderPath))
            .OrderBy(widget => widget.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(widget => new DesktopOrganizationDestinationOption(
                widget.Id,
                widget.Name,
                Path.GetFullPath(widget.MappedFolderPath!),
                IsDynamic: false))
            .ToList();
    }

    public async Task<DesktopOrganizationExecutionResult> ExecuteAsync(
        DesktopOrganizationPlan plan,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(plan, progress: null, cancellationToken);
    }

    public async Task<DesktopOrganizationExecutionResult> ExecuteAsync(
        DesktopOrganizationPlan plan,
        IProgress<DesktopOrganizationProgress>? progress,
        CancellationToken cancellationToken = default,
        IntPtr ownerWindowHandle = default)
    {
        string[] existingTargetIds = plan.Targets
            .Where(target => !target.CreatesWidget)
            .Select(target => target.TargetWidgetId)
            .ToArray();
        _widgetManager.SetDesktopOrganizationBusy(existingTargetIds, isBusy: true);
        DesktopOrganizationExecutionResult result;
        try
        {
            result = await _transaction.ExecuteAsync(plan, progress, cancellationToken, ownerWindowHandle);
        }
        finally
        {
            _widgetManager.SetDesktopOrganizationBusy(existingTargetIds, isBusy: false);
        }

        var shownWidgetIds = new List<string>();
        try
        {
            foreach (WidgetConfig widget in result.CreatedWidgets)
            {
                await _widgetManager.ShowWidgetAsync(widget.Id, reveal: true, autoRestoreOnReveal: false);
                shownWidgetIds.Add(widget.Id);
            }

            foreach (DesktopOrganizationTargetPlan target in plan.Targets.Where(target => !target.CreatesWidget))
            {
                await _widgetManager.RefreshFileWidgetAsync(target.TargetWidgetId);
            }

            if (plan.IsAiPlan && plan.AiGroupSuggestions.Count > 0)
            {
                var activeWidgetIds = _settingsService.Settings.WidgetLayout.Widgets
                    .Where(w => !w.IsDisabled)
                    .Select(w => w.Id)
                    .ToHashSet(StringComparer.Ordinal);

                foreach (DesktopOrganizationAiGroupSuggestion groupSuggestion in plan.AiGroupSuggestions.Where(g => g.IsSelected))
                {
                    var validIds = groupSuggestion.TargetWidgetIds
                        .Where(activeWidgetIds.Contains)
                        .Distinct(StringComparer.Ordinal)
                        .ToList();
                    if (validIds.Count < 2)
                    {
                        continue;
                    }

                    string primaryId = validIds[0];
                    for (int i = 1; i < validIds.Count; i++)
                    {
                        try
                        {
                            await _widgetManager.MergeWidgetsAsync(validIds[i], primaryId);
                        }
                        catch (Exception mergeEx)
                        {
                            App.Log($"[DesktopOrganizationAi] Failed to merge widget {validIds[i]} into {primaryId}: {mergeEx.Message}");
                        }
                    }

                    await _widgetManager.SetWidgetGroupNavigationStyleAsync(primaryId, WidgetGroupNavigationStyles.Tabs);
                }
            }
            else if (result.CreatedWidgets.Count > 0)
            {
                await ApplyLiveBentoLayoutCoreAsync(PreferredLayoutStyle, useAiModel: false);
            }

            return result;
        }
        catch (Exception ex)
        {
            // The file transaction is already committed. A window failure must
            // never erase its history or initiate an unsolicited public undo.
            App.Log($"[DesktopOrganization] Could not reveal an organized widget: {ex}");
            return result;
        }
    }

    public bool HasPendingRecovery => _transaction.HasPendingRecovery;

    public Task<int> RecoverPendingAsync(IntPtr ownerWindowHandle = default) => _transaction.RecoverPendingAsync(ownerWindowHandle);

    public DesktopOrganizationPlan CreateRetryPlan(DesktopOrganizationPlan previous, IReadOnlySet<string> remainingPaths)
    {
        var plan = DesktopOrganizationPlanner.CreateRetryPlan(previous, remainingPaths, _settingsService.Settings.Widgets);
        foreach (var target in plan.Targets.Where(target => !target.CreatesWidget))
        {
            if (!_settingsService.Settings.Widgets.Any(widget => widget.Id == target.TargetWidgetId &&
                !widget.IsDisabled && widget.WidgetKind == WidgetKind.File &&
                string.Equals(widget.MappedFolderPath, target.TargetDirectoryPath, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException(_localizationService.T("DesktopOrganization.Error.TargetUnavailable"));
        }
        AssignNonOverlappingBounds(plan);
        return plan;
    }

    public async Task UndoAsync(string historyId, IntPtr ownerWindowHandle = default)
    {
        OrganizationHistoryEntry? history = _settingsService.OrganizationHistory.Entries
            .FirstOrDefault(entry =>
                string.Equals(entry.Id, historyId, StringComparison.Ordinal));
        if (history is null)
        {
            throw new InvalidOperationException("The organization history entry no longer exists.");
        }

        await _organizerService.UndoAsync(historyId, ownerWindowHandle);
        await CleanupCreatedTargetsAsync(history);
    }

    /// <summary>
    /// Stops further restore attempts for an interrupted undo and cleans up
    /// widgets the operation created that are now empty. The unblock itself
    /// is durable; widget cleanup is best-effort.
    /// </summary>
    public async Task AbandonUndoAsync(string historyId)
    {
        OrganizationHistoryEntry? history = await _transaction.AbandonUndoAsync(historyId);
        if (history is null)
        {
            return;
        }

        try
        {
            await CleanupCreatedTargetsAsync(history);
        }
        catch (Exception ex)
        {
            App.Log($"[DesktopOrganization] Widget cleanup after abandoning restore failed: {ex}");
        }
    }

    public Task AbandonPendingRecoveryAsync() => _transaction.AbandonPendingRecoveryAsync();

    private async Task CleanupCreatedTargetsAsync(OrganizationHistoryEntry history)
    {
        bool restoredInPlace = false;
        foreach (OrganizationHistoryTarget target in history.Targets)
        {
            var widgetConfig = _settingsService.Settings.WidgetLayout.Widgets
                .FirstOrDefault(w => string.Equals(w.Id, target.WidgetId, StringComparison.Ordinal));
            bool isInPlaceBucket = widgetConfig is not null &&
                widgetConfig.Metadata.TryGetValue("InPlaceDesktopBucket", out string? inPlace) &&
                string.Equals(inPlace, "true", StringComparison.OrdinalIgnoreCase);

            if (target.WasCreated && (isInPlaceBucket || !Directory.Exists(target.DirectoryPath) ||
                !Directory.EnumerateFileSystemEntries(target.DirectoryPath).Any()))
            {
                await _widgetManager.RemoveWidgetAsync(
                    target.WidgetId,
                    WidgetRemovalAction.RemoveWidgetOnly);
                _settingsService.Settings.DesktopOrganizationRules.RemoveAll(rule =>
                    string.Equals(
                        rule.TargetWidgetId,
                        target.WidgetId,
                        StringComparison.Ordinal));
                if (isInPlaceBucket)
                {
                    restoredInPlace = true;
                }
                else
                {
                    TryDeleteEmptyDirectory(target.DirectoryPath);
                }
            }
            else
            {
                await _widgetManager.RefreshFileWidgetAsync(target.WidgetId);
            }
        }

        if (restoredInPlace)
        {
            DesktopNativeIconVisibilityHelper.SetNativeDesktopIconsVisible(true);
        }

        await _settingsService.SaveAsync(notifySubscribers: false);
    }

    private string ResolveCategoryName(string categoryId)
    {
        string key = $"DesktopOrganization.Category.{categoryId}";
        string localized = _localizationService.T(key);
        return string.Equals(localized, key, StringComparison.Ordinal)
            ? categoryId
            : localized;
    }

    private readonly DesktopLayoutDesignService _layoutDesignService = new();

    public string PreferredLayoutStyle { get; set; } = DesktopLayoutStyles.StudioWings;

    public Task<ComputedDesktopLayoutPlan> DesignAndApplyLiveDesktopLayoutAsync(
        string layoutStyle,
        bool useAiModel = true,
        IProgress<DesktopOrganizationAiStreamProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        PreferredLayoutStyle = DesktopLayoutStyles.Normalize(layoutStyle);
        return ApplyLiveBentoLayoutCoreAsync(PreferredLayoutStyle, useAiModel, progress, cancellationToken);
    }

    private async Task<ComputedDesktopLayoutPlan> ApplyLiveBentoLayoutCoreAsync(
        string layoutStyle,
        bool useAiModel,
        IProgress<DesktopOrganizationAiStreamProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Win32Helper.NativeRect nativeWorkArea = default;
        if (!Win32Helper.SystemParametersInfo(SpiGetWorkArea, 0, ref nativeWorkArea, 0))
        {
            nativeWorkArea = new Win32Helper.NativeRect { Left = 0, Top = 0, Right = 1920, Bottom = 1032 };
        }

        double scale = Math.Max(1.0, Win32Helper.GetDpiForSystem() / 96d);
        var workArea = new DesktopOrganizationRect(
            nativeWorkArea.Left,
            nativeWorkArea.Top,
            nativeWorkArea.Right - nativeWorkArea.Left,
            nativeWorkArea.Bottom - nativeWorkArea.Top);
        var workAreaInt32 = new Windows.Graphics.RectInt32(
            nativeWorkArea.Left,
            nativeWorkArea.Top,
            nativeWorkArea.Right - nativeWorkArea.Left,
            nativeWorkArea.Bottom - nativeWorkArea.Top);

        var aiOptions = GetCurrentAiOptions();
        ComputedDesktopLayoutPlan layoutPlan = await _layoutDesignService.DesignLiveDesktopWithAiAsync(
            _settingsService.Settings.WidgetLayout.Widgets,
            workArea,
            scale,
            aiOptions,
            layoutStyle,
            useAiModel,
            progress,
            cancellationToken);

        await _widgetManager.ApplyComputedDesktopLayoutAsync(layoutPlan, workAreaInt32, scale);
        return layoutPlan;
    }

    private void AssignNonOverlappingBounds(DesktopOrganizationPlan plan)
    {
        if (plan.NewWidgetCount == 0)
        {
            return;
        }

        Win32Helper.NativeRect nativeWorkArea = default;
        if (!Win32Helper.SystemParametersInfo(SpiGetWorkArea, 0, ref nativeWorkArea, 0))
        {
            return;
        }

        double scale = Math.Max(1, Win32Helper.GetDpiForSystem() / 96d);
        var workArea = new DesktopOrganizationRect(
            nativeWorkArea.Left,
            nativeWorkArea.Top,
            nativeWorkArea.Right - nativeWorkArea.Left,
            nativeWorkArea.Bottom - nativeWorkArea.Top);

        DesktopLayoutDesignService.AssignAestheticBoundsToOrganizationPlan(
            plan,
            _settingsService.Settings.WidgetLayout.Widgets,
            workArea,
            scale,
            PreferredLayoutStyle);
    }

    private static void TryDeleteEmptyDirectory(string path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) &&
                Directory.Exists(path) &&
                !Directory.EnumerateFileSystemEntries(path).Any())
            {
                Directory.Delete(path);
            }
        }
        catch
        {
        }
    }

    private const uint SpiGetWorkArea = 0x0030;
}
