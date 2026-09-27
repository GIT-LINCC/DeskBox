namespace DeskBox.Models;

/// <summary>
/// Desktop organization rules, auto-organization baseline, and the recent-organization history (local-domain data, still stored in settings.json).
/// </summary>
public sealed class DesktopOrganizationSettingsSlice
{
    /// <summary>
    /// Recent organization history used for undo and quick review.
    /// </summary>
    public List<OrganizationHistoryEntry> RecentOrganizationHistory { get; set; } = [];

    /// <summary>
    /// Stable per-widget routing rules used by desktop one-click and automatic
    /// organization. Rules target widget IDs and therefore survive renames and
    /// group membership changes.
    /// </summary>
    public List<DesktopOrganizationRule> DesktopOrganizationRules { get; set; } = [];

    /// <summary>
    /// Whether newly created, stable desktop files may be routed automatically.
    /// This is deliberately opt-in.
    /// </summary>
    public bool DesktopAutoOrganizationEnabled { get; set; }

    /// <summary>
    /// Baseline created when automatic organization is enabled. Existing
    /// desktop content is never processed merely by enabling the feature.
    /// </summary>
    public DateTimeOffset? DesktopAutoOrganizationBaselineUtc { get; set; }

    /// <summary>
    /// Active desktop organization strategy ("Rule" or "Ai").
    /// </summary>
    public string DesktopOrganizationMode { get; set; } = DesktopOrganizationModes.Ai;

    /// <summary>
    /// Selected AI provider preset ID (e.g. Gemini, DeepSeek, Qwen, OpenAI, Zhipu, Moonshot, Ollama, Custom).
    /// </summary>
    public string DesktopOrganizationAiProviderId { get; set; } = DesktopOrganizationAiProviderIds.Gemini;

    /// <summary>
    /// OpenAI-compatible API endpoint base URL.
    /// </summary>
    public string DesktopOrganizationAiBaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/openai/";

    /// <summary>
    /// Model identifier for AI desktop organization.
    /// </summary>
    public string DesktopOrganizationAiModel { get; set; } = "gemini-2.5-flash";

    /// <summary>
    /// User-supplied natural language preference prompt for AI organization.
    /// </summary>
    public string DesktopOrganizationAiCustomPrompt { get; set; } = string.Empty;

    /// <summary>
    /// Whether AI should inspect existing file widgets and route related desktop files into them.
    /// </summary>
    public bool DesktopOrganizationAiEnableSmartReuse { get; set; } = true;

    /// <summary>
    /// Whether AI-recommended extension/category routing rules should be bound to new widgets.
    /// </summary>
    public bool DesktopOrganizationAiAutoBindRules { get; set; } = true;

    /// <summary>
    /// Whether AI should suggest merging related widgets into a DeskBox Widget Group (opt-in).
    /// </summary>
    public bool DesktopOrganizationAiEnableWidgetGroups { get; set; }

    /// <summary>
    /// Whether AI should deeply inspect shortcut targets, executable version info, file snippets, and folder contents.
    /// </summary>
    [System.Text.Json.Serialization.JsonInclude]
    internal bool DesktopOrganizationAiEnableDeepInspection { get; set; } = true;

    /// <summary>
    /// Whether AI should perform online web search enrichment for unknown/specialized desktop software and games.
    /// </summary>
    [System.Text.Json.Serialization.JsonInclude]
    internal bool DesktopOrganizationAiEnableWebSearch { get; set; } = true;

    /// <summary>
    /// Storage mode for desktop organization ("InPlaceDesktop", "DesktopSubfolders", or "ManagedExternal").
    /// </summary>
    [System.Text.Json.Serialization.JsonInclude]
    internal string DesktopOrganizationStorageMode { get; set; } = DesktopOrganizationStorageModes.InPlaceDesktop;

    /// <summary>
    /// Saved AI organization plan drafts for comparison, fallback, and debugging.
    /// </summary>
    [System.Text.Json.Serialization.JsonInclude]
    internal List<DesktopOrganizationAiSavedDraft> SavedAiDrafts { get; set; } = [];
}
