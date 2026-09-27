using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DeskBox.Models;

namespace DeskBox.Services;

public sealed partial class DesktopOrganizationAiService
{
    private const int MaxSavedDrafts = 15;

    private static string GetDraftsFilePath()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DeskBox");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "ai-organization-drafts.json");
    }

    public List<DesktopOrganizationAiSavedDraft> LoadSavedDrafts()
    {
        try
        {
            string path = GetDraftsFilePath();
            if (!File.Exists(path))
            {
                return [];
            }

            string raw = File.ReadAllText(path, Encoding.UTF8);
            return JsonSerializer.Deserialize(
                raw,
                DesktopOrganizationAiJsonContext.Default.ListDesktopOrganizationAiSavedDraft) ?? [];
        }
        catch (Exception ex)
        {
            App.Log($"[DesktopOrganizationAi] Failed to load saved drafts: {ex.Message}");
            return [];
        }
    }

    public DesktopOrganizationAiSavedDraft SaveDraftFromPlan(
        DesktopOrganizationPlan plan,
        DesktopOrganizationAiOptions options,
        string? customTitle = null)
    {
        var drafts = LoadSavedDrafts();
        string modeBadge = plan.IsInPlaceDesktop
            ? "原位桌面"
            : string.Equals(plan.StorageMode, DesktopOrganizationStorageModes.DesktopSubfolders, StringComparison.OrdinalIgnoreCase)
                ? "桌面子目录"
                : "独立目录";
        string inspectBadge = options.EnableDeepInspection ? "深度探查" : "知识库";
        string defaultTitle = $"{DateTime.Now:HH:mm:ss} · {plan.Targets.Count}个分区 ({plan.EligibleItemCount}项 · {inspectBadge}/{modeBadge})";

        var draft = new DesktopOrganizationAiSavedDraft
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = string.IsNullOrWhiteSpace(customTitle) ? defaultTitle : customTitle.Trim(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Model = options.Model,
            StorageMode = plan.StorageMode,
            EnableDeepInspection = options.EnableDeepInspection,
            CustomPrompt = options.CustomPrompt,
            Summary = plan.AiSummary ?? string.Empty,
            ThinkingTrace = plan.AiThinkingTrace ?? string.Empty,
            RawPromptJson = plan.AiRawPromptJson ?? string.Empty,
            RawResponseJson = plan.AiRawResponseJson ?? string.Empty,
            SchemaSnapshot = plan.AiSchemaSnapshot
        };

        drafts.Insert(0, draft);
        if (drafts.Count > MaxSavedDrafts)
        {
            drafts.RemoveRange(MaxSavedDrafts, drafts.Count - MaxSavedDrafts);
        }

        SaveDraftsList(drafts);
        return draft;
    }

    public void DeleteSavedDraft(string draftId)
    {
        var drafts = LoadSavedDrafts();
        if (drafts.RemoveAll(d => string.Equals(d.Id, draftId, StringComparison.Ordinal)) > 0)
        {
            SaveDraftsList(drafts);
        }
    }

    private static void SaveDraftsList(List<DesktopOrganizationAiSavedDraft> drafts)
    {
        try
        {
            string path = GetDraftsFilePath();
            string json = JsonSerializer.Serialize(
                drafts,
                DesktopOrganizationAiJsonContext.Default.ListDesktopOrganizationAiSavedDraft);
            File.WriteAllText(path, json, new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            App.Log($"[DesktopOrganizationAi] Failed to save drafts list: {ex.Message}");
        }
    }

    private async Task<(string RawJsonContent, string ThinkingTrace)> SendChatCompletionStreamingAsync(
        string endpoint,
        string? apiKey,
        AiChatCompletionRequest payload,
        IReadOnlyList<string> inspectionLogs,
        IProgress<DesktopOrganizationAiStreamProgress>? progress,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        payload.Stream = true;
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
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"AI service returned HTTP {(int)response.StatusCode}: {errorBody}");
        }

        var contentBuilder = new StringBuilder();
        var reasoningBuilder = new StringBuilder();

        using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        string? firstLine = await reader.ReadLineAsync(cancellationToken);
        if (firstLine is not null && !firstLine.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            // Server returned non-streaming JSON directly
            string rest = await reader.ReadToEndAsync(cancellationToken);
            string fullBody = firstLine + "\n" + rest;
            AiChatCompletionResponse? nonStreamResp = JsonSerializer.Deserialize(
                fullBody,
                DesktopOrganizationAiJsonContext.Default.AiChatCompletionResponse);

            string msg = nonStreamResp?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
            string? rMsg = nonStreamResp?.Choices?.FirstOrDefault()?.Message?.ReasoningContent;
            if (!string.IsNullOrWhiteSpace(rMsg))
            {
                reasoningBuilder.Append(rMsg);
            }

            contentBuilder.Append(msg);
            string finalTrace = ComposeLiveThinkingText(
                inspectionLogs,
                reasoningBuilder.ToString(),
                contentBuilder.ToString(),
                payload.Model,
                stopwatch.Elapsed.TotalSeconds);
            progress?.Report(new DesktopOrganizationAiStreamProgress
            {
                Stage = "reconciling",
                StageSummary = "💭 推理完成，正在校验文件零丢失并构建预览卡片...",
                ThinkingText = finalTrace,
                StreamingResponsePreview = contentBuilder.ToString(),
                InspectionLogs = inspectionLogs.ToList(),
                ElapsedSeconds = stopwatch.Elapsed.TotalSeconds
            });
            return (contentBuilder.ToString(), finalTrace);
        }

        if (firstLine is not null)
        {
            ProcessSseLine(firstLine, reasoningBuilder, contentBuilder);
        }

        long lastReportMs = 0;
        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string? line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!ProcessSseLine(line, reasoningBuilder, contentBuilder))
            {
                break;
            }

            long nowMs = stopwatch.ElapsedMilliseconds;
            if (nowMs - lastReportMs >= 90)
            {
                lastReportMs = nowMs;
                string liveTrace = ComposeLiveThinkingText(
                    inspectionLogs,
                    reasoningBuilder.ToString(),
                    contentBuilder.ToString(),
                    payload.Model,
                    stopwatch.Elapsed.TotalSeconds);

                progress?.Report(new DesktopOrganizationAiStreamProgress
                {
                    Stage = "thinking",
                    StageSummary = $"💭 {payload.Model} 正在实时思考与规划分区 ({stopwatch.Elapsed.TotalSeconds:F1}s)...",
                    ThinkingText = liveTrace,
                    StreamingResponsePreview = contentBuilder.ToString(),
                    InspectionLogs = inspectionLogs.ToList(),
                    ElapsedSeconds = stopwatch.Elapsed.TotalSeconds
                });
            }
        }

        string completeThinking = ComposeLiveThinkingText(
            inspectionLogs,
            reasoningBuilder.ToString(),
            contentBuilder.ToString(),
            payload.Model,
            stopwatch.Elapsed.TotalSeconds);

        progress?.Report(new DesktopOrganizationAiStreamProgress
        {
            Stage = "reconciling",
            StageSummary = "💭 思考完毕，正在校验文件零丢失并生成桌面预览...",
            ThinkingText = completeThinking,
            StreamingResponsePreview = contentBuilder.ToString(),
            InspectionLogs = inspectionLogs.ToList(),
            ElapsedSeconds = stopwatch.Elapsed.TotalSeconds
        });

        return (contentBuilder.ToString(), completeThinking);
    }

    private static bool ProcessSseLine(
        string line,
        StringBuilder reasoningBuilder,
        StringBuilder contentBuilder)
    {
        string trimmed = line.Trim();
        if (!trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string data = trimmed[5..].Trim();
        if (string.Equals(data, "[DONE]", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            AiChatCompletionResponse? chunk = JsonSerializer.Deserialize(
                data,
                DesktopOrganizationAiJsonContext.Default.AiChatCompletionResponse);

            var choice = chunk?.Choices?.FirstOrDefault();
            if (choice?.Delta is { } delta)
            {
                string? r = delta.ReasoningContent ?? delta.Reasoning;
                if (!string.IsNullOrEmpty(r))
                {
                    reasoningBuilder.Append(r);
                }

                if (!string.IsNullOrEmpty(delta.Content))
                {
                    contentBuilder.Append(delta.Content);
                }
            }
            else if (choice?.Message is { } msg)
            {
                if (!string.IsNullOrEmpty(msg.ReasoningContent))
                {
                    reasoningBuilder.Append(msg.ReasoningContent);
                }

                if (!string.IsNullOrEmpty(msg.Content))
                {
                    contentBuilder.Append(msg.Content);
                }
            }
        }
        catch
        {
            // Ignore malformed SSE partial frame
        }

        return true;
    }

    private static string ComposeLiveThinkingText(
        IReadOnlyList<string> inspectionLogs,
        string rawReasoning,
        string streamingContent,
        string modelName,
        double elapsedSeconds)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"> 💭 思考引擎：{modelName} （已耗时 {elapsedSeconds:F1}s）");

        if (inspectionLogs.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("【阶段 1：本地桌面探查与特征提取】");
            foreach (string log in inspectionLogs.Take(12))
            {
                sb.AppendLine($"  • {log}");
            }

            if (inspectionLogs.Count > 12)
            {
                sb.AppendLine($"  • ... 共提取 {inspectionLogs.Count} 条深层文件与快捷方式线索");
            }
        }

        // Extract <think>...</think> if embedded in content
        string embeddedThink = ExtractEmbeddedThink(streamingContent);
        string combinedReasoning = string.Join(
            "\n",
            new[] { rawReasoning.Trim(), embeddedThink.Trim() }.Where(s => !string.IsNullOrWhiteSpace(s)));

        if (!string.IsNullOrWhiteSpace(combinedReasoning))
        {
            sb.AppendLine();
            sb.AppendLine("【阶段 2：大模型深度思考链 (Reasoning Trace)】");
            sb.AppendLine(combinedReasoning);
        }

        // Real-time extraction of emerging buckets and reasons from streaming JSON
        var emergingLines = ExtractEmergingBucketsFromPartialJson(streamingContent);
        if (emergingLines.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("【阶段 3：正在推演与构建的整理分区】");
            foreach (string item in emergingLines)
            {
                sb.AppendLine($"  ✨ {item}");
            }
        }
        else if (string.IsNullOrWhiteSpace(combinedReasoning))
        {
            sb.AppendLine();
            sb.AppendLine("【阶段 2：正在深度推理桌面语义结构...】");
            sb.AppendLine("  • 正在分析桌面文件的项目归属、软件用途与现有 DeskBox 分区匹配关系...");
        }

        return sb.ToString().Trim();
    }

    private static string ExtractEmbeddedThink(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        int start = content.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return string.Empty;
        }

        int bodyStart = start + 7;
        int end = content.IndexOf("</think>", bodyStart, StringComparison.OrdinalIgnoreCase);
        return end >= bodyStart
            ? content[bodyStart..end].Trim()
            : content[bodyStart..].Trim();
    }

    private static List<string> ExtractEmergingBucketsFromPartialJson(string partialContent)
    {
        var results = new List<string>();
        if (string.IsNullOrWhiteSpace(partialContent))
        {
            return results;
        }

        string? summary = TryExtractJsonStringField(partialContent, "summary", 0, out _);
        if (!string.IsNullOrWhiteSpace(summary))
        {
            results.Add($"总体策略：{summary}");
        }

        int searchPos = 0;
        while (searchPos < partialContent.Length)
        {
            string? bucketName = TryExtractJsonStringField(partialContent, "bucketName", searchPos, out int nextPos);
            if (bucketName is null || nextPos <= searchPos)
            {
                break;
            }

            string? reason = TryExtractJsonStringField(partialContent, "reason", nextPos, out _);
            if (!string.IsNullOrWhiteSpace(reason))
            {
                results.Add($"分区「{bucketName}」：{reason}");
            }
            else
            {
                results.Add($"分区「{bucketName}」：正在分配匹配文件...");
            }

            searchPos = nextPos;
        }

        return results;
    }

    private static string? TryExtractJsonStringField(
        string text,
        string propertyName,
        int startIndex,
        out int afterMatchIndex)
    {
        afterMatchIndex = startIndex;
        string needle = $"\"{propertyName}\"";
        int propIdx = text.IndexOf(needle, startIndex, StringComparison.OrdinalIgnoreCase);
        if (propIdx < 0)
        {
            return null;
        }

        int colonIdx = text.IndexOf(':', propIdx + needle.Length);
        if (colonIdx < 0)
        {
            return null;
        }

        int quoteStart = text.IndexOf('"', colonIdx + 1);
        if (quoteStart < 0)
        {
            return null;
        }

        var val = new StringBuilder();
        for (int i = quoteStart + 1; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length)
            {
                val.Append(text[i + 1]);
                i++;
                continue;
            }

            if (text[i] == '"')
            {
                afterMatchIndex = i + 1;
                return val.ToString();
            }

            val.Append(text[i]);
        }

        afterMatchIndex = text.Length;
        return val.Length > 0 ? val.ToString() + "..." : null;
    }
}
