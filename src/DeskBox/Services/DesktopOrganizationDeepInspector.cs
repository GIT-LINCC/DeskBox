using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using DeskBox.Models;

namespace DeskBox.Services;

/// <summary>
/// Deep metadata and content inspector for desktop items when AI Deep Inspection mode is enabled,
/// plus Fences-style Windows native desktop icon visibility management.
/// </summary>
public static class DesktopOrganizationDeepInspector
{
    private static readonly HashSet<string> TextSnippetExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".markdown", ".json", ".csv", ".tsv", ".xml", ".yaml", ".yml",
        ".ini", ".cfg", ".conf", ".log", ".py", ".js", ".ts", ".cs", ".cpp", ".h",
        ".java", ".go", ".rs", ".ps1", ".bat", ".cmd", ".sh", ".sql", ".html", ".htm", ".css"
    };

    public static string? InspectItemClue(DesktopOrganizationFileSnapshot item)
    {
        try
        {
            if (item.IsDirectory)
            {
                return InspectDirectoryClue(item.SourcePath);
            }

            string ext = item.Extension?.ToLowerInvariant() ?? string.Empty;
            if (ext == ".lnk")
            {
                return InspectShortcutClue(item.SourcePath);
            }

            if (ext == ".url")
            {
                return InspectUrlShortcutClue(item.SourcePath);
            }

            if (ext is ".exe" or ".msi")
            {
                return InspectExecutableClue(item.SourcePath);
            }

            if (TextSnippetExtensions.Contains(ext))
            {
                return InspectTextFileSnippet(item.SourcePath);
            }
        }
        catch
        {
            // Ignore inspection failures and fall back to filename knowledge.
        }

        return null;
    }

    private static readonly HttpClient WebSearchHttpClient = CreateWebSearchHttpClient();
    private static readonly object WebCacheLock = new();
    private static Dictionary<string, string>? _webKnowledgeCache;

    private static HttpClient CreateWebSearchHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3.5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
        return client;
    }

    private static readonly Dictionary<string, string> BuiltInSoftwareEncyclopedia = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CurseForge"] = "Minecraft(我的世界)/魔兽世界等游戏的第三方启动器、整合包与Mod模组平台 -> 归类：游戏与启动平台",
        ["Modrinth"] = "Minecraft(我的世界)开源第三方游戏启动器、整合包与Mod管理平台 -> 归类：游戏与启动平台",
        ["Modrinth App"] = "Minecraft(我的世界)开源第三方游戏启动器、整合包与Mod管理平台 -> 归类：游戏与启动平台",
        ["Migurinth"] = "Minecraft(我的世界)第三方整合包与模组启动器工具 -> 归类：游戏与启动平台",
        ["Ubisoft Connect"] = "育碧(Ubisoft)官方PC游戏启动器与游戏平台 -> 归类：游戏与启动平台",
        ["小黑盒"] = "Steam/PC游戏玩家社区、游戏库启动与战绩百科平台 -> 归类：游戏与启动平台",
        ["3A社区"] = "PC单机3A游戏启动与资源平台客户端 -> 归类：游戏与启动平台",
        ["东东电竞"] = "PC电竞游戏大厅与对战启动平台 -> 归类：游戏与启动平台",
        ["雷电模拟器"] = "安卓手游PC模拟器与多开游戏启动平台 -> 归类：游戏与启动平台",
        ["雷电多开器"] = "雷电安卓手游模拟器多开管理启动器 -> 归类：游戏与启动平台",
        ["KOOK"] = "游戏开黑语音与即时通讯社区软件(原开黑啦) -> 归类：社交通讯",
        ["Oopz"] = "轻量级低延迟游戏语音开黑与社交通讯软件 -> 归类：社交通讯",
        ["黑盒语音"] = "小黑盒旗下PC游戏语音开黑与社交聊天客户端 -> 归类：社交通讯",
        ["YY语音"] = "语音聊天、公会开黑与即时通讯社交软件 -> 归类：社交通讯",
        ["Discord"] = "全球主流游戏社群语音聊天与即时通讯社交平台 -> 归类：社交通讯",
        ["Telegram"] = "跨平台加密即时通讯与社群聊天软件 -> 归类：社交通讯",
        ["腾讯QQ"] = "腾讯即时通讯社交聊天客户端 -> 归类：社交通讯",
        ["企业微信"] = "企业办公沟通与即时通讯社交软件 -> 归类：社交通讯",
        ["微博Lite"] = "社交媒体资讯与即时互动客户端 -> 归类：社交通讯",
        ["网易云音乐"] = "在线音乐播放与歌单流媒体客户端 -> 归类：影音直播",
        ["腾讯视频"] = "在线影视剧集点播与视频播放客户端 -> 归类：影音直播",
        ["抖音"] = "短视频观看与直播互动娱乐客户端 -> 归类：影音直播",
        ["斗鱼直播"] = "游戏与娱乐弹幕直播观看客户端 -> 归类：影音直播",
        ["哔哩哔哩直播姬"] = "B站UP主视频推流与直播开播工具 -> 归类：影音直播",
        ["Google Chrome"] = "Google网页浏览器 -> 归类：网络与下载",
        ["Tor Browser"] = "隐私匿名网页浏览器 -> 归类：网络与下载",
        ["Clash"] = "网络代理与分流加速工具 -> 归类：网络与下载",
        ["UU远程"] = "网易低延迟远程桌面控制与连接工具 -> 归类：网络与下载",
        ["百度网盘"] = "云端网盘存储与大文件下载传输客户端 -> 归类：网络与下载",
        ["阿里云盘"] = "阿里云端网盘存储与高速下载客户端 -> 归类：网络与下载",
        ["迅雷"] = "多协议BT/磁力/HTTP高速网络下载器 -> 归类：网络与下载",
        ["Gopeed"] = "开源跨平台高速多协议网络下载器(支持HTTP/BT/磁力) -> 归类：网络与下载",
        ["Aria2"] = "轻量级多协议命令行/图形化高速网络下载工具 -> 归类：网络与下载",
        ["AList"] = "支持多存储源挂载的网盘文件列表与WebDAV传输服务 -> 归类：网络与下载",
        ["IPFS Desktop"] = "星际文件系统(IPFS)分布式节点与网络存储客户端 -> 归类：网络与下载",
        ["OCS Desktop"] = "大学生网课在线学习与题库辅助桌面客户端 -> 归类：学业与办公",
        ["风灵月影修改器"] = "PC单机游戏数值修改器合集平台 -> 归类：游戏修改与加速",
        ["Gloss Mod Manager"] = "3DM开源PC单机游戏Mod模组安装管理器 -> 归类：游戏修改与加速",
        ["Hearthstone Deck Tracker"] = "炉石传说记牌器与对战数据分析插件 -> 归类：游戏修改与加速",
        ["HearthArena"] = "炉石传说竞技场选牌评分辅助插件 -> 归类：游戏修改与加速",
        ["Firestone"] = "炉石传说酒馆战棋与天梯全功能记牌辅助插件 -> 归类：游戏修改与加速",
        ["MetaTFT"] = "云顶之弈阵容胜率统计与对局辅助插件 -> 归类：游戏修改与加速",
        ["GreenLumaPro"] = "Steam游戏家庭共享与DLC入库解锁工具 -> 归类：游戏修改与加速",
        ["SteamTools"] = "Steam游戏入库清单拉取与解锁辅助工具 -> 归类：游戏修改与加速",
        ["游戏加加"] = "游戏内硬件帧率监控、画质优化与截图工具 -> 归类：游戏修改与加速",
        ["帧率救星"] = "PC游戏帧数优化与补帧流畅度辅助工具 -> 归类：游戏修改与加速",
        ["Antigravity"] = "Google DeepMind Agentic AI编程IDE与智能开发助手 -> 归类：AI与编程开发",
        ["Cursor"] = "AI原生代码编辑器与智能编程IDE -> 归类：AI与编程开发",
        ["Windsurf"] = "Codeium旗下AI Agentic代码编辑器与开发IDE -> 归类：AI与编程开发",
        ["Trae"] = "字节跳动AI原生集成开发IDE -> 归类：AI与编程开发",
        ["Kiro"] = "AI规范驱动(Spec-driven)智能开发IDE -> 归类：AI与编程开发",
        ["Claude"] = "Anthropic Claude AI大模型桌面客户端与编程助手 -> 归类：AI与编程开发",
        ["Codex"] = "AI代码生成、模型路由与开发代理管理工具 -> 归类：AI与编程开发",
        ["CC Switch"] = "Claude Code / AI编程模型配置与API快速切换工具 -> 归类：AI与编程开发"
    };

    public static async Task<string?> LookupWebKnowledgeAsync(
        DesktopOrganizationFileSnapshot item,
        string? localDeepClue,
        CancellationToken cancellationToken = default)
    {
        string rawName = Path.GetFileNameWithoutExtension(item.Name)
            .Replace(" - 快捷方式", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" - Shortcut", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        if (string.IsNullOrWhiteSpace(rawName) || rawName.Length < 2)
        {
            return null;
        }

        // 1. Check built-in high-precision software & game platform encyclopedia
        foreach (var kvp in BuiltInSoftwareEncyclopedia)
        {
            if (rawName.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(localDeepClue) && localDeepClue.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase)))
            {
                return $"🌐 联网百科: {kvp.Value}";
            }
        }

        // Only query online search for shortcuts, executables, URL links, or specialized software folders
        string ext = item.Extension?.ToLowerInvariant() ?? string.Empty;
        bool isCandidateForWebSearch = ext is ".lnk" or ".exe" or ".url" or ".msi" ||
                                       (item.IsDirectory && !rawName.Contains("新建", StringComparison.Ordinal) && !rawName.Contains("副本", StringComparison.Ordinal));
        if (!isCandidateForWebSearch)
        {
            return null;
        }

        var cache = LoadWebKnowledgeCache();
        lock (WebCacheLock)
        {
            if (cache.TryGetValue(rawName, out string? cached) && !string.IsNullOrWhiteSpace(cached))
            {
                return $"🌐 联网百科: {cached}";
            }
        }

        // 2. Perform live Bing web search snippet extraction
        try
        {
            string query = $"{rawName} 是什么软件 游戏 用途";
            string url = $"https://cn.bing.com/search?q={Uri.EscapeDataString(query)}&ensearch=0";
            using var response = await WebSearchHttpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                string html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                string? snippet = ExtractCleanSnippetFromSearchHtml(html, rawName);
                if (!string.IsNullOrWhiteSpace(snippet))
                {
                    lock (WebCacheLock)
                    {
                        cache[rawName] = snippet;
                        SaveWebKnowledgeCache(cache);
                    }

                    return $"🌐 联网搜索: {snippet}";
                }
            }
        }
        catch
        {
            // Ignore network timeout and fall back gracefully
        }

        return null;
    }

    private static string? ExtractCleanSnippetFromSearchHtml(string html, string keyword)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        // Strip script/style and extract text from first b_caption / b_algo block
        int algoIndex = html.IndexOf("class=\"b_algo\"", StringComparison.OrdinalIgnoreCase);
        if (algoIndex < 0)
        {
            algoIndex = html.IndexOf("class=\"b_caption\"", StringComparison.OrdinalIgnoreCase);
        }

        if (algoIndex < 0)
        {
            return null;
        }

        string window = html.Substring(algoIndex, Math.Min(2400, html.Length - algoIndex));
        var plain = new StringBuilder();
        bool inTag = false;
        foreach (char ch in window)
        {
            if (ch == '<') { inTag = true; continue; }
            if (ch == '>') { inTag = false; plain.Append(' '); continue; }
            if (!inTag && !char.IsControl(ch))
            {
                plain.Append(ch);
            }
        }

        string cleaned = System.Net.WebUtility.HtmlDecode(
            string.Join(" ", plain.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)));
        if (cleaned.Length > 110)
        {
            cleaned = cleaned[..110] + "...";
        }

        return cleaned.Length >= 10 ? cleaned : null;
    }

    private static Dictionary<string, string> LoadWebKnowledgeCache()
    {
        lock (WebCacheLock)
        {
            if (_webKnowledgeCache is not null)
            {
                return _webKnowledgeCache;
            }

            _webKnowledgeCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DeskBox",
                    "ai-web-search-cache.json");
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path, Encoding.UTF8);
                    var parsed = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (parsed is not null)
                    {
                        foreach (var kvp in parsed)
                        {
                            _webKnowledgeCache[kvp.Key] = kvp.Value;
                        }
                    }
                }
            }
            catch
            {
            }

            return _webKnowledgeCache;
        }
    }

    private static void SaveWebKnowledgeCache(Dictionary<string, string> cache)
    {
        try
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DeskBox");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "ai-web-search-cache.json");
            string json = System.Text.Json.JsonSerializer.Serialize(cache);
            File.WriteAllText(path, json, new UTF8Encoding(false));
        }
        catch
        {
        }
    }

    private static string? InspectDirectoryClue(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return null;
        }

        var entries = Directory.EnumerateFileSystemEntries(directoryPath)
            .Take(12)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        if (entries.Count == 0)
        {
            return "Empty folder";
        }

        var topExtensions = entries
            .Select(name => Path.GetExtension(name!))
            .Where(ext => !string.IsNullOrWhiteSpace(ext))
            .GroupBy(ext => ext!.ToLowerInvariant())
            .OrderByDescending(g => g.Count())
            .Take(4)
            .Select(g => $"{g.Key}({g.Count()})")
            .ToList();

        string sampleList = string.Join(", ", entries.Take(8));
        string extInfo = topExtensions.Count > 0 ? $" [exts: {string.Join(" ", topExtensions)}]" : string.Empty;
        return $"Folder contents: {sampleList}{extInfo}";
    }

    private static string? InspectExecutableClue(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        FileVersionInfo info = FileVersionInfo.GetVersionInfo(filePath);
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(info.ProductName))
        {
            parts.Add($"Product: {info.ProductName.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(info.FileDescription) &&
            !string.Equals(info.FileDescription.Trim(), info.ProductName?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            parts.Add($"Desc: {info.FileDescription.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(info.CompanyName))
        {
            parts.Add($"Publisher: {info.CompanyName.Trim()}");
        }

        return parts.Count > 0 ? string.Join(" | ", parts) : null;
    }

    private static string? InspectUrlShortcutClue(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        foreach (string line in File.ReadLines(filePath).Take(25))
        {
            if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
            {
                string url = line[4..].Trim();
                return url.Length > 140 ? $"URL: {url[..140]}..." : $"URL: {url}";
            }
        }

        return null;
    }

    private static string? InspectShortcutClue(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        // Fast binary string extraction from Windows Shell Link (.lnk) without COM thread affinity issues
        byte[] bytes = File.ReadAllBytes(filePath);
        var extractedPaths = new List<string>();

        // Scan ASCII & UTF-16LE sequences for paths like C:\... or \\...
        string ascii = Encoding.ASCII.GetString(bytes);
        ExtractDrivePaths(ascii, extractedPaths);

        string unicode = Encoding.Unicode.GetString(bytes);
        ExtractDrivePaths(unicode, extractedPaths);

        string? bestTarget = extractedPaths
            .Where(p => !p.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? 2 : 1)
            .ThenByDescending(p => p.Length)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(bestTarget))
        {
            if (File.Exists(bestTarget) && bestTarget.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                string? exeClue = InspectExecutableClue(bestTarget);
                if (!string.IsNullOrWhiteSpace(exeClue))
                {
                    return $"Shortcut -> {bestTarget} ({exeClue})";
                }
            }

            return $"Shortcut -> {bestTarget}";
        }

        return null;
    }

    private static void ExtractDrivePaths(string raw, List<string> results)
    {
        for (int i = 0; i < raw.Length - 4; i++)
        {
            char c = raw[i];
            if (char.IsLetter(c) && raw[i + 1] == ':' && raw[i + 2] == '\\')
            {
                int end = i + 3;
                while (end < raw.Length &&
                       raw[end] >= 32 &&
                       raw[end] != '"' &&
                       raw[end] != '\'' &&
                       raw[end] != '<' &&
                       raw[end] != '>' &&
                       raw[end] != '|' &&
                       raw[end] != '\0' &&
                       (end - i) < 220)
                {
                    end++;
                }

                string candidate = raw[i..end].Trim();
                if (candidate.Length > 5 && candidate.Contains('\\'))
                {
                    results.Add(candidate);
                }

                i = end;
            }
        }
    }

    private static string? InspectTextFileSnippet(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists || fileInfo.Length == 0)
        {
            return null;
        }

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        byte[] buffer = new byte[Math.Min(1024, (int)fileInfo.Length)];
        int read = stream.Read(buffer, 0, buffer.Length);
        if (read <= 0)
        {
            return null;
        }

        string text = Encoding.UTF8.GetString(buffer, 0, read);
        var cleanBuilder = new StringBuilder();
        foreach (char ch in text)
        {
            if (char.IsControl(ch) && ch is not ('\r' or '\n' or '\t'))
            {
                continue;
            }

            cleanBuilder.Append(ch is '\r' or '\n' or '\t' ? ' ' : ch);
            if (cleanBuilder.Length >= 220)
            {
                break;
            }
        }

        string snippet = string.Join(" ", cleanBuilder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(snippet) ? null : $"Content snippet: \"{snippet}\"";
    }
}

/// <summary>
/// Controls Windows native desktop icon layer (<c>SysListView32</c> inside <c>SHELLDLL_DefView</c>)
/// so Fences-style in-place organization can hide scattered native icons while keeping files in <c>Desktop</c>.
/// </summary>
public static class DesktopNativeIconVisibilityHelper
{
    private const int SW_HIDE = 0;
    private const int SW_SHOWNA = 8;

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string? lpszClass, string? lpszWindow);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    public static bool AreNativeDesktopIconsVisible()
    {
        IntPtr listView = FindDesktopListViewHandle();
        return listView == IntPtr.Zero || IsWindowVisible(listView);
    }

    public static bool SetNativeDesktopIconsVisible(bool visible)
    {
        IntPtr listView = FindDesktopListViewHandle();
        if (listView == IntPtr.Zero)
        {
            return false;
        }

        ShowWindow(listView, visible ? SW_SHOWNA : SW_HIDE);
        return true;
    }

    public static bool ToggleNativeDesktopIcons()
    {
        bool currentlyVisible = AreNativeDesktopIconsVisible();
        SetNativeDesktopIconsVisible(!currentlyVisible);
        return !currentlyVisible;
    }

    private static IntPtr FindDesktopListViewHandle()
    {
        IntPtr progman = FindWindow("Progman", null);
        IntPtr defView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (defView == IntPtr.Zero)
        {
            EnumWindows((hWnd, _) =>
            {
                IntPtr candidate = FindWindowEx(hWnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (candidate != IntPtr.Zero)
                {
                    defView = candidate;
                    return false;
                }

                return true;
            }, IntPtr.Zero);
        }

        if (defView == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        return FindWindowEx(defView, IntPtr.Zero, "SysListView32", null);
    }
}
