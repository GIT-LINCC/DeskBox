using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeskBox.Models;

public static class DesktopOrganizationModes
{
    public const string Rule = "Rule";
    public const string Ai = "Ai";

    public static string Normalize(string? mode) =>
        string.Equals(mode, Ai, StringComparison.OrdinalIgnoreCase) ? Ai : Rule;
}

public static class DesktopOrganizationAiProviderIds
{
    public const string DeepSeek = "DeepSeek";
    public const string Gemini = "Gemini";
    public const string Qwen = "Qwen";
    public const string OpenAI = "OpenAI";
    public const string Zhipu = "Zhipu";
    public const string Moonshot = "Moonshot";
    public const string Ollama = "Ollama";
    public const string Custom = "Custom";
}

public sealed record DesktopOrganizationAiProviderPreset(
    string Id,
    string DisplayNameKey,
    string DefaultDisplayName,
    string DefaultBaseUrl,
    string DefaultModel,
    bool RequiresApiKey);

public static class DesktopOrganizationAiProviderPresets
{
    public static readonly IReadOnlyList<DesktopOrganizationAiProviderPreset> All =
    [
        new(
            DesktopOrganizationAiProviderIds.Gemini,
            "DesktopOrganization.Ai.Provider.Gemini",
            "Google Gemini",
            "https://generativelanguage.googleapis.com/v1beta/openai/",
            "gemini-2.5-flash",
            RequiresApiKey: true),
        new(
            DesktopOrganizationAiProviderIds.DeepSeek,
            "DesktopOrganization.Ai.Provider.DeepSeek",
            "DeepSeek",
            "https://api.deepseek.com/v1",
            "deepseek-chat",
            RequiresApiKey: true),
        new(
            DesktopOrganizationAiProviderIds.Qwen,
            "DesktopOrganization.Ai.Provider.Qwen",
            "Alibaba Qwen (DashScope)",
            "https://dashscope.aliyuncs.com/compatible-mode/v1",
            "qwen-plus",
            RequiresApiKey: true),
        new(
            DesktopOrganizationAiProviderIds.OpenAI,
            "DesktopOrganization.Ai.Provider.OpenAI",
            "OpenAI",
            "https://api.openai.com/v1",
            "gpt-4o-mini",
            RequiresApiKey: true),
        new(
            DesktopOrganizationAiProviderIds.Zhipu,
            "DesktopOrganization.Ai.Provider.Zhipu",
            "Zhipu GLM",
            "https://open.bigmodel.cn/api/paas/v4",
            "glm-4-flash",
            RequiresApiKey: true),
        new(
            DesktopOrganizationAiProviderIds.Moonshot,
            "DesktopOrganization.Ai.Provider.Moonshot",
            "Moonshot (Kimi)",
            "https://api.moonshot.cn/v1",
            "moonshot-v1-8k",
            RequiresApiKey: true),
        new(
            DesktopOrganizationAiProviderIds.Ollama,
            "DesktopOrganization.Ai.Provider.Ollama",
            "Ollama (Local)",
            "http://localhost:11434/v1",
            "qwen2.5:7b",
            RequiresApiKey: false),
        new(
            DesktopOrganizationAiProviderIds.Custom,
            "DesktopOrganization.Ai.Provider.Custom",
            "Custom (OpenAI Compatible)",
            "https://api.openai.com/v1",
            "gpt-4o-mini",
            RequiresApiKey: false)
    ];

    public static DesktopOrganizationAiProviderPreset GetById(string? providerId) =>
        All.FirstOrDefault(preset =>
            string.Equals(preset.Id, providerId, StringComparison.OrdinalIgnoreCase))
        ?? All[0];
}

public sealed class DesktopOrganizationAiOptions
{
    public string ProviderId { get; set; } = DesktopOrganizationAiProviderIds.Gemini;

    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/openai/";

    public string Model { get; set; } = "gemini-2.5-flash";

    public string? ApiKey { get; set; }

    public string CustomPrompt { get; set; } = string.Empty;

    /// <summary>
    /// Whether AI should inspect existing file widgets (titles and sample filenames)
    /// and route matching desktop items into them instead of creating duplicate widgets.
    /// </summary>
    public bool EnableSmartWidgetReuse { get; set; } = true;

    /// <summary>
    /// Whether AI-recommended extension/category routing rules should be automatically
    /// bound to newly created widgets upon execution.
    /// </summary>
    public bool AutoBindRoutingRules { get; set; } = true;

    /// <summary>
    /// Optional DeskBox advanced feature integration: whether AI should suggest merging
    /// related target widgets into a DeskBox Widget Group.
    /// </summary>
    public bool EnableWidgetGroupSuggestions { get; set; }

    /// <summary>
    /// Whether to perform deep inspection of desktop items (shortcut targets, executable
    /// version info, text snippets, folder child items) instead of relying solely on filenames.
    /// </summary>
    public bool EnableDeepInspection { get; set; } = true;

    /// <summary>
    /// Whether AI deep inspection should also perform online web searches (with local caching)
    /// to identify specialized third-party launchers, gaming platforms (e.g., CurseForge, Modrinth),
    /// developer utilities, and niche applications.
    /// </summary>
    public bool EnableWebSearch { get; set; } = true;

    /// <summary>
    /// How organized desktop files should be stored ("InPlaceDesktop", "DesktopSubfolders", or "ManagedExternal").
    /// </summary>
    public string StorageMode { get; set; } = DesktopOrganizationStorageModes.InPlaceDesktop;
}

public static class DesktopOrganizationStorageModes
{
    /// <summary>
    /// Fences-style in-place virtual desktop organization: files stay in C:\Users\...\Desktop
    /// (so Windows Explorer's "Desktop" shortcut is never emptied) and scattered native desktop icons are hidden.
    /// </summary>
    public const string InPlaceDesktop = "InPlaceDesktop";

    /// <summary>
    /// Creates category subfolders directly inside C:\Users\...\Desktop\<Category> so files remain inside Desktop.
    /// </summary>
    public const string DesktopSubfolders = "DesktopSubfolders";

    /// <summary>
    /// Moves files into the external DeskBox managed storage directory.
    /// </summary>
    public const string ManagedExternal = "ManagedExternal";

    public static string Normalize(string? mode)
    {
        if (string.Equals(mode, DesktopSubfolders, StringComparison.OrdinalIgnoreCase))
        {
            return DesktopSubfolders;
        }

        if (string.Equals(mode, ManagedExternal, StringComparison.OrdinalIgnoreCase))
        {
            return ManagedExternal;
        }

        return InPlaceDesktop;
    }
}

public sealed class DesktopOrganizationAiStreamProgress
{
    public string Stage { get; init; } = string.Empty;

    public string StageSummary { get; init; } = string.Empty;

    public string ThinkingText { get; init; } = string.Empty;

    public string StreamingResponsePreview { get; init; } = string.Empty;

    public List<string> InspectionLogs { get; init; } = [];

    public double ElapsedSeconds { get; init; }
}

public sealed class DesktopOrganizationAiSavedDraft
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Title { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public string Model { get; set; } = string.Empty;

    public string StorageMode { get; set; } = DesktopOrganizationStorageModes.InPlaceDesktop;

    public bool EnableDeepInspection { get; set; } = true;

    public string CustomPrompt { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string ThinkingTrace { get; set; } = string.Empty;

    public string RawPromptJson { get; set; } = string.Empty;

    public string RawResponseJson { get; set; } = string.Empty;

    public AiOrganizationSchemaResponse? SchemaSnapshot { get; set; }
}

public sealed class DesktopOrganizationAiGroupSuggestion
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string GroupTitle { get; init; } = string.Empty;

    public List<string> TargetBucketIds { get; init; } = [];

    public List<string> TargetWidgetIds { get; init; } = [];

    public List<string> TargetDisplayNames { get; init; } = [];

    public string Reason { get; init; } = string.Empty;

    public bool IsSelected { get; set; } = true;
}

// ─── OpenAI-Compatible Wire DTOs & Structured Output Schema (AOT-Safe) ───

internal sealed class AiChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<AiChatMessage> Messages { get; set; } = [];

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 0.2;

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }
}

internal sealed class AiChatMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("reasoning_content")]
    public string? ReasoningContent { get; set; }
}

internal sealed class AiChatCompletionResponse
{
    [JsonPropertyName("choices")]
    public List<AiChatChoice>? Choices { get; set; }

    [JsonPropertyName("error")]
    public AiApiErrorDetail? Error { get; set; }
}

internal sealed class AiChatChoice
{
    [JsonPropertyName("message")]
    public AiChatMessage? Message { get; set; }

    [JsonPropertyName("delta")]
    public AiChatStreamDelta? Delta { get; set; }
}

internal sealed class AiChatStreamDelta
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("reasoning_content")]
    public string? ReasoningContent { get; set; }

    [JsonPropertyName("reasoning")]
    public string? Reasoning { get; set; }
}

internal sealed class AiApiErrorDetail
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }
}

internal sealed class AiDesktopScanPromptPayload
{
    [JsonPropertyName("userPreferencePrompt")]
    public string? UserPreferencePrompt { get; set; }

    [JsonPropertyName("enableSmartWidgetReuse")]
    public bool EnableSmartWidgetReuse { get; set; }

    [JsonPropertyName("autoBindRoutingRules")]
    public bool AutoBindRoutingRules { get; set; }

    [JsonPropertyName("enableWidgetGroupSuggestions")]
    public bool EnableWidgetGroupSuggestions { get; set; }

    [JsonPropertyName("deepInspectionEnabled")]
    public bool DeepInspectionEnabled { get; set; }

    [JsonPropertyName("existingFileWidgets")]
    public List<AiExistingWidgetContextDto> ExistingFileWidgets { get; set; } = [];

    [JsonPropertyName("desktopItems")]
    public List<AiDesktopItemContextDto> DesktopItems { get; set; } = [];
}

internal sealed class AiExistingWidgetContextDto
{
    [JsonPropertyName("widgetId")]
    public string WidgetId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("sampleFiles")]
    public List<string> SampleFiles { get; set; } = [];

    [JsonPropertyName("routedExtensions")]
    public List<string> RoutedExtensions { get; set; } = [];
}

internal sealed class AiDesktopItemContextDto
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("ext")]
    public string Extension { get; set; } = string.Empty;

    [JsonPropertyName("baseCategory")]
    public string BaseCategory { get; set; } = string.Empty;

    [JsonPropertyName("subtype")]
    public string? Subtype { get; set; }

    [JsonPropertyName("isFolder")]
    public bool IsFolder { get; set; }

    [JsonPropertyName("sizeKb")]
    public long SizeKb { get; set; }

    [JsonPropertyName("modifiedDate")]
    public string ModifiedDate { get; set; } = string.Empty;

    [JsonPropertyName("deepClue")]
    public string? DeepClue { get; set; }
}

public sealed class AiOrganizationSchemaResponse
{
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("buckets")]
    public List<AiTargetBucketDto> Buckets { get; set; } = [];

    [JsonPropertyName("widgetGroups")]
    public List<AiWidgetGroupSuggestionDto> WidgetGroups { get; set; } = [];
}

public sealed class AiTargetBucketDto
{
    [JsonPropertyName("bucketName")]
    public string BucketName { get; set; } = string.Empty;

    [JsonPropertyName("existingWidgetId")]
    public string? ExistingWidgetId { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("itemIndices")]
    public List<int> ItemIndices { get; set; } = [];

    [JsonPropertyName("itemNames")]
    public List<string> ItemNames { get; set; } = [];

    [JsonPropertyName("recommendedCategoryIds")]
    public List<string> RecommendedCategoryIds { get; set; } = [];

    [JsonPropertyName("recommendedExtensions")]
    public List<string> RecommendedExtensions { get; set; } = [];
}

public sealed class AiWidgetGroupSuggestionDto
{
    [JsonPropertyName("groupTitle")]
    public string GroupTitle { get; set; } = string.Empty;

    [JsonPropertyName("bucketNames")]
    public List<string> BucketNames { get; set; } = [];

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    WriteIndented = false)]
[JsonSerializable(typeof(AiChatCompletionRequest))]
[JsonSerializable(typeof(AiChatCompletionResponse))]
[JsonSerializable(typeof(AiDesktopScanPromptPayload))]
[JsonSerializable(typeof(AiOrganizationSchemaResponse))]
[JsonSerializable(typeof(DesktopOrganizationAiSavedDraft))]
[JsonSerializable(typeof(List<DesktopOrganizationAiSavedDraft>))]
internal sealed partial class DesktopOrganizationAiJsonContext : JsonSerializerContext
{
}
