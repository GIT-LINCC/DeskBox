using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeskBox.Helpers;
using DeskBox.Models;

namespace DeskBox.Services;

public static class DesktopLayoutStyles
{
    public const string SurroundStage = "SurroundStage";         // 环幕剧院式：四周环绕 + 中央超大壁纸呼吸区
    public const string SplitFactions = "SplitFactions";         // 双阵营对峙：左开发生产力 + 右游戏娱乐
    public const string BottomDockGallery = "BottomDockGallery"; // 底部控制台 + 顶部迷你胶囊条
    public const string TabbedWorkspaces = "TabbedWorkspaces";   // 四大超级多标签工作舱
    public const string StudioWings = "StudioWings";             // 极客双翼工作台
    public const string BentoAdaptiveGrid = "BentoAdaptiveGrid"; // 全屏杂志 Bento 瀑布流
    public const string TopDockAndSidebar = "TopDockAndSidebar"; // 兼容旧枚举映射

    public static string Normalize(string? style)
    {
        if (string.Equals(style, SurroundStage, StringComparison.OrdinalIgnoreCase)) return SurroundStage;
        if (string.Equals(style, SplitFactions, StringComparison.OrdinalIgnoreCase)) return SplitFactions;
        if (string.Equals(style, BottomDockGallery, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(style, TopDockAndSidebar, StringComparison.OrdinalIgnoreCase)) return BottomDockGallery;
        if (string.Equals(style, TabbedWorkspaces, StringComparison.OrdinalIgnoreCase)) return TabbedWorkspaces;
        if (string.Equals(style, BentoAdaptiveGrid, StringComparison.OrdinalIgnoreCase)) return BentoAdaptiveGrid;
        return StudioWings;
    }

    public static string GetDisplayTitle(string? style) => Normalize(style) switch
    {
        SurroundStage => "🖼️ 环幕剧院式（四周环绕 + 中央巨幅壁纸区）",
        SplitFactions => "⚔️ 双阵营对峙（左侧生产力开发 · 右侧游戏娱乐）",
        BottomDockGallery => "🚀 底部控制台 + 顶栏胶囊（上屏纯净 · 下屏聚合）",
        TabbedWorkspaces => "🗂️ 四大超级工作舱（深度多标签页合并）",
        BentoAdaptiveGrid => "🍱 杂志级 Bento 瀑布流（全画幅错落矩阵）",
        _ => "🦅 极客双翼工作台（左翼仪表盘 + 右翼多列矩阵）"
    };
}

public sealed class AiLayoutWidgetPlacementDto
{
    [JsonPropertyName("widget_id")]
    public string WidgetId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("zone")]
    public string Zone { get; set; } = "right_matrix";

    [JsonPropertyName("size_tier")]
    public string SizeTier { get; set; } = "standard";

    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 50;

    [JsonPropertyName("x_pct")]
    public double? XPct { get; set; }

    [JsonPropertyName("y_pct")]
    public double? YPct { get; set; }

    [JsonPropertyName("w_pct")]
    public double? WPct { get; set; }

    [JsonPropertyName("h_pct")]
    public double? HPct { get; set; }

    [JsonPropertyName("is_collapsed")]
    public bool IsCollapsed { get; set; }
}

public sealed class AiLayoutTabGroupDto
{
    [JsonPropertyName("group_title")]
    public string GroupTitle { get; set; } = string.Empty;

    [JsonPropertyName("member_names")]
    public List<string> MemberNames { get; set; } = [];

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}

public sealed class AiDesktopLayoutResponseDto
{
    [JsonPropertyName("design_title")]
    public string DesignTitle { get; set; } = string.Empty;

    [JsonPropertyName("design_philosophy")]
    public string DesignPhilosophy { get; set; } = string.Empty;

    [JsonPropertyName("wallpaper_breathing_ratio")]
    public string WallpaperBreathingRatio { get; set; } = "45%";

    [JsonPropertyName("placements")]
    public List<AiLayoutWidgetPlacementDto> Placements { get; set; } = [];

    [JsonPropertyName("tab_groups")]
    public List<AiLayoutTabGroupDto> TabGroups { get; set; } = [];
}

public sealed class ComputedWidgetBounds
{
    public string WidgetId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public double PhysicalX { get; set; }
    public double PhysicalY { get; set; }
    public double LogicalWidth { get; set; }
    public double LogicalHeight { get; set; }
    public string Zone { get; init; } = string.Empty;
    public bool IsCollapsed { get; set; }
    public int ItemCount { get; init; }
    public WidgetKind Kind { get; init; } = WidgetKind.File;
    public string GroupTitle { get; set; } = string.Empty;
    public List<string> TabMemberNames { get; set; } = [];
    public bool IsSecondaryTabMember { get; set; }
}

public sealed class SavedDesktopTabGroupDto
{
    public string GroupTitle { get; set; } = string.Empty;
    public List<string> MemberWidgetIds { get; set; } = [];
    public string Reason { get; set; } = string.Empty;
}

public sealed class ComputedDesktopLayoutPlan
{
    public string LayoutStyle { get; init; } = DesktopLayoutStyles.StudioWings;
    public string DesignTitle { get; init; } = string.Empty;
    public string DesignPhilosophy { get; init; } = string.Empty;
    public string CustomPrompt { get; init; } = string.Empty;
    public string WallpaperBreathingRatio { get; init; } = "42%";
    public List<ComputedWidgetBounds> WidgetBounds { get; init; } = [];
    public List<(string GroupTitle, List<string> MemberWidgetIds, string Reason)> TabGroups { get; init; } = [];
}

public sealed class SavedDesktopLayoutPreset
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string LayoutStyle { get; set; } = DesktopLayoutStyles.StudioWings;
    public string DesignPhilosophy { get; set; } = string.Empty;
    public string CustomPrompt { get; set; } = string.Empty;
    public string WallpaperBreathingRatio { get; set; } = "42%";
    public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.Now;
    public List<ComputedWidgetBounds> WidgetBounds { get; set; } = [];
    public List<SavedDesktopTabGroupDto> TabGroups { get; set; } = [];

    public ComputedDesktopLayoutPlan ToPlan()
    {
        return new ComputedDesktopLayoutPlan
        {
            LayoutStyle = LayoutStyle,
            DesignTitle = Name,
            DesignPhilosophy = DesignPhilosophy,
            CustomPrompt = CustomPrompt,
            WallpaperBreathingRatio = WallpaperBreathingRatio,
            WidgetBounds = WidgetBounds.Select(b => new ComputedWidgetBounds
            {
                WidgetId = b.WidgetId,
                DisplayName = b.DisplayName,
                PhysicalX = b.PhysicalX,
                PhysicalY = b.PhysicalY,
                LogicalWidth = b.LogicalWidth,
                LogicalHeight = b.LogicalHeight,
                Zone = b.Zone,
                IsCollapsed = b.IsCollapsed,
                ItemCount = b.ItemCount,
                Kind = b.Kind,
                GroupTitle = b.GroupTitle,
                TabMemberNames = [.. (b.TabMemberNames ?? [])],
                IsSecondaryTabMember = b.IsSecondaryTabMember
            }).ToList(),
            TabGroups = (TabGroups ?? [])
                .Select(g => (g.GroupTitle, g.MemberWidgetIds ?? [], g.Reason))
                .ToList()
        };
    }

    public static SavedDesktopLayoutPreset FromPlan(ComputedDesktopLayoutPlan plan, string? customName = null)
    {
        return new SavedDesktopLayoutPreset
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = string.IsNullOrWhiteSpace(customName) ? plan.DesignTitle : customName.Trim(),
            LayoutStyle = plan.LayoutStyle,
            DesignPhilosophy = plan.DesignPhilosophy,
            CustomPrompt = plan.CustomPrompt,
            WallpaperBreathingRatio = plan.WallpaperBreathingRatio,
            SavedAt = DateTimeOffset.Now,
            WidgetBounds = plan.WidgetBounds.ToList(),
            TabGroups = plan.TabGroups.Select(g => new SavedDesktopTabGroupDto
            {
                GroupTitle = g.GroupTitle,
                MemberWidgetIds = [.. g.MemberWidgetIds],
                Reason = g.Reason
            }).ToList()
        };
    }
}

public sealed class DesktopLayoutDesignService
{
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(110)
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string SavedLayoutsFilePath =>
        Path.Combine(DeskBoxDataPathService.Current.RootPath, "ai-desktop-layouts.json");

    public static List<SavedDesktopLayoutPreset> LoadSavedLayouts()
    {
        try
        {
            string path = SavedLayoutsFilePath;
            if (!File.Exists(path))
            {
                return [];
            }

            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<SavedDesktopLayoutPreset>>(json, JsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            App.Log($"[DesktopLayoutDesigner] Failed to load saved layouts: {ex.Message}");
            return [];
        }
    }

    public static void SaveLayoutPreset(SavedDesktopLayoutPreset preset)
    {
        try
        {
            var list = LoadSavedLayouts();
            list.RemoveAll(x => string.Equals(x.Id, preset.Id, StringComparison.OrdinalIgnoreCase));
            list.Insert(0, preset);
            if (list.Count > 30)
            {
                list.RemoveRange(30, list.Count - 30);
            }

            string path = SavedLayoutsFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(list, JsonOptions));
        }
        catch (Exception ex)
        {
            App.Log($"[DesktopLayoutDesigner] Failed to save layout preset: {ex.Message}");
        }
    }

    public static void DeleteLayoutPreset(string presetId)
    {
        try
        {
            var list = LoadSavedLayouts();
            list.RemoveAll(x => string.Equals(x.Id, presetId, StringComparison.OrdinalIgnoreCase));
            string path = SavedLayoutsFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(list, JsonOptions));
        }
        catch (Exception ex)
        {
            App.Log($"[DesktopLayoutDesigner] Failed to delete layout preset: {ex.Message}");
        }
    }

    public static ComputedDesktopLayoutPlan CaptureCurrentDesktopAsPlan(
        IReadOnlyList<WidgetConfig> allWidgets,
        DesktopOrganizationRect workArea,
        double dpiScale,
        string? customTitle = null)
    {
        double scale = Math.Max(1.0, dpiScale);
        var visible = allWidgets.Where(w => w.IsVisible && !w.IsDisabled).ToList();
        var boundsList = new List<ComputedWidgetBounds>();
        var tabGroups = new List<(string GroupTitle, List<string> MemberWidgetIds, string Reason)>();

        var existingGroups = App.Current.SettingsService?.Settings is { } appSettings
            ? appSettings.WidgetLayout.WidgetGroups
            : [];

        var memberToGroup = new Dictionary<string, (string GroupTitle, List<string> MemberNames, string PrimaryId)>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in existingGroups)
        {
            var validMembers = g.MemberIds
                .Select(id => visible.FirstOrDefault(v => string.Equals(v.Id, id, StringComparison.OrdinalIgnoreCase)))
                .Where(v => v is not null)
                .Cast<WidgetConfig>()
                .ToList();
            if (validMembers.Count >= 2)
            {
                string title = !string.IsNullOrWhiteSpace(g.Name)
                    ? g.Name
                    : string.Join(" · ", validMembers.Take(2).Select(w => w.Name));
                var names = validMembers.Select(w => w.Name).ToList();
                string primaryId = validMembers[0].Id;
                tabGroups.Add((title, validMembers.Select(w => w.Id).ToList(), "当前桌面已合并的标签页组"));
                foreach (var m in validMembers)
                {
                    memberToGroup[m.Id] = (title, names, primaryId);
                }
            }
        }

        foreach (var w in visible)
        {
            bool isSecondary = false;
            List<string> tabNames = [];
            string groupTitle = string.Empty;

            if (memberToGroup.TryGetValue(w.Id, out var gInfo))
            {
                tabNames = gInfo.MemberNames;
                groupTitle = gInfo.GroupTitle;
                isSecondary = !string.Equals(gInfo.PrimaryId, w.Id, StringComparison.OrdinalIgnoreCase);
            }

            double capX = w.IsCollapsed && w.CompactPlacement is not null ? w.CompactPlacement.X : w.X;
            double capY = w.IsCollapsed && w.CompactPlacement is not null ? w.CompactPlacement.Y : w.Y;
            boundsList.Add(new ComputedWidgetBounds
            {
                WidgetId = w.Id,
                DisplayName = string.IsNullOrWhiteSpace(w.Name) ? w.WidgetKind.ToString() : w.Name,
                PhysicalX = capX > -9000 ? capX : workArea.X + 48,
                PhysicalY = capY > -9000 ? capY : workArea.Y + 48,
                LogicalWidth = Math.Clamp(w.Width, 180, 760),
                LogicalHeight = Math.Clamp(w.Height <= 80 ? 260 : w.Height, 140, 680),
                Zone = w.WidgetKind == WidgetKind.File ? "right_matrix" : "left_dock",
                IsCollapsed = w.IsCollapsed,
                ItemCount = EstimateWidgetFileCount(w),
                Kind = w.WidgetKind,
                GroupTitle = groupTitle,
                TabMemberNames = tabNames,
                IsSecondaryTabMember = isSecondary
            });
        }

        string openPct = ComputeOpenWallpaperRatioPercent(boundsList, workArea, scale);
        return new ComputedDesktopLayoutPlan
        {
            LayoutStyle = DesktopLayoutStyles.StudioWings,
            DesignTitle = string.IsNullOrWhiteSpace(customTitle) ? $"📸 当前桌面实时快照 ({DateTime.Now:HH:mm})" : customTitle!,
            DesignPhilosophy = "完整抓取当前桌面所有小组件与文件格子的真实物理坐标、尺寸、折叠胶囊状态与多标签分组关系。",
            WallpaperBreathingRatio = openPct,
            WidgetBounds = boundsList,
            TabGroups = tabGroups
        };
    }

    public static string GetDefaultStyleTitle(string? style) => DesktopLayoutStyles.GetDisplayTitle(style);

    public async Task<ComputedDesktopLayoutPlan> DesignLiveDesktopWithAiAsync(
        IReadOnlyList<WidgetConfig> allWidgets,
        DesktopOrganizationRect workArea,
        double dpiScale,
        DesktopOrganizationAiOptions aiOptions,
        string layoutStyle,
        bool useAiModel = true,
        IProgress<DesktopOrganizationAiStreamProgress>? progress = null,
        CancellationToken cancellationToken = default,
        string? customPrompt = null)
    {
        string normalizedStyle = DesktopLayoutStyles.Normalize(layoutStyle);
        var visibleWidgets = allWidgets
            .Where(w => w.IsVisible && !w.IsDisabled)
            .ToList();

        AiDesktopLayoutResponseDto? aiDesign = null;
        if (useAiModel && !string.IsNullOrWhiteSpace(aiOptions.BaseUrl))
        {
            try
            {
                aiDesign = await RequestAiSpatialDesignAsync(
                    visibleWidgets,
                    workArea,
                    dpiScale,
                    aiOptions,
                    normalizedStyle,
                    customPrompt,
                    progress,
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                App.Log($"[DesktopLayoutDesigner] AI spatial request failed, falling back to archetype solver: {ex.Message}");
            }
        }

        return ComputeBentoLayoutForLiveWidgets(
            visibleWidgets,
            workArea,
            dpiScale,
            normalizedStyle,
            aiDesign,
            customPrompt);
    }

    public static ComputedDesktopLayoutPlan AssignAestheticBoundsToOrganizationPlan(
        DesktopOrganizationPlan plan,
        IReadOnlyList<WidgetConfig> existingWidgets,
        DesktopOrganizationRect workArea,
        double dpiScale,
        string layoutStyle = DesktopLayoutStyles.StudioWings)
    {
        string normalizedStyle = DesktopLayoutStyles.Normalize(layoutStyle);
        double scale = Math.Max(1.0, dpiScale);

        var syntheticWidgets = new List<WidgetConfig>();
        var targetToSyntheticId = new Dictionary<DesktopOrganizationTargetPlan, string>();

        foreach (var existing in existingWidgets.Where(w => w.IsVisible && !w.IsDisabled && w.WidgetKind != WidgetKind.File))
        {
            syntheticWidgets.Add(existing);
        }

        var existingFileWidgetsById = existingWidgets
            .Where(w => w.WidgetKind == WidgetKind.File && !w.IsDisabled)
            .ToDictionary(w => w.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var target in plan.Targets.Where(t => t.Items.Count > 0))
        {
            if (!target.CreatesWidget &&
                !string.IsNullOrWhiteSpace(target.TargetWidgetId) &&
                existingFileWidgetsById.TryGetValue(target.TargetWidgetId, out var existingBucketWidget))
            {
                targetToSyntheticId[target] = existingBucketWidget.Id;
                if (syntheticWidgets.All(w => !string.Equals(w.Id, existingBucketWidget.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    syntheticWidgets.Add(existingBucketWidget);
                }
            }
            else
            {
                string synId = $"plan-target-{Guid.NewGuid():N}";
                targetToSyntheticId[target] = synId;
                syntheticWidgets.Add(new WidgetConfig
                {
                    Id = synId,
                    Name = target.SuggestedDisplayName,
                    WidgetKind = WidgetKind.File,
                    IsVisible = true,
                    Items = Enumerable.Repeat(new WidgetItemConfig(), Math.Max(1, target.Items.Count)).ToList()
                });
            }
        }

        var computed = ComputeBentoLayoutForLiveWidgets(
            syntheticWidgets,
            workArea,
            scale,
            normalizedStyle,
            aiDesign: null);

        var boundsById = computed.WidgetBounds.ToDictionary(b => b.WidgetId, StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in targetToSyntheticId)
        {
            if (boundsById.TryGetValue(kvp.Value, out var rect))
            {
                kvp.Key.PlannedBounds = new DesktopOrganizationRect(
                    (int)Math.Round(rect.PhysicalX),
                    (int)Math.Round(rect.PhysicalY),
                    Math.Max(220, (int)Math.Round(rect.LogicalWidth)),
                    Math.Max(160, (int)Math.Round(rect.LogicalHeight)));
            }
        }

        return computed;
    }

    public static ComputedDesktopLayoutPlan ComputeBentoLayoutForLiveWidgets(
        IReadOnlyList<WidgetConfig> visibleWidgets,
        DesktopOrganizationRect workArea,
        double dpiScale,
        string layoutStyle,
        AiDesktopLayoutResponseDto? aiDesign = null,
        string? customPrompt = null)
    {
        double scale = Math.Max(1.0, dpiScale);
        string style = DesktopLayoutStyles.Normalize(layoutStyle);

        double logicalScreenW = workArea.Width / scale;
        double logicalScreenH = workArea.Height / scale;
        double padX = Math.Clamp(logicalScreenW * 0.016, 16, 28);
        double padY = Math.Clamp(logicalScreenH * 0.020, 16, 28);
        double gap = 14.0;
        double usableW = Math.Max(800, logicalScreenW - padX * 2);
        double usableH = Math.Max(500, logicalScreenH - padY * 2);

        var utilityWidgets = visibleWidgets.Where(w => w.WidgetKind != WidgetKind.File).ToList();
        var fileWidgets = visibleWidgets.Where(w => w.WidgetKind == WidgetKind.File).ToList();

        var itemCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var fw in fileWidgets)
        {
            itemCounts[fw.Id] = EstimateWidgetFileCount(fw);
        }

        // 1. 解析多标签分组（AI 设计优先，或者根据风格与额外提示词决定）
        bool forbidTabs = !string.IsNullOrWhiteSpace(customPrompt) &&
            (customPrompt.Contains("不合并", StringComparison.OrdinalIgnoreCase) ||
             customPrompt.Contains("独立", StringComparison.OrdinalIgnoreCase) ||
             customPrompt.Contains("不要标签", StringComparison.OrdinalIgnoreCase));

        var tabGroups = forbidTabs
            ? []
            : ResolveTabGroups(fileWidgets, itemCounts, style, aiDesign);

        var secondaryMemberToLeader = new Dictionary<string, (string LeaderId, string GroupTitle, List<string> TabNames)>(StringComparer.OrdinalIgnoreCase);
        var leaderTabInfo = new Dictionary<string, (string GroupTitle, List<string> TabNames, int CombinedCount)>(StringComparer.OrdinalIgnoreCase);

        foreach (var tg in tabGroups)
        {
            if (tg.MemberWidgetIds.Count < 2) continue;
            string leaderId = tg.MemberWidgetIds[0];
            var tabNames = tg.MemberWidgetIds
                .Select(id => fileWidgets.FirstOrDefault(w => string.Equals(w.Id, id, StringComparison.OrdinalIgnoreCase))?.Name ?? id)
                .ToList();
            int combinedItems = tg.MemberWidgetIds.Sum(id => itemCounts.GetValueOrDefault(id, 8));
            leaderTabInfo[leaderId] = (tg.GroupTitle, tabNames, combinedItems);
            for (int i = 1; i < tg.MemberWidgetIds.Count; i++)
            {
                secondaryMemberToLeader[tg.MemberWidgetIds[i]] = (leaderId, tg.GroupTitle, tabNames);
            }
        }

        // 只有未被合并为次级标签的组件才作为独立容器参与桌面空间布局！
        var primaryFileWidgets = fileWidgets
            .Where(w => !secondaryMemberToLeader.ContainsKey(w.Id))
            .ToList();

        var aiPlacementById = new Dictionary<string, AiLayoutWidgetPlacementDto>(StringComparer.OrdinalIgnoreCase);
        if (aiDesign?.Placements is { Count: > 0 })
        {
            foreach (var p in aiDesign.Placements)
            {
                var matched = visibleWidgets.FirstOrDefault(w =>
                    string.Equals(w.Id, p.WidgetId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(w.Name, p.Name, StringComparison.OrdinalIgnoreCase));
                if (matched is not null)
                {
                    aiPlacementById[matched.Id] = p;
                }
            }
        }

        primaryFileWidgets = primaryFileWidgets
            .OrderByDescending(w =>
            {
                if (aiPlacementById.TryGetValue(w.Id, out var p)) return p.Priority * 100 + itemCounts.GetValueOrDefault(w.Id, 0);
                return leaderTabInfo.TryGetValue(w.Id, out var info) ? info.CombinedCount + 40 : itemCounts.GetValueOrDefault(w.Id, 0);
            })
            .ToList();

        var activeContainers = new List<ComputedWidgetBounds>();

        // 检查 AI 是否给出了有效的自由 2D 归一化坐标 (x_pct, y_pct)
        int aiCoordCount = aiPlacementById.Values.Count(p => p.XPct.HasValue && p.YPct.HasValue);
        if (aiCoordCount >= Math.Max(2, (utilityWidgets.Count + primaryFileWidgets.Count) / 2))
        {
            foreach (var w in utilityWidgets.Concat(primaryFileWidgets))
            {
                aiPlacementById.TryGetValue(w.Id, out var p);
                double xPct = Math.Clamp(p?.XPct ?? 0.05, 0.01, 0.88);
                double yPct = Math.Clamp(p?.YPct ?? 0.05, 0.02, 0.86);
                double defaultW = w.WidgetKind == WidgetKind.File ? 0.22 : 0.17;
                double defaultH = w.WidgetKind == WidgetKind.File ? 0.28 : 0.22;
                double wPct = Math.Clamp(p?.WPct ?? defaultW, 0.14, 0.46);
                double hPct = Math.Clamp(p?.HPct ?? defaultH, 0.22, 0.56);

                leaderTabInfo.TryGetValue(w.Id, out var tInfo);
                activeContainers.Add(new ComputedWidgetBounds
                {
                    WidgetId = w.Id,
                    DisplayName = w.Name,
                    PhysicalX = Math.Round((padX + xPct * usableW) * scale + workArea.X),
                    PhysicalY = Math.Round((padY + yPct * usableH) * scale + workArea.Y),
                    LogicalWidth = Math.Round(wPct * usableW),
                    LogicalHeight = p?.IsCollapsed == true ? Math.Max(260, Math.Round(hPct * usableH)) : Math.Round(hPct * usableH),
                    Zone = p?.Zone ?? (w.WidgetKind == WidgetKind.File ? "freeform" : "left_dock"),
                    IsCollapsed = p?.IsCollapsed ?? false,
                    ItemCount = tInfo.CombinedCount > 0 ? tInfo.CombinedCount : itemCounts.GetValueOrDefault(w.Id, 0),
                    Kind = w.WidgetKind,
                    GroupTitle = tInfo.GroupTitle ?? string.Empty,
                    TabMemberNames = tInfo.TabNames ?? []
                });
            }

            ResolveFreeformNonOverlappingBounds(activeContainers, workArea, scale, padX, padY, gap);
        }
        else
        {
            // 使用 6 种截然不同的建筑空间构图引擎生成几何排布
            BuildArchetypeSpatialComposition(
                style,
                utilityWidgets,
                primaryFileWidgets,
                itemCounts,
                leaderTabInfo,
                aiPlacementById,
                activeContainers,
                workArea,
                scale,
                usableW,
                usableH,
                padX,
                padY,
                gap,
                customPrompt);
        }

        // 将被合并为次级标签页的成员挂载到主容器相同的坐标与宽高下
        var finalBounds = new List<ComputedWidgetBounds>(activeContainers);
        var activeById = activeContainers.ToDictionary(b => b.WidgetId, StringComparer.OrdinalIgnoreCase);

        foreach (var secondary in fileWidgets.Where(w => secondaryMemberToLeader.ContainsKey(w.Id)))
        {
            var leaderMeta = secondaryMemberToLeader[secondary.Id];
            if (activeById.TryGetValue(leaderMeta.LeaderId, out var leaderRect))
            {
                finalBounds.Add(new ComputedWidgetBounds
                {
                    WidgetId = secondary.Id,
                    DisplayName = secondary.Name,
                    PhysicalX = leaderRect.PhysicalX,
                    PhysicalY = leaderRect.PhysicalY,
                    LogicalWidth = leaderRect.LogicalWidth,
                    LogicalHeight = leaderRect.LogicalHeight,
                    Zone = leaderRect.Zone,
                    IsCollapsed = leaderRect.IsCollapsed,
                    ItemCount = itemCounts.GetValueOrDefault(secondary.Id, 0),
                    Kind = secondary.WidgetKind,
                    GroupTitle = leaderMeta.GroupTitle,
                    TabMemberNames = leaderMeta.TabNames,
                    IsSecondaryTabMember = true
                });
            }
        }

        string openRatio = ComputeOpenWallpaperRatioPercent(activeContainers, workArea, scale);
        string defaultTitle = DesktopLayoutStyles.GetDisplayTitle(style);

        string defaultPhilosophy = style switch
        {
            DesktopLayoutStyles.SurroundStage =>
                $"采用「环幕剧院式」构图，将组件与分类格沿屏幕四周边缘错落环绕，中央留出 {openRatio} 的完整壁纸舞台区域，视觉通透无遮挡。",
            DesktopLayoutStyles.SplitFactions =>
                $"采用「左右双阵营对峙」构图，左侧聚合开发工具、生产力与桌面仪表盘，右侧聚合游戏娱乐与影音媒体，中间形成清晰的黄金分割视觉走廊（留白率 {openRatio}）。",
            DesktopLayoutStyles.BottomDockGallery =>
                $"采用「底部控制台 + 顶置胶囊条」构图，低频分类折叠为顶部悬浮胶囊，核心高频工作舱下沉至屏幕下半区，释放上半屏完整壁纸天际线（留白率 {openRatio}）。",
            DesktopLayoutStyles.TabbedWorkspaces =>
                $"采用「四大超级多标签工作舱」构图，将 {fileWidgets.Count} 个零散文件格深度合并为 {activeContainers.Count(c => c.Kind == WidgetKind.File)} 个大尺寸多标签容器，彻底消除桌面碎片感（留白率 {openRatio}）。",
            DesktopLayoutStyles.BentoAdaptiveGrid =>
                $"采用「杂志级 Bento 瀑布流」构图，根据每个分类内文件密度自适应分配卡片高矮与跨度，形成错落有致的现代 Bento 矩阵（留白率 {openRatio}）。",
            _ =>
                $"采用「极客双翼工作台」构图，左侧构建天气、待办、速记垂直效率坞，右侧按黄金比例排布高频文件分类矩阵（留白率 {openRatio}）。"
        };

        return new ComputedDesktopLayoutPlan
        {
            LayoutStyle = style,
            DesignTitle = !string.IsNullOrWhiteSpace(aiDesign?.DesignTitle) ? aiDesign.DesignTitle : defaultTitle,
            DesignPhilosophy = !string.IsNullOrWhiteSpace(aiDesign?.DesignPhilosophy) ? aiDesign.DesignPhilosophy : defaultPhilosophy,
            CustomPrompt = customPrompt ?? string.Empty,
            WallpaperBreathingRatio = openRatio,
            WidgetBounds = finalBounds,
            TabGroups = tabGroups
        };
    }

    private static void BuildArchetypeSpatialComposition(
        string style,
        List<WidgetConfig> utilityWidgets,
        List<WidgetConfig> primaryFileWidgets,
        Dictionary<string, int> itemCounts,
        Dictionary<string, (string GroupTitle, List<string> TabNames, int CombinedCount)> leaderTabInfo,
        Dictionary<string, AiLayoutWidgetPlacementDto> aiPlacementById,
        List<ComputedWidgetBounds> output,
        DesktopOrganizationRect workArea,
        double scale,
        double usableW,
        double usableH,
        double padX,
        double padY,
        double gap,
        string? customPrompt)
    {
        bool preferCollapsedTop = style == DesktopLayoutStyles.BottomDockGallery ||
            (!string.IsNullOrWhiteSpace(customPrompt) && customPrompt.Contains("胶囊", StringComparison.OrdinalIgnoreCase));

        ComputedWidgetBounds MakeBounds(WidgetConfig w, double lx, double ly, double lw, double lh, string zone, bool collapsed = false)
        {
            leaderTabInfo.TryGetValue(w.Id, out var tInfo);
            int count = tInfo.CombinedCount > 0 ? tInfo.CombinedCount : itemCounts.GetValueOrDefault(w.Id, 0);
            return new ComputedWidgetBounds
            {
                WidgetId = w.Id,
                DisplayName = w.Name,
                PhysicalX = Math.Round((padX + lx) * scale + workArea.X),
                PhysicalY = Math.Round((padY + ly) * scale + workArea.Y),
                LogicalWidth = Math.Round(lw),
                LogicalHeight = collapsed ? Math.Max(260, Math.Round(lh)) : Math.Round(lh),
                Zone = zone,
                IsCollapsed = collapsed,
                ItemCount = count,
                Kind = w.WidgetKind,
                GroupTitle = tInfo.GroupTitle ?? string.Empty,
                TabMemberNames = tInfo.TabNames ?? []
            };
        }

        if (style == DesktopLayoutStyles.SurroundStage)
        {
            // 环幕剧院式：左岸工具列 + 顶栏胶囊/横幅 + 右岸分类列 + 底岸精选，中央 50%×58% 完全留白
            double leftW = Math.Clamp(usableW * 0.19, 250, 320);
            double rightW = Math.Clamp(usableW * 0.24, 300, 410);
            double curLeftY = 0;

            foreach (var uw in utilityWidgets)
            {
                double h = uw.WidgetKind == WidgetKind.Weather ? 155 : 220;
                if (curLeftY + h > usableH) h = Math.Max(130, usableH - curLeftY);
                output.Add(MakeBounds(uw, 0, curLeftY, leftW, h, "left_wing"));
                curLeftY += h + gap;
            }

            // 将文件组件分配到：左岸剩余、顶部横排、右侧双列、底部横排
            int idx = 0;
            while (idx < primaryFileWidgets.Count && curLeftY + 185 <= usableH)
            {
                var fw = primaryFileWidgets[idx++];
                output.Add(MakeBounds(fw, 0, curLeftY, leftW, 195, "left_wing"));
                curLeftY += 195 + gap;
            }

            var remaining = primaryFileWidgets.Skip(idx).ToList();
            int rightCount = Math.Min(remaining.Count, 4);
            double curRightY = 0;
            for (int i = 0; i < rightCount; i++)
            {
                var fw = remaining[i];
                double h = Math.Clamp((usableH - (rightCount - 1) * gap) / rightCount, 165, 250);
                output.Add(MakeBounds(fw, usableW - rightW, curRightY, rightW, h, "right_wing"));
                curRightY += h + gap;
            }

            var centerRim = remaining.Skip(rightCount).ToList();
            if (centerRim.Count > 0)
            {
                double centerStartX = leftW + gap * 1.5;
                double centerSpanW = Math.Max(340, usableW - leftW - rightW - gap * 3);
                int topCount = (centerRim.Count + 1) / 2;
                double topCardW = Math.Clamp((centerSpanW - (topCount - 1) * gap) / Math.Max(1, topCount), 220, 360);
                for (int i = 0; i < topCount; i++)
                {
                    output.Add(MakeBounds(centerRim[i], centerStartX + i * (topCardW + gap), 0, topCardW, 185, "top_rim"));
                }

                var bottomRim = centerRim.Skip(topCount).ToList();
                if (bottomRim.Count > 0)
                {
                    double botCardW = Math.Clamp((centerSpanW - (bottomRim.Count - 1) * gap) / bottomRim.Count, 220, 360);
                    double botY = Math.Max(205, usableH - 195);
                    for (int i = 0; i < bottomRim.Count; i++)
                    {
                        output.Add(MakeBounds(bottomRim[i], centerStartX + i * (botCardW + gap), botY, botCardW, 195, "bottom_rim"));
                    }
                }
            }
            return;
        }

        if (style == DesktopLayoutStyles.SplitFactions)
        {
            // 左右双阵营对峙：左侧生产力/开发阵营 (2列)，右侧游戏/娱乐/媒体阵营 (2列)，中间 16% 视觉峡谷走廊
            double wingW = Math.Clamp(usableW * 0.42, 540, 780);
            double colW = (wingW - gap) / 2.0;

            var leftFaction = new List<WidgetConfig>();
            var rightFaction = new List<WidgetConfig>();

            foreach (var fw in primaryFileWidgets)
            {
                if (IsEntertainmentOrMediaBucket(fw.Name))
                    rightFaction.Add(fw);
                else
                    leftFaction.Add(fw);
            }

            // 平衡左右阵营数量
            while (leftFaction.Count > rightFaction.Count + 2 && leftFaction.Count > 1)
            {
                var moved = leftFaction[^1];
                leftFaction.RemoveAt(leftFaction.Count - 1);
                rightFaction.Insert(0, moved);
            }

            double[] leftColsY = [0, 0];
            foreach (var uw in utilityWidgets)
            {
                int col = leftColsY[0] <= leftColsY[1] ? 0 : 1;
                double h = uw.WidgetKind == WidgetKind.Weather ? 150 : 210;
                output.Add(MakeBounds(uw, col * (colW + gap), leftColsY[col], colW, h, "left_faction"));
                leftColsY[col] += h + gap;
            }

            foreach (var fw in leftFaction)
            {
                int col = leftColsY[0] <= leftColsY[1] ? 0 : 1;
                double h = ComputeCardHeight(itemCounts.GetValueOrDefault(fw.Id, 8), usableH);
                if (leftColsY[col] + h > usableH) h = Math.Max(155, usableH - leftColsY[col]);
                output.Add(MakeBounds(fw, col * (colW + gap), leftColsY[col], colW, h, "left_faction"));
                leftColsY[col] += h + gap;
            }

            double rightOriginX = usableW - wingW;
            double[] rightColsY = [0, 0];
            foreach (var fw in rightFaction)
            {
                int col = rightColsY[0] <= rightColsY[1] ? 0 : 1;
                double h = ComputeCardHeight(itemCounts.GetValueOrDefault(fw.Id, 8), usableH);
                if (rightColsY[col] + h > usableH) h = Math.Max(155, usableH - rightColsY[col]);
                output.Add(MakeBounds(fw, rightOriginX + col * (colW + gap), rightColsY[col], colW, h, "right_faction"));
                rightColsY[col] += h + gap;
            }
            return;
        }

        if (style == DesktopLayoutStyles.BottomDockGallery)
        {
            // 顶部紧凑工具与悬浮胶囊条 + 底部单排整齐工作坞（彻底释放屏幕中央壁纸天际线）
            double topBarX = 0;
            foreach (var uw in utilityWidgets)
            {
                double w = uw.WidgetKind == WidgetKind.Weather ? 240 : 260;
                double h = 140;
                output.Add(MakeBounds(uw, topBarX, 0, w, h, "top_bar", collapsed: false));
                topBarX += w + gap;
            }

            // 将文件数较少的次要分类放到右上角折叠为迷你胶囊，确保底部工作坞保持在 5 个以内单排对齐
            int targetDockCount = Math.Min(5, primaryFileWidgets.Count);
            int capsuleCount = preferCollapsedTop
                ? Math.Max(primaryFileWidgets.Count - targetDockCount, Math.Min(3, Math.Max(0, primaryFileWidgets.Count - 4)))
                : Math.Max(0, primaryFileWidgets.Count - 5);
            var capsuleWidgets = primaryFileWidgets.TakeLast(capsuleCount).ToList();
            var dockWidgets = primaryFileWidgets.Take(primaryFileWidgets.Count - capsuleCount).ToList();

            double capX = usableW;
            foreach (var cw in capsuleWidgets)
            {
                double w = 210;
                capX -= w;
                output.Add(MakeBounds(cw, Math.Max(topBarX, capX), 6, w, 44, "top_capsule", collapsed: true));
                capX -= gap;
            }

            // 底部工作坞：最多 5 列单排底部对齐，释放中央 45% 以上的通透壁纸区
            int cols = Math.Clamp(dockWidgets.Count, 2, 5);
            double colW = (usableW - (cols - 1) * gap) / cols;
            double dockStartY = usableH * 0.58;
            double dockCardH = Math.Clamp(usableH - dockStartY, 220, 340);
            double[] colY = new double[cols];
            Array.Fill(colY, dockStartY);

            foreach (var fw in dockWidgets)
            {
                int bestCol = 0;
                for (int c = 1; c < cols; c++)
                {
                    if (colY[c] < colY[bestCol]) bestCol = c;
                }

                double h = dockWidgets.Count <= cols
                    ? dockCardH
                    : Math.Clamp((usableH - dockStartY - gap) / 2.0, 175, 240);
                if (colY[bestCol] + h > usableH) h = Math.Max(160, usableH - colY[bestCol]);
                output.Add(MakeBounds(fw, bestCol * (colW + gap), colY[bestCol], colW, h, "bottom_dock"));
                colY[bestCol] += h + gap;
            }
            return;
        }

        if (style == DesktopLayoutStyles.TabbedWorkspaces)
        {
            // 四大超级多标签工作舱：左侧工具窄栏 + 右侧 2×2 巨幅多标签旗舰容器
            double utilW = Math.Clamp(usableW * 0.19, 250, 310);
            double utilY = 0;
            foreach (var uw in utilityWidgets)
            {
                double h = uw.WidgetKind == WidgetKind.Weather ? 155 : 225;
                output.Add(MakeBounds(uw, 0, utilY, utilW, h, "left_dock"));
                utilY += h + gap;
            }

            int gridCols = primaryFileWidgets.Count <= 2 ? 1 : 2;
            int gridRows = Math.Max(1, (int)Math.Ceiling(primaryFileWidgets.Count / (double)gridCols));
            double matrixW = Math.Clamp(usableW * 0.62, 640, usableW - utilW - gap * 3);
            double matrixStartX = usableW - matrixW;
            double cellW = (matrixW - (gridCols - 1) * gap) / gridCols;
            double cellH = Math.Clamp((usableH - (gridRows - 1) * gap) / gridRows, 220, 380);

            for (int i = 0; i < primaryFileWidgets.Count; i++)
            {
                int r = i / gridCols;
                int c = i % gridCols;
                output.Add(MakeBounds(
                    primaryFileWidgets[i],
                    matrixStartX + c * (cellW + gap),
                    r * (cellH + gap),
                    cellW,
                    cellH,
                    "tab_hub"));
            }
            return;
        }

        // 默认 StudioWings 或 BentoAdaptiveGrid
        bool isBentoFull = style == DesktopLayoutStyles.BentoAdaptiveGrid;
        double leftDockW = isBentoFull ? Math.Clamp(usableW * 0.18, 240, 300) : Math.Clamp(usableW * 0.19, 250, 320);
        double dockY = 0;
        foreach (var uw in utilityWidgets)
        {
            double h = uw.WidgetKind == WidgetKind.Weather ? 150 : 215;
            output.Add(MakeBounds(uw, 0, dockY, leftDockW, h, "left_dock"));
            dockY += h + gap;
        }

        int matrixCols = primaryFileWidgets.Count <= 4 ? 2 : 3;
        double breathingGap = isBentoFull ? gap * 1.5 : Math.Max(gap * 2, usableW * 0.12);
        double rightMatrixW = Math.Max(580, usableW - leftDockW - breathingGap);
        double rightStartX = usableW - rightMatrixW;
        double matrixColW = (rightMatrixW - (matrixCols - 1) * gap) / matrixCols;
        double[] colHeights = new double[matrixCols];

        foreach (var fw in primaryFileWidgets)
        {
            int targetCol = 0;
            for (int c = 1; c < matrixCols; c++)
            {
                if (colHeights[c] < colHeights[targetCol]) targetCol = c;
            }

            int cnt = itemCounts.GetValueOrDefault(fw.Id, 8);
            double cardH = ComputeCardHeight(cnt, usableH);
            if (colHeights[targetCol] + cardH > usableH)
            {
                cardH = Math.Max(155, usableH - colHeights[targetCol]);
            }

            output.Add(MakeBounds(fw, rightStartX + targetCol * (matrixColW + gap), colHeights[targetCol], matrixColW, cardH, "right_matrix"));
            colHeights[targetCol] += cardH + gap;
        }
    }

    private static double ComputeCardHeight(int itemCount, double usableH)
    {
        if (itemCount >= 22) return Math.Clamp(usableH * 0.34, 250, 330);
        if (itemCount >= 10) return Math.Clamp(usableH * 0.25, 195, 250);
        return Math.Clamp(usableH * 0.19, 155, 195);
    }

    /// <summary>
    /// 2D 自由坐标防重叠松弛求解器：保留 AI 设计的 2D 方位与比例，消除任何像素级重叠并限制在工作区内。
    /// </summary>
    private static void ResolveFreeformNonOverlappingBounds(
        List<ComputedWidgetBounds> boxes,
        DesktopOrganizationRect workArea,
        double scale,
        double padX,
        double padY,
        double gap)
    {
        double minX = workArea.X + padX * scale;
        double minY = workArea.Y + padY * scale;
        double maxX = workArea.X + workArea.Width - padX * scale;
        double maxY = workArea.Y + workArea.Height - padY * scale;
        double physGap = gap * scale;

        for (int iter = 0; iter < 18; iter++)
        {
            bool anyOverlap = false;
            for (int i = 0; i < boxes.Count; i++)
            {
                var a = boxes[i];
                double aw = a.LogicalWidth * scale;
                double ah = (a.IsCollapsed ? 44 : a.LogicalHeight) * scale;
                a.PhysicalX = Math.Clamp(a.PhysicalX, minX, Math.Max(minX, maxX - aw));
                a.PhysicalY = Math.Clamp(a.PhysicalY, minY, Math.Max(minY, maxY - ah));

                for (int j = i + 1; j < boxes.Count; j++)
                {
                    var b = boxes[j];
                    double bw = b.LogicalWidth * scale;
                    double bh = (b.IsCollapsed ? 44 : b.LogicalHeight) * scale;

                    double overlapX = Math.Min(a.PhysicalX + aw + physGap, b.PhysicalX + bw + physGap) - Math.Max(a.PhysicalX, b.PhysicalX);
                    double overlapY = Math.Min(a.PhysicalY + ah + physGap, b.PhysicalY + bh + physGap) - Math.Max(a.PhysicalY, b.PhysicalY);

                    if (overlapX > 0 && overlapY > 0)
                    {
                        anyOverlap = true;
                        if (overlapX < overlapY)
                        {
                            double shift = overlapX / 2.0 + 2;
                            if (a.PhysicalX <= b.PhysicalX) { a.PhysicalX -= shift; b.PhysicalX += shift; }
                            else { a.PhysicalX += shift; b.PhysicalX -= shift; }
                        }
                        else
                        {
                            double shift = overlapY / 2.0 + 2;
                            if (a.PhysicalY <= b.PhysicalY) { a.PhysicalY -= shift; b.PhysicalY += shift; }
                            else { a.PhysicalY += shift; b.PhysicalY -= shift; }
                        }
                    }
                }
            }

            if (!anyOverlap) break;
        }
    }

    private static string ComputeOpenWallpaperRatioPercent(
        IReadOnlyList<ComputedWidgetBounds> activeBoxes,
        DesktopOrganizationRect workArea,
        double scale)
    {
        double screenArea = Math.Max(1.0, (double)workArea.Width * workArea.Height);
        double covered = 0;
        foreach (var b in activeBoxes.Where(x => !x.IsSecondaryTabMember))
        {
            double effectiveH = b.IsCollapsed ? 44 : b.LogicalHeight;
            covered += (b.LogicalWidth * scale) * (effectiveH * scale);
        }

        int openPct = Math.Clamp((int)Math.Round((1.0 - Math.Min(0.85, covered / screenArea)) * 100), 20, 88);
        return $"{openPct}%";
    }

    private static List<(string GroupTitle, List<string> MemberWidgetIds, string Reason)> ResolveTabGroups(
        IReadOnlyList<WidgetConfig> fileWidgets,
        IReadOnlyDictionary<string, int> itemCounts,
        string style,
        AiDesktopLayoutResponseDto? aiDesign)
    {
        var results = new List<(string GroupTitle, List<string> MemberWidgetIds, string Reason)>();
        var assignedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (aiDesign?.TabGroups is { Count: > 0 })
        {
            foreach (var groupDto in aiDesign.TabGroups)
            {
                var matchedIds = new List<string>();
                foreach (string memberName in groupDto.MemberNames)
                {
                    var match = fileWidgets.FirstOrDefault(w =>
                        !assignedIds.Contains(w.Id) &&
                        (string.Equals(w.Id, memberName, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(w.Name, memberName, StringComparison.OrdinalIgnoreCase) ||
                         w.Name.Contains(memberName, StringComparison.OrdinalIgnoreCase)));
                    if (match is not null)
                    {
                        matchedIds.Add(match.Id);
                        assignedIds.Add(match.Id);
                    }
                }

                if (matchedIds.Count >= 2)
                {
                    results.Add((
                        string.IsNullOrWhiteSpace(groupDto.GroupTitle) ? "组合工作舱" : groupDto.GroupTitle,
                        matchedIds,
                        groupDto.Reason));
                }
            }
        }

        if (results.Count == 0 && (style == DesktopLayoutStyles.TabbedWorkspaces || fileWidgets.Count > 8))
        {
            var devGroup = fileWidgets
                .Where(w => !assignedIds.Contains(w.Id) && IsDevOrProductivityBucket(w.Name))
                .Select(w => w.Id)
                .Take(3)
                .ToList();
            if (devGroup.Count >= 2)
            {
                foreach (var id in devGroup) assignedIds.Add(id);
                results.Add(("💻 研发与效率工作舱", devGroup, "将开发与办公效率分类合并为多标签工作舱"));
            }

            var gameMediaGroup = fileWidgets
                .Where(w => !assignedIds.Contains(w.Id) && IsEntertainmentOrMediaBucket(w.Name))
                .Select(w => w.Id)
                .Take(3)
                .ToList();
            if (gameMediaGroup.Count >= 2)
            {
                foreach (var id in gameMediaGroup) assignedIds.Add(id);
                results.Add(("🎮 娱乐与影音中心", gameMediaGroup, "将游戏、辅助与媒体分类聚合为多标签娱乐中心"));
            }

            if (style == DesktopLayoutStyles.TabbedWorkspaces)
            {
                var rest = fileWidgets.Where(w => !assignedIds.Contains(w.Id)).Select(w => w.Id).ToList();
                while (rest.Count >= 2)
                {
                    var chunk = rest.Take(3).ToList();
                    rest = rest.Skip(3).ToList();
                    results.Add(("📂 综合资源与归档舱", chunk, "深度聚合零散文件格为统一多标签面板"));
                }
            }
        }

        return results;
    }

    private static bool IsDevOrProductivityBucket(string name)
    {
        string n = name.ToLowerInvariant();
        return n.Contains("开发") || n.Contains("编程") || n.Contains("代码") ||
               n.Contains("ai") || n.Contains("工具") || n.Contains("系统") ||
               n.Contains("硬件") || n.Contains("办公") || n.Contains("文档");
    }

    private static bool IsEntertainmentOrMediaBucket(string name)
    {
        string n = name.ToLowerInvariant();
        return n.Contains("游戏") || n.Contains("娱乐") || n.Contains("影音") ||
               n.Contains("媒体") || n.Contains("社交") || n.Contains("音乐") ||
               n.Contains("视频") || n.Contains("聊天") || n.Contains("启动器");
    }

    private static int EstimateWidgetFileCount(WidgetConfig widget)
    {
        if (widget.Items is { Count: > 0 }) return widget.Items.Count;
        return 8;
    }

    private static async Task<AiDesktopLayoutResponseDto?> RequestAiSpatialDesignAsync(
        IReadOnlyList<WidgetConfig> visibleWidgets,
        DesktopOrganizationRect workArea,
        double dpiScale,
        DesktopOrganizationAiOptions aiOptions,
        string layoutStyle,
        string? customPrompt,
        IProgress<DesktopOrganizationAiStreamProgress>? progress,
        CancellationToken cancellationToken)
    {
        string endpoint = NormalizeChatCompletionsEndpoint(aiOptions.BaseUrl);
        var widgetSummaryLines = visibleWidgets.Select(w =>
        {
            int count = w.WidgetKind == WidgetKind.File ? EstimateWidgetFileCount(w) : 0;
            return $"- id=\"{w.Id}\", name=\"{w.Name}\", kind=\"{w.WidgetKind}\", item_count={count}";
        });

        string extraUserPromptBlock = string.IsNullOrWhiteSpace(customPrompt)
            ? "（用户未填写额外特殊指令，请充分发挥该空间架构风格的标志性美学特征）"
            : $"【🔥 用户额外定制设计要求（最高优先级，必须严格满足）】：\n{customPrompt}";

        string systemPrompt =
            "你是世界顶级的 Windows 桌面空间架构师与 UI 视觉总监。\n" +
            "你的任务是打破千篇一律的死板网格，为用户的桌面小组件（天气、待办、速记）和文件分类格子设计具有鲜明空间美学的 2D 桌面布局。\n" +
            "请为每个组件（或主容器）直接规划在屏幕上的 2D 归一化坐标与宽高比例：\n" +
            "- x_pct: 距离屏幕左边缘百分比 (0.01 ~ 0.84)\n" +
            "- y_pct: 距离屏幕上边缘百分比 (0.02 ~ 0.82)\n" +
            "- w_pct: 宽度占屏幕百分比 (0.15 ~ 0.42)\n" +
            "- h_pct: 高度占屏幕百分比 (0.16 ~ 0.52)\n" +
            "- is_collapsed: 是否折叠为 44px 高的顶部迷你悬浮胶囊 (true/false)\n" +
            "【6大标志性空间构图原型要求】：\n" +
            "1. SurroundStage（环幕剧院式）：组件分布在左边缘、右边缘、顶部或底部边缘，屏幕正中央 (x: 0.22~0.74, y: 0.18~0.76) 必须完全留空展示壁纸！\n" +
            "2. SplitFactions（左右双阵营对峙）：左半屏 (x: 0.01~0.40) 放置生产力/开发/效率工具，右半屏 (x: 0.58~0.98) 放置游戏/娱乐/影音，中央留出视觉走廊。\n" +
            "3. BottomDockGallery（底部工作坞+顶部胶囊）：将次要分类设为 is_collapsed=true 放在顶部 (y_pct=0.02)，核心高频面板集中在下半屏 (y_pct: 0.45~0.85)。\n" +
            "4. TabbedWorkspaces（四大超级多标签工作舱）：在 tab_groups 中将 2~3 个相关性高的文件格子合并为多标签容器，使桌面只保留 3~4 个大尺寸旗舰工作舱。\n" +
            "5. BentoAdaptiveGrid（杂志级 Bento 瀑布流）：全画幅错落矩阵，文件多的分类给大尺寸 (w_pct=0.26, h_pct=0.36)，文件少的给紧凑卡片。\n" +
            "6. StudioWings（极客双翼工作台）：左侧垂直停靠 Weather/Todo/QuickCapture，右侧排布高频分类矩阵。\n\n" +
            "请先用简体中文输出 2~3 句你的空间美学构思，然后输出严格合法的 JSON 对象：\n" +
            "{\n" +
            "  \"design_title\": \"富有设计感的方案名称\",\n" +
            "  \"design_philosophy\": \"一句话总结空间布局亮点与留白美学\",\n" +
            "  \"wallpaper_breathing_ratio\": \"45%\",\n" +
            "  \"placements\": [\n" +
            "    { \"widget_id\": \"...\", \"name\": \"...\", \"zone\": \"left_dock|right_matrix|top_capsule|bottom_dock\", \"size_tier\": \"hero|standard|compact\", \"priority\": 90, \"x_pct\": 0.02, \"y_pct\": 0.04, \"w_pct\": 0.18, \"h_pct\": 0.24, \"is_collapsed\": false }\n" +
            "  ],\n" +
            "  \"tab_groups\": [\n" +
            "    { \"group_title\": \"开发与效率中心\", \"member_names\": [\"格名1\", \"格名2\"], \"reason\": \"合并理由\" }\n" +
            "  ]\n" +
            "}";

        string userPrompt =
            $"屏幕分辨率：{workArea.Width}x{workArea.Height} (DPI缩放 {dpiScale:0.00})\n" +
            $"用户选择的布局架构风格：{layoutStyle} ({DesktopLayoutStyles.GetDisplayTitle(layoutStyle)})\n" +
            $"{extraUserPromptBlock}\n" +
            $"当前桌面上的小组件与文件格子（共 {visibleWidgets.Count} 个）：\n" +
            string.Join("\n", widgetSummaryLines);

        progress?.Report(new DesktopOrganizationAiStreamProgress
        {
            StageSummary = "🎨 AI 桌面空间建筑师正在构思全画幅 2D 构图...",
            ThinkingText = $"正在请求 {aiOptions.Model} 生成「{DesktopLayoutStyles.GetDisplayTitle(layoutStyle)}」预览方案..."
        });

        var payload = new
        {
            model = string.IsNullOrWhiteSpace(aiOptions.Model) ? "gemini-2.5-flash" : aiOptions.Model.Trim(),
            temperature = 0.4,
            stream = true,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        if (!string.IsNullOrWhiteSpace(aiOptions.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", aiOptions.ApiKey.Trim());
        }

        request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        using var response = await SharedHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var fullContent = new StringBuilder();
        var reasoningContent = new StringBuilder();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string data = line["data:".Length..].Trim();
            if (string.Equals(data, "[DONE]", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            try
            {
                using var doc = JsonDocument.Parse(data);
                if (doc.RootElement.TryGetProperty("choices", out var choices) &&
                    choices.GetArrayLength() > 0 &&
                    choices[0].TryGetProperty("delta", out var delta))
                {
                    if (delta.TryGetProperty("reasoning_content", out var rc) && rc.ValueKind == JsonValueKind.String)
                    {
                        reasoningContent.Append(rc.GetString());
                    }

                    if (delta.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String)
                    {
                        fullContent.Append(c.GetString());
                    }

                    progress?.Report(new DesktopOrganizationAiStreamProgress
                    {
                        StageSummary = "🎨 AI 正在计算 2D 空间坐标与留白比例...",
                        ThinkingText = reasoningContent.Length > 0 ? reasoningContent.ToString() : fullContent.ToString()
                    });
                }
            }
            catch
            {
            }
        }

        string rawText = fullContent.ToString();
        int firstBrace = rawText.IndexOf('{');
        int lastBrace = rawText.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            string jsonSlice = rawText[firstBrace..(lastBrace + 1)];
            return JsonSerializer.Deserialize<AiDesktopLayoutResponseDto>(jsonSlice, JsonOptions);
        }

        return null;
    }

    private static string NormalizeChatCompletionsEndpoint(string baseUrl)
    {
        string trimmed = (baseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        if (trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed + "/chat/completions";
        }

        return trimmed + "/v1/chat/completions";
    }
}

