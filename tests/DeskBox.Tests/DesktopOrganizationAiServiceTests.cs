using DeskBox.Models;
using DeskBox.Services;
using Xunit;

namespace DeskBox.Tests;

public sealed class DesktopOrganizationAiServiceTests
{
    [Fact]
    public void ProviderPresets_IncludeMainstreamProvidersAndDefaultEndpoints()
    {
        Assert.Contains(DesktopOrganizationAiProviderPresets.All, p => p.Id == DesktopOrganizationAiProviderIds.DeepSeek);
        Assert.Contains(DesktopOrganizationAiProviderPresets.All, p => p.Id == DesktopOrganizationAiProviderIds.Gemini);
        Assert.Contains(DesktopOrganizationAiProviderPresets.All, p => p.Id == DesktopOrganizationAiProviderIds.Qwen);
        Assert.Contains(DesktopOrganizationAiProviderPresets.All, p => p.Id == DesktopOrganizationAiProviderIds.OpenAI);
        Assert.Contains(DesktopOrganizationAiProviderPresets.All, p => p.Id == DesktopOrganizationAiProviderIds.Zhipu);
        Assert.Contains(DesktopOrganizationAiProviderPresets.All, p => p.Id == DesktopOrganizationAiProviderIds.Moonshot);
        Assert.Contains(DesktopOrganizationAiProviderPresets.All, p => p.Id == DesktopOrganizationAiProviderIds.Ollama);
        Assert.Contains(DesktopOrganizationAiProviderPresets.All, p => p.Id == DesktopOrganizationAiProviderIds.Custom);

        string endpoint = DesktopOrganizationAiService.ResolveChatCompletionsEndpoint("https://api.deepseek.com/v1/");
        Assert.Equal("https://api.deepseek.com/v1/chat/completions", endpoint);

        string geminiEndpoint1 = DesktopOrganizationAiService.ResolveChatCompletionsEndpoint("https://generativelanguage.googleapis.com/v1beta/openai");
        Assert.Equal("https://generativelanguage.googleapis.com/v1beta/openai/chat/completions", geminiEndpoint1);

        string geminiEndpoint2 = DesktopOrganizationAiService.ResolveChatCompletionsEndpoint("https://generativelanguage.googleapis.com/v1beta/");
        Assert.Equal("https://generativelanguage.googleapis.com/v1beta/openai/chat/completions", geminiEndpoint2);

        string localGatewayEndpoint = DesktopOrganizationAiService.ResolveChatCompletionsEndpoint("http://127.0.0.1:11434");
        Assert.Equal("http://127.0.0.1:11434/v1/chat/completions", localGatewayEndpoint);
    }

    [Fact]
    public void ParseAiResponseJson_StripsThinkBlocksAndMarkdownCodeFences()
    {
        string raw = """
            <think>
            Analyzing the user's desktop items...
            </think>
            ```json
            {
              "summary": "Grouped by project and app usage",
              "buckets": [
                {
                  "bucketName": "财务报销项目",
                  "existingWidgetId": null,
                  "reason": "发票与报销表属于同一事项",
                  "itemIndices": [0, 1],
                  "recommendedCategoryIds": ["Documents", "Images"],
                  "recommendedExtensions": [".pdf", ".xlsx", ".png"]
                }
              ],
              "widgetGroups": []
            }
            ```
            """;

        AiOrganizationSchemaResponse parsed = DesktopOrganizationAiService.ParseAiResponseJson(raw);
        Assert.Equal("Grouped by project and app usage", parsed.Summary);
        Assert.Single(parsed.Buckets);
        Assert.Equal("财务报销项目", parsed.Buckets[0].BucketName);
        Assert.Equal([0, 1], parsed.Buckets[0].ItemIndices);
    }

    [Fact]
    public void BuildPlanFromAiResponse_ReusesExistingWidgets_InfersRules_AndGuaranteesZeroLoss()
    {
        var items = new List<DesktopOrganizationFileSnapshot>
        {
            new(@"C:\Users\Test\Desktop\invoice.pdf", "invoice.pdf", ".pdf", 1024, DateTime.UtcNow, DesktopOrganizationCategoryIds.Documents, DesktopOrganizationSubtypeIds.Pdf, DesktopOrganizationExclusionReason.None),
            new(@"C:\Users\Test\Desktop\receipt.png", "receipt.png", ".png", 2048, DateTime.UtcNow, DesktopOrganizationCategoryIds.Images, null, DesktopOrganizationExclusionReason.None),
            new(@"C:\Users\Test\Desktop\vscode.lnk", "vscode.lnk", ".lnk", 512, DateTime.UtcNow, DesktopOrganizationCategoryIds.Shortcuts, null, DesktopOrganizationExclusionReason.None),
            new(@"C:\Users\Test\Desktop\unassigned.zip", "unassigned.zip", ".zip", 4096, DateTime.UtcNow, DesktopOrganizationCategoryIds.Packages, null, DesktopOrganizationExclusionReason.None)
        };

        var scan = new DesktopOrganizationScanResult
        {
            DesktopPath = @"C:\Users\Test\Desktop",
            Items = items
        };

        var existingDevWidget = new WidgetConfig
        {
            Id = "widget-dev-tools",
            Name = "开发工具",
            WidgetKind = WidgetKind.File,
            MappedFolderPath = @"D:\DeskBox\开发工具"
        };

        var aiResponse = new AiOrganizationSchemaResponse
        {
            Summary = "按报销项目与现有开发工具盒子整理",
            Buckets =
            [
                new AiTargetBucketDto
                {
                    BucketName = "差旅报销资料",
                    ExistingWidgetId = null,
                    Reason = "同属差旅报销的发票与凭证截图",
                    ItemIndices = [0, 1],
                    RecommendedCategoryIds = [DesktopOrganizationCategoryIds.Documents],
                    RecommendedExtensions = [".pdf", ".png"]
                },
                new AiTargetBucketDto
                {
                    BucketName = "开发工具",
                    ExistingWidgetId = "widget-dev-tools",
                    Reason = "归入桌面已有的开发工具格子",
                    ItemIndices = [2]
                }
            ],
            WidgetGroups =
            [
                new AiWidgetGroupSuggestionDto
                {
                    GroupTitle = "办公与开发综合组",
                    BucketNames = ["差旅报销资料", "开发工具"],
                    Reason = "常用工作入口合并为一个标签组"
                }
            ]
        };

        var options = new DesktopOrganizationAiOptions
        {
            EnableSmartWidgetReuse = true,
            AutoBindRoutingRules = true,
            EnableWidgetGroupSuggestions = true
        };

        DesktopOrganizationPlan plan = DesktopOrganizationAiService.BuildPlanFromAiResponse(
            scan,
            @"D:\DeskBox",
            items,
            [],
            [existingDevWidget],
            options,
            aiResponse,
            id => id);

        Assert.True(plan.IsAiPlan);
        Assert.Equal(4, plan.EligibleItemCount); // All 4 items accounted for (including unassigned index 3)

        var newBucket = Assert.Single(plan.Targets, t => t.SuggestedDisplayName == "差旅报销资料");
        Assert.True(newBucket.CreatesWidget);
        Assert.True(newBucket.AutoBindRule);
        Assert.Contains(".pdf", newBucket.RecommendedExtensions);
        Assert.Contains(".png", newBucket.RecommendedExtensions);

        var reusedBucket = Assert.Single(plan.Targets, t => t.TargetWidgetId == "widget-dev-tools");
        Assert.False(reusedBucket.CreatesWidget);
        Assert.Single(reusedBucket.Items);

        Assert.Single(plan.AiGroupSuggestions);
        Assert.Equal("办公与开发综合组", plan.AiGroupSuggestions[0].GroupTitle);
        Assert.True(plan.AiGroupSuggestions[0].IsSelected);
    }
}
