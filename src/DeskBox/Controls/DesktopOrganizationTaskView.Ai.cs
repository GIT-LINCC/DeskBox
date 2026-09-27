using System.Text;
using DeskBox.Models;
using DeskBox.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace DeskBox.Controls;

public sealed partial class DesktopOrganizationTaskView
{
    private bool _initializingAiControls;
    private bool _isAiMode;
    private bool _suppressDraftComboSelection;
    private List<DesktopOrganizationAiSavedDraft> _savedDrafts = [];

    private void InitializeAiControls()
    {
        _initializingAiControls = true;
        try
        {
            var slice = App.Current.SettingsService.Settings.DesktopOrganization;
            if (DesktopOrganizationAiService.TryApplyLocalProfileOverrides(slice))
            {
                _ = App.Current.SettingsService.SaveAsync();
            }

            _isAiMode = string.Equals(
                DesktopOrganizationModes.Normalize(slice.DesktopOrganizationMode),
                DesktopOrganizationModes.Ai,
                StringComparison.Ordinal);

            RuleModeToggleButton.IsChecked = !_isAiMode;
            AiModeToggleButton.IsChecked = _isAiMode;
            AiControlPanel.Visibility = _isAiMode ? Visibility.Visible : Visibility.Collapsed;

            AiProviderComboBox.Items.Clear();
            int selectedIndex = 0;
            for (int i = 0; i < DesktopOrganizationAiProviderPresets.All.Count; i++)
            {
                var preset = DesktopOrganizationAiProviderPresets.All[i];
                string localizedName = T(preset.DisplayNameKey);
                if (string.Equals(localizedName, preset.DisplayNameKey, StringComparison.Ordinal))
                {
                    localizedName = preset.DefaultDisplayName;
                }

                AiProviderComboBox.Items.Add(new ComboBoxItem
                {
                    Content = localizedName,
                    Tag = preset.Id
                });

                if (string.Equals(preset.Id, slice.DesktopOrganizationAiProviderId, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i;
                }
            }

            AiProviderComboBox.SelectedIndex = selectedIndex;
            AiBaseUrlTextBox.Text = slice.DesktopOrganizationAiBaseUrl;
            AiModelTextBox.Text = slice.DesktopOrganizationAiModel;
            AiCustomPromptTextBox.Text = slice.DesktopOrganizationAiCustomPrompt;
            AiDeepInspectionCheckBox.IsChecked = slice.DesktopOrganizationAiEnableDeepInspection;
            AiWebSearchCheckBox.IsChecked = slice.DesktopOrganizationAiEnableWebSearch;
            AiSmartReuseCheckBox.IsChecked = slice.DesktopOrganizationAiEnableSmartReuse;
            AiAutoBindRulesCheckBox.IsChecked = slice.DesktopOrganizationAiAutoBindRules;
            AiEnableWidgetGroupsCheckBox.IsChecked = slice.DesktopOrganizationAiEnableWidgetGroups;

            PopulateStorageModeComboBox(slice.DesktopOrganizationStorageMode);
            PopulateLayoutStyleComboBox(Coordinator.PreferredLayoutStyle);
            UpdateNativeDesktopIconsButtonText();
            ReloadSavedDraftsComboBox();
            UpdateAiProviderStatusBadge();
            _ = LoadStoredApiKeyAsync(slice.DesktopOrganizationAiProviderId);
        }
        finally
        {
            _initializingAiControls = false;
        }
    }

    private void PopulateLayoutStyleComboBox(string? currentStyle)
    {
        string normalized = DesktopLayoutStyles.Normalize(currentStyle);
        AiLayoutStyleComboBox.Items.Clear();

        var styles = new (string Id, string Label)[]
        {
            (DesktopLayoutStyles.StudioWings, "✨ 极客双翼 · 中央壁纸留白 (推荐：左效率列 + 中央留白 + 主卡与双标签组)"),
            (DesktopLayoutStyles.BentoAdaptiveGrid, "🍱 Bento 便当盒自适应矩阵 (全分类独立展开 · 按文件数动态卡片高度)"),
            (DesktopLayoutStyles.TabbedWorkspaces, "🏛️ 四大领域顶部多标签工作舱 (极致清爽 · 聚合成 4 个平铺 Tabs 大舱)"),
            (DesktopLayoutStyles.TopDockAndSidebar, "📌 顶栏画廊 + 右侧主看板 (顶部紧凑栏 + 左右主次分明)")
        };

        int selectedIdx = 0;
        for (int i = 0; i < styles.Length; i++)
        {
            AiLayoutStyleComboBox.Items.Add(new ComboBoxItem
            {
                Content = styles[i].Label,
                Tag = styles[i].Id
            });
            if (string.Equals(styles[i].Id, normalized, StringComparison.OrdinalIgnoreCase))
            {
                selectedIdx = i;
            }
        }

        AiLayoutStyleComboBox.SelectedIndex = selectedIdx;
        ToolTipService.SetToolTip(
            AiLayoutStyleComboBox,
            "选择桌面格子的空间构图与美学排版流派：\n" +
            "• 极客双翼 · 中央壁纸留白：左侧对齐天气/待办/随记，中央留出 38% 壁纸视觉中心，右侧保留高频主卡并将姊妹分类合并为顶部双标签页。\n" +
            "• Bento 便当盒自适应矩阵：所有分类保持独立全展开，根据文件数量动态计算卡片高度（矮卡/标准卡/高卡），彻底消除死板的 6×2 等高矩阵与重叠。\n" +
            "• 四大领域顶部多标签工作舱：将 10+ 个分类聚合成研发、游戏、互联、生产力 4 个顶部平铺标签栏大工作舱，释放 45%+ 桌面壁纸空间。\n" +
            "• 顶栏画廊 + 右侧主看板：顶部横向排列紧凑分类，左右双翼排布核心分类。");
        ToolTipService.SetToolTip(
            AiDesignLiveDesktopButton,
            "无需重新扫描或移动文件！直接由 AI 大模型分析当前桌面上已有的全部小组件（天气/待办/随记 + 所有分类格子），实时推演空间构图并一键重排桌面窗口。");
        ToolTipService.SetToolTip(
            AiQuickApplyLayoutButton,
            "0.2 秒极速切换：使用内置 Bento 美学几何求解器立即将当前桌面窗口重排为所选布局流派，支持在 4 种流派间随时自由切换对比！");
    }

    private string GetSelectedLayoutStyle()
    {
        if (AiLayoutStyleComboBox.SelectedItem is ComboBoxItem { Tag: string styleId })
        {
            return DesktopLayoutStyles.Normalize(styleId);
        }

        return DesktopLayoutStyles.StudioWings;
    }

    private void AiLayoutStyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializingAiControls)
        {
            return;
        }

        Coordinator.PreferredLayoutStyle = GetSelectedLayoutStyle();
    }

    private void AiDesignLiveDesktopButton_Click(object sender, RoutedEventArgs e)
    {
        App.Current.ShowDesktopLayoutDesignerWindow();
    }

    private void AiQuickApplyLayoutButton_Click(object sender, RoutedEventArgs e)
    {
        App.Current.ShowDesktopLayoutDesignerWindow();
    }

    private void PopulateStorageModeComboBox(string? currentMode)
    {
        string normalized = DesktopOrganizationStorageModes.Normalize(currentMode);
        AiStorageModeComboBox.Items.Clear();

        var modes = new (string Id, string Label)[]
        {
            (DesktopOrganizationStorageModes.InPlaceDesktop, "🟢 原位桌面虚拟收纳 (类 Fences，不搬空桌面)"),
            (DesktopOrganizationStorageModes.DesktopSubfolders, "🔵 桌面子文件夹归档 (留在系统 Desktop 内)"),
            (DesktopOrganizationStorageModes.ManagedExternal, "⚪ 独立外部仓库目录 (移入 DeskBox 文件夹)")
        };

        int selectedIdx = 0;
        for (int i = 0; i < modes.Length; i++)
        {
            AiStorageModeComboBox.Items.Add(new ComboBoxItem
            {
                Content = modes[i].Label,
                Tag = modes[i].Id
            });
            if (string.Equals(modes[i].Id, normalized, StringComparison.OrdinalIgnoreCase))
            {
                selectedIdx = i;
            }
        }

        AiStorageModeComboBox.SelectedIndex = selectedIdx;
        ToolTipService.SetToolTip(
            AiStorageModeComboBox,
            "选择整理后桌面文件的存放方式：\n" +
            "• 原位桌面虚拟收纳（推荐）：文件 100% 留在系统 Desktop 原位（资源管理器左侧点「桌面」绝不变空！），仅隐藏原生散乱图标并用 DeskBox 分区盒子接管展示。\n" +
            "• 桌面子文件夹归档：直接在系统 Desktop 下创建分类文件夹，资源管理器点「桌面」即可看到各分类文件夹。\n" +
            "• 独立外部仓库目录：将桌面文件移动到外部 DeskBox 文件夹中。");
    }

    private void UpdateNativeDesktopIconsButtonText()
    {
        bool visible = DesktopNativeIconVisibilityHelper.AreNativeDesktopIconsVisible();
        AiToggleNativeDesktopIconsButton.Content = visible
            ? "👁️ 隐藏散乱图标"
            : "👁️ 恢复桌面图标";
        ToolTipService.SetToolTip(
            AiToggleNativeDesktopIconsButton,
            "类似 Fences 双击桌面：一键隐藏或恢复显示 Windows 原生桌面图标层（文件仍在系统 Desktop 文件夹原位，不会丢失或移动）。");
    }

    private void ReloadSavedDraftsComboBox(string? selectDraftId = null)
    {
        _suppressDraftComboSelection = true;
        try
        {
            _savedDrafts = Coordinator.AiService.LoadSavedDrafts();
            AiSavedDraftsComboBox.Items.Clear();

            if (_savedDrafts.Count == 0)
            {
                AiSavedDraftsComboBox.PlaceholderText = "暂无备选方案（生成后自动保存或点击右侧「💾 暂存」）";
                AiSavedDraftsComboBox.IsEnabled = false;
                AiDeleteDraftButton.IsEnabled = false;
                return;
            }

            AiSavedDraftsComboBox.IsEnabled = true;
            AiSavedDraftsComboBox.PlaceholderText = $"选择备选方案（共 {_savedDrafts.Count} 个历史快照，点击秒级切换预览）";

            int selectedIdx = -1;
            for (int i = 0; i < _savedDrafts.Count; i++)
            {
                var draft = _savedDrafts[i];
                AiSavedDraftsComboBox.Items.Add(new ComboBoxItem
                {
                    Content = draft.Title,
                    Tag = draft.Id
                });
                if (!string.IsNullOrWhiteSpace(selectDraftId) &&
                    string.Equals(draft.Id, selectDraftId, StringComparison.Ordinal))
                {
                    selectedIdx = i;
                }
            }

            if (selectedIdx >= 0)
            {
                AiSavedDraftsComboBox.SelectedIndex = selectedIdx;
            }

            AiDeleteDraftButton.IsEnabled = AiSavedDraftsComboBox.SelectedIndex >= 0;
        }
        finally
        {
            _suppressDraftComboSelection = false;
        }
    }

    private void ApplyAiLocalization()
    {
        RuleModeToggleButton.Content = T("DesktopOrganization.Mode.Rule");
        AiModeToggleButton.Content = "✨ " + T("DesktopOrganization.Mode.Ai");
        AiConfigTitleText.Text = T("DesktopOrganization.Ai.ConfigTitle");
        AiProviderComboBox.Header = T("DesktopOrganization.Ai.ProviderHeader");
        AiBaseUrlTextBox.Header = T("DesktopOrganization.Ai.BaseUrlHeader");
        AiBaseUrlTextBox.PlaceholderText = "https://generativelanguage.googleapis.com/v1beta/openai/";
        AiApiKeyPasswordBox.Header = T("DesktopOrganization.Ai.ApiKeyHeader");
        AiApiKeyPasswordBox.PlaceholderText = T("DesktopOrganization.Ai.ApiKeyPlaceholder");
        AiModelTextBox.Header = T("DesktopOrganization.Ai.ModelHeader");
        AiModelTextBox.PlaceholderText = "gemini-2.5-flash";
        AiTestConnectionButton.Content = T("DesktopOrganization.Ai.TestConnection");
        AiSaveConfigButton.Content = T("DesktopOrganization.Ai.SaveConfig");
        AiCustomPromptTextBox.PlaceholderText = T("DesktopOrganization.Ai.PromptPlaceholder");
        AiGeneratePlanButton.Content = "✨ " + T("DesktopOrganization.Ai.GenerateAction");

        AiDeepInspectionText.Text = "🔬 深度探查本地文件";
        ToolTipService.SetToolTip(
            AiDeepInspectionCheckBox,
            "开启（推荐）：深入解析桌面 .lnk/.url 快捷方式指向的真实目标程序、提取 .exe 产品/开发商信息、读取文本文档前 240 字内容摘要、扫描子目录文件结构后再交由 AI 思考。\n" +
            "关闭：仅发送文件名与后缀，依靠大模型自身知识库快速判断。");

        AiWebSearchText.Text = "🌐 联网搜索软件百科";
        ToolTipService.SetToolTip(
            AiWebSearchCheckBox,
            "开启（推荐）：针对桌面上的第三方游戏启动器（如 CurseForge、Modrinth）、小众开发工具与英文快捷方式，自动联网检索软件百科定义并缓存，精准识别软件用途与归类！");

        AiSmartReuseText.Text = "♻️ 归入已有格子";
        AiAutoBindRulesText.Text = "🔗 自动绑定路由";
        AiEnableWidgetGroupsText.Text = "📦 推荐合并为格子组";
        ToolTipService.SetToolTip(AiSmartReuseCheckBox, T("DesktopOrganization.Ai.Option.SmartReuseTip"));
        ToolTipService.SetToolTip(AiAutoBindRulesCheckBox, T("DesktopOrganization.Ai.Option.AutoBindRulesTip"));
        ToolTipService.SetToolTip(AiEnableWidgetGroupsCheckBox, T("DesktopOrganization.Ai.Option.EnableWidgetGroupsTip"));

        AiDraftsLabelText.Text = "🗂️ 备选方案库：";
        AiSaveDraftButton.Content = "💾 暂存当前方案";
        ToolTipService.SetToolTip(AiSaveDraftButton, "将当前 AI 生成的整理方案保存为备选快照，方便修改指令后对比或随时切回。");
        AiDeleteDraftButton.Content = "🗑️ 删除";
        AiToggleThinkingButton.Content = "💭 思考过程";
        ToolTipService.SetToolTip(AiToggleThinkingButton, "展开或收起类似 Antigravity 的 AI 实时思考链、本地深度探查线索与分区推演过程。");
        AiToggleDebugButton.Content = "🐞 调试报文";
        ToolTipService.SetToolTip(AiToggleDebugButton, "查看发送给大模型的完整深层探查 Prompt JSON 与原始返回 Raw JSON，方便排查与调试。");
    }

    private async Task LoadStoredApiKeyAsync(string providerId)
    {
        string? secret = await Coordinator.AiService.GetApiKeyAsync(providerId);
        DispatcherQueue.TryEnqueue(() =>
        {
            if (GetSelectedProviderId() == providerId)
            {
                AiApiKeyPasswordBox.Password = secret ?? string.Empty;
                UpdateAiProviderStatusBadge();
            }
        });
    }

    private string GetSelectedProviderId()
    {
        if (AiProviderComboBox.SelectedItem is ComboBoxItem { Tag: string id } && !string.IsNullOrWhiteSpace(id))
        {
            return id;
        }

        return DesktopOrganizationAiProviderIds.Gemini;
    }

    private string GetSelectedStorageMode()
    {
        if (AiStorageModeComboBox.SelectedItem is ComboBoxItem { Tag: string mode } && !string.IsNullOrWhiteSpace(mode))
        {
            return DesktopOrganizationStorageModes.Normalize(mode);
        }

        return DesktopOrganizationStorageModes.InPlaceDesktop;
    }

    private static string GetStorageModeShortName(string? mode)
    {
        string normalized = DesktopOrganizationStorageModes.Normalize(mode);
        return normalized switch
        {
            DesktopOrganizationStorageModes.InPlaceDesktop => "原位桌面虚拟收纳 (文件留在桌面)",
            DesktopOrganizationStorageModes.DesktopSubfolders => "桌面子文件夹归档 (留在 Desktop 内)",
            _ => "独立外部仓库目录"
        };
    }

    private DesktopOrganizationAiOptions BuildUiAiOptions()
    {
        return new DesktopOrganizationAiOptions
        {
            ProviderId = GetSelectedProviderId(),
            BaseUrl = AiBaseUrlTextBox.Text.Trim(),
            Model = AiModelTextBox.Text.Trim(),
            ApiKey = string.IsNullOrWhiteSpace(AiApiKeyPasswordBox.Password) ? null : AiApiKeyPasswordBox.Password.Trim(),
            CustomPrompt = AiCustomPromptTextBox.Text.Trim(),
            EnableDeepInspection = AiDeepInspectionCheckBox.IsChecked == true,
            EnableWebSearch = AiWebSearchCheckBox.IsChecked == true,
            StorageMode = GetSelectedStorageMode(),
            EnableSmartWidgetReuse = AiSmartReuseCheckBox.IsChecked == true,
            AutoBindRoutingRules = AiAutoBindRulesCheckBox.IsChecked == true,
            EnableWidgetGroupSuggestions = AiEnableWidgetGroupsCheckBox.IsChecked == true
        };
    }

    private void UpdateAiProviderStatusBadge()
    {
        var preset = DesktopOrganizationAiProviderPresets.GetById(GetSelectedProviderId());
        string model = string.IsNullOrWhiteSpace(AiModelTextBox.Text) ? preset.DefaultModel : AiModelTextBox.Text.Trim();
        AiProviderStatusText.Text = $"🤖 {preset.DefaultDisplayName} · {model}";
    }

    private void RuleModeToggleButton_Click(object sender, RoutedEventArgs e) => ModeToggle_Click(sender, e);

    private void AiModeToggleButton_Click(object sender, RoutedEventArgs e) => ModeToggle_Click(sender, e);

    private void ModeToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_initializingAiControls)
        {
            return;
        }

        bool targetAi = ReferenceEquals(sender, AiModeToggleButton);
        _isAiMode = targetAi;
        RuleModeToggleButton.IsChecked = !_isAiMode;
        AiModeToggleButton.IsChecked = _isAiMode;
        AiControlPanel.Visibility = _isAiMode ? Visibility.Visible : Visibility.Collapsed;

        var slice = App.Current.SettingsService.Settings.DesktopOrganization;
        slice.DesktopOrganizationMode = _isAiMode ? DesktopOrganizationModes.Ai : DesktopOrganizationModes.Rule;
        _ = App.Current.SettingsService.SaveAsync(notifySubscribers: false);

        if (!_isAiMode)
        {
            AiPlanInsightsPanel.Visibility = Visibility.Collapsed;
            _ = ScanAsync(preserveSelection: true);
        }
        else if (_plan is null || !_plan.IsAiPlan)
        {
            ShowAiReadyPromptState();
        }
    }

    private void ShowAiReadyPromptState()
    {
        ResultInfo.Severity = InfoBarSeverity.Informational;
        ResultInfo.Title = string.Empty;
        ResultInfo.Message = T("DesktopOrganization.Ai.ReadyHint");
        ResultInfo.IsOpen = true;
    }

    private void AiProviderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializingAiControls)
        {
            return;
        }

        string providerId = GetSelectedProviderId();
        var preset = DesktopOrganizationAiProviderPresets.GetById(providerId);
        if (!string.IsNullOrWhiteSpace(preset.DefaultBaseUrl))
        {
            AiBaseUrlTextBox.Text = preset.DefaultBaseUrl;
        }

        if (!string.IsNullOrWhiteSpace(preset.DefaultModel))
        {
            AiModelTextBox.Text = preset.DefaultModel;
        }

        AiConnectionStatusText.Visibility = Visibility.Collapsed;
        UpdateAiProviderStatusBadge();
        _ = LoadStoredApiKeyAsync(providerId);
    }

    private async void AiStorageModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializingAiControls)
        {
            return;
        }

        var options = BuildUiAiOptions();
        await Coordinator.SaveAiOptionsAsync(options, updateApiKey: false);

        // If we already have an AI plan with a schema snapshot, immediately rebuild the preview with the new storage mode!
        if (_plan is { IsAiPlan: true, AiSchemaSnapshot: not null })
        {
            var syntheticDraft = new DesktopOrganizationAiSavedDraft
            {
                StorageMode = options.StorageMode,
                ThinkingTrace = _plan.AiThinkingTrace ?? string.Empty,
                RawPromptJson = _plan.AiRawPromptJson ?? string.Empty,
                RawResponseJson = _plan.AiRawResponseJson ?? string.Empty,
                SchemaSnapshot = _plan.AiSchemaSnapshot
            };

            bool includePersonal = PersonalDesktopSourceButton.IsChecked != false;
            bool includePublic = PublicDesktopSourceButton.IsChecked == true;

            _plan = await Coordinator.BuildPlanFromSavedDraftAsync(
                syntheticDraft,
                includePersonal,
                includePublic,
                includeSlowItems: false,
                _optionalIncludedPaths);

            _basePlan = _plan;
            RenderPlan(_plan);
            UpdateSummary(_plan);
            RenderAiInsightsPanel(_plan);
            ResultInfo.Severity = InfoBarSeverity.Informational;
            ResultInfo.Title = "已切换整理存储方式";
            ResultInfo.Message = $"当前预览已实时更新为「{GetStorageModeShortName(options.StorageMode)}」。";
            ResultInfo.IsOpen = true;
        }
    }

    private void AiToggleNativeDesktopIconsButton_Click(object sender, RoutedEventArgs e)
    {
        bool nowVisible = DesktopNativeIconVisibilityHelper.ToggleNativeDesktopIcons();
        UpdateNativeDesktopIconsButtonText();
        ResultInfo.Severity = InfoBarSeverity.Informational;
        ResultInfo.Title = nowVisible ? "已恢复显示系统原生桌面图标" : "已隐藏系统原生散乱桌面图标";
        ResultInfo.Message = nowVisible
            ? "Windows 原生桌面图标层已重新显示，所有文件仍在系统 Desktop 原位。"
            : "Windows 原生桌面散乱图标已隐藏，桌面由 DeskBox 分区盒子接管显示，且资源管理器左侧「桌面」中的所有文件 100% 保留在原位。";
        ResultInfo.IsOpen = true;
    }

    private async void AiOptionCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializingAiControls)
        {
            return;
        }

        var options = BuildUiAiOptions();
        await Coordinator.SaveAiOptionsAsync(options, updateApiKey: false);
    }

    private async void AiTestConnectionButton_Click(object sender, RoutedEventArgs e)
    {
        AiTestConnectionButton.IsEnabled = false;
        AiConnectionStatusText.Visibility = Visibility.Visible;
        AiConnectionStatusText.Text = T("DesktopOrganization.Ai.TestingConnection");

        try
        {
            var options = BuildUiAiOptions();
            var (success, message) = await Coordinator.AiService.TestConnectionAsync(options);
            AiConnectionStatusText.Text = success
                ? $"✅ {T("DesktopOrganization.Ai.ConnectionSuccess")} ({message})"
                : $"❌ {message}";
        }
        finally
        {
            AiTestConnectionButton.IsEnabled = true;
        }
    }

    private async void AiSaveConfigButton_Click(object sender, RoutedEventArgs e)
    {
        var options = BuildUiAiOptions();
        await Coordinator.SaveAiOptionsAsync(
            options,
            apiKeyToUpdate: AiApiKeyPasswordBox.Password,
            updateApiKey: true);
        UpdateAiProviderStatusBadge();
        AiConfigFlyout.Hide();
    }

    private void AiCustomPromptTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            _ = GenerateAiPlanFromUiAsync();
        }
    }

    private void AiGeneratePlanButton_Click(object sender, RoutedEventArgs e)
    {
        _ = GenerateAiPlanFromUiAsync();
    }

    private void AiSaveDraftButton_Click(object sender, RoutedEventArgs e)
    {
        if (_plan is null || !_plan.IsAiPlan || _plan.AiSchemaSnapshot is null)
        {
            ResultInfo.Severity = InfoBarSeverity.Warning;
            ResultInfo.Title = "暂无可暂存的 AI 方案";
            ResultInfo.Message = "请先点击「✨ 生成 AI 整理方案」生成方案后再暂存。";
            ResultInfo.IsOpen = true;
            return;
        }

        var options = BuildUiAiOptions();
        string? promptNote = string.IsNullOrWhiteSpace(options.CustomPrompt)
            ? null
            : (options.CustomPrompt.Length > 14 ? options.CustomPrompt[..14] + "..." : options.CustomPrompt);
        string customTitle = promptNote is not null
            ? $"⭐ 手动暂存 ({DateTime.Now:HH:mm:ss}) · \"{promptNote}\" ({_plan.Targets.Count}个分区)"
            : $"⭐ 手动暂存 ({DateTime.Now:HH:mm:ss}) · {_plan.Targets.Count}个分区 ({_plan.EligibleItemCount}项)";

        var saved = Coordinator.AiService.SaveDraftFromPlan(_plan, options, customTitle);
        ReloadSavedDraftsComboBox(saved.Id);

        ResultInfo.Severity = InfoBarSeverity.Success;
        ResultInfo.Title = "已暂存到备选方案库";
        ResultInfo.Message = $"已保存快照「{saved.Title}」，您随时可以在备选下拉框中一键切回该方案。";
        ResultInfo.IsOpen = true;
    }

    private void AiDeleteDraftButton_Click(object sender, RoutedEventArgs e)
    {
        if (AiSavedDraftsComboBox.SelectedItem is ComboBoxItem { Tag: string draftId } &&
            !string.IsNullOrWhiteSpace(draftId))
        {
            Coordinator.AiService.DeleteSavedDraft(draftId);
            ReloadSavedDraftsComboBox();
        }
    }

    private async void AiSavedDraftsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializingAiControls || _suppressDraftComboSelection)
        {
            return;
        }

        AiDeleteDraftButton.IsEnabled = AiSavedDraftsComboBox.SelectedIndex >= 0;
        if (AiSavedDraftsComboBox.SelectedItem is not ComboBoxItem { Tag: string draftId } ||
            string.IsNullOrWhiteSpace(draftId))
        {
            return;
        }

        var draft = _savedDrafts.FirstOrDefault(d => string.Equals(d.Id, draftId, StringComparison.Ordinal));
        if (draft is null)
        {
            return;
        }

        try
        {
            bool includePersonal = PersonalDesktopSourceButton.IsChecked != false;
            bool includePublic = PublicDesktopSourceButton.IsChecked == true;

            _plan = await Coordinator.BuildPlanFromSavedDraftAsync(
                draft,
                includePersonal,
                includePublic,
                includeSlowItems: false,
                _optionalIncludedPaths);
            _basePlan = _plan;

            // Reset per-plan exclusions and sync opt-in items so the restored draft matches its exact snapshot state
            _targetSelections.Clear();
            _excludedSourcePaths.Clear();
            foreach (var targetItem in _plan.Targets.SelectMany(t => t.Items))
            {
                if (targetItem.CanOptIn)
                {
                    _optionalIncludedPaths.Add(targetItem.SourcePath);
                }
            }

            _initializingAiControls = true;
            PopulateStorageModeComboBox(draft.StorageMode);
            AiDeepInspectionCheckBox.IsChecked = draft.EnableDeepInspection;
            if (!string.IsNullOrWhiteSpace(draft.CustomPrompt))
            {
                AiCustomPromptTextBox.Text = draft.CustomPrompt;
            }
            _initializingAiControls = false;

            UpdateThinkingAndDebugPanelsFromPlan(_plan, $"🗂️ 已从备选方案恢复：{draft.Title}");
            RenderPlan(_plan);
            UpdateSummary(_plan);
            RenderAiInsightsPanel(_plan);
        }
        catch (Exception ex)
        {
            _initializingAiControls = false;
            ResultInfo.Severity = InfoBarSeverity.Error;
            ResultInfo.Title = "恢复备选方案失败";
            ResultInfo.Message = ex.Message;
            ResultInfo.IsOpen = true;
        }
    }

    private void AiToggleThinkingButton_Click(object sender, RoutedEventArgs e)
    {
        AiLiveThinkingPanel.Visibility = AiLiveThinkingPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void AiToggleDebugButton_Click(object sender, RoutedEventArgs e)
    {
        AiDebugInspectorPanel.Visibility = AiDebugInspectorPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private async Task GenerateAiPlanFromUiAsync()
    {
        var options = BuildUiAiOptions();
        await Coordinator.SaveAiOptionsAsync(
            options,
            apiKeyToUpdate: AiApiKeyPasswordBox.Password,
            updateApiKey: !string.IsNullOrWhiteSpace(AiApiKeyPasswordBox.Password));
        UpdateAiProviderStatusBadge();
        await RefreshAiPlanCoreAsync(options);
    }

    private async Task RefreshAiPlanCoreAsync(DesktopOrganizationAiOptions? optionsOverride = null)
    {
        if (_isScanning || _isExecuting)
        {
            return;
        }

        _isScanning = true;
        RefreshButton.IsEnabled = false;
        AiGeneratePlanButton.IsEnabled = false;
        ExecuteButton.IsEnabled = false;
        string originalButtonText = "✨ " + T("DesktopOrganization.Ai.GenerateAction");
        AiGeneratePlanButton.Content = "⏳ " + T("DesktopOrganization.Ai.GeneratingStatus");
        ResultInfo.Severity = InfoBarSeverity.Informational;
        ResultInfo.Title = string.Empty;
        ResultInfo.Message = T("DesktopOrganization.Ai.GeneratingSummary");
        ResultInfo.IsOpen = true;

        // Automatically open Antigravity-style live thinking panel during generation!
        AiLiveThinkingPanel.Visibility = Visibility.Visible;
        AiThinkingSpinner.Visibility = Visibility.Visible;
        AiThinkingSpinner.IsActive = true;
        AiLiveStageText.Text = "💭 正在启动桌面扫描与深度探查引擎...";
        AiLiveThinkingTextBox.Text = "> 💭 思考：正在扫描桌面文件并初始化 AI 规划上下文...";

        try
        {
            var options = optionsOverride ?? BuildUiAiOptions();
            var liveProgress = new Progress<DesktopOrganizationAiStreamProgress>(report =>
            {
                AiLiveStageText.Text = report.StageSummary;
                if (!string.IsNullOrWhiteSpace(report.ThinkingText))
                {
                    AiLiveThinkingTextBox.Text = report.ThinkingText;
                }
            });

            bool includePersonal = PersonalDesktopSourceButton.IsChecked != false;
            bool includePublic = PublicDesktopSourceButton.IsChecked == true;

            _plan = await Coordinator.BuildAiPlanAsync(
                options,
                includePersonal,
                includePublic,
                includeSlowItems: false,
                _optionalIncludedPaths,
                liveProgress);
            _basePlan = _plan;

            AiThinkingSpinner.IsActive = false;
            AiThinkingSpinner.Visibility = Visibility.Collapsed;

            // Auto-save as a draft snapshot in the history dropdown for easy fallback & comparison
            var autoDraft = Coordinator.AiService.SaveDraftFromPlan(_plan, options);
            ReloadSavedDraftsComboBox(autoDraft.Id);

            UpdateThinkingAndDebugPanelsFromPlan(
                _plan,
                $"✅ AI 思考与方案推演完成（共 {_plan.Targets.Count} 个分区，{_plan.EligibleItemCount} 项，存储模式：{GetStorageModeShortName(_plan.StorageMode)}）");

            RenderPlan(_plan);
            UpdateSummary(_plan);
            RenderAiInsightsPanel(_plan);
        }
        catch (Exception ex)
        {
            App.Log($"[DesktopOrganizationAi] Plan generation failed: {ex}");
            AiThinkingSpinner.IsActive = false;
            AiThinkingSpinner.Visibility = Visibility.Collapsed;
            AiLiveStageText.Text = $"❌ AI 思考中断：{ex.Message}";
            AiLiveThinkingTextBox.Text += $"\n\n[错误详情]\n{ex.Message}";

            ResultInfo.Severity = InfoBarSeverity.Error;
            ResultInfo.Title = T("DesktopOrganization.Ai.ErrorTitle");
            ResultInfo.Message = ex.Message;
            ResultInfo.IsOpen = true;
        }
        finally
        {
            _isScanning = false;
            RefreshButton.IsEnabled = true;
            AiGeneratePlanButton.IsEnabled = true;
            AiGeneratePlanButton.Content = originalButtonText;
            UpdateSummary(_plan);
        }
    }

    private void UpdateThinkingAndDebugPanelsFromPlan(DesktopOrganizationPlan plan, string stageHeader)
    {
        AiLiveStageText.Text = stageHeader;
        if (!string.IsNullOrWhiteSpace(plan.AiThinkingTrace))
        {
            AiLiveThinkingTextBox.Text = plan.AiThinkingTrace;
        }

        var debugBuilder = new StringBuilder();
        debugBuilder.AppendLine($"=== 1. 整理存储模式: {plan.StorageMode} ({GetStorageModeShortName(plan.StorageMode)}) ===");
        debugBuilder.AppendLine($"=== 2. 发送给大模型的桌面上下文 (Prompt Payload JSON) ===");
        debugBuilder.AppendLine(string.IsNullOrWhiteSpace(plan.AiRawPromptJson) ? "(无)" : plan.AiRawPromptJson);
        debugBuilder.AppendLine();
        debugBuilder.AppendLine("=== 3. 大模型原始响应报文 (Raw Response Content) ===");
        debugBuilder.AppendLine(string.IsNullOrWhiteSpace(plan.AiRawResponseJson) ? "(无)" : plan.AiRawResponseJson);
        AiDebugInspectorTextBox.Text = debugBuilder.ToString();
    }

    private void RenderAiInsightsPanel(DesktopOrganizationPlan? plan)
    {
        if (plan is null || !plan.IsAiPlan)
        {
            AiPlanInsightsPanel.Visibility = Visibility.Collapsed;
            return;
        }

        bool hasSummary = !string.IsNullOrWhiteSpace(plan.AiSummary);
        bool hasGroups = plan.AiGroupSuggestions.Count > 0;
        if (!hasSummary && !hasGroups)
        {
            AiPlanInsightsPanel.Visibility = Visibility.Collapsed;
            return;
        }

        AiPlanInsightsPanel.Visibility = Visibility.Visible;
        string modeTag = $"【{GetStorageModeShortName(plan.StorageMode)}】";
        AiSummaryBannerText.Text = hasSummary
            ? $"✨ {modeTag} {plan.AiSummary}"
            : $"✨ {modeTag}";
        AiSummaryBannerText.Visibility = Visibility.Visible;

        AiGroupSuggestionsHost.Children.Clear();
        if (hasGroups)
        {
            AiGroupSuggestionsHost.Visibility = Visibility.Visible;
            var header = new TextBlock
            {
                Text = T("DesktopOrganization.Ai.WidgetGroupSuggestionsHeader"),
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 2, 0, 2)
            };
            AiGroupSuggestionsHost.Children.Add(header);

            foreach (DesktopOrganizationAiGroupSuggestion suggestion in plan.AiGroupSuggestions)
            {
                string joinedTargets = string.Join(" + ", suggestion.TargetDisplayNames);
                string label = string.IsNullOrWhiteSpace(suggestion.Reason)
                    ? $"{suggestion.GroupTitle} ({joinedTargets})"
                    : $"{suggestion.GroupTitle} ({joinedTargets}) — {suggestion.Reason}";

                var checkBox = new CheckBox
                {
                    IsChecked = suggestion.IsSelected,
                    Content = new TextBlock
                    {
                        Text = label,
                        FontSize = 12,
                        TextWrapping = TextWrapping.Wrap
                    },
                    Tag = suggestion
                };
                checkBox.Checked += AiGroupSuggestionCheckBox_Changed;
                checkBox.Unchecked += AiGroupSuggestionCheckBox_Changed;
                AiGroupSuggestionsHost.Children.Add(checkBox);
            }
        }
        else
        {
            AiGroupSuggestionsHost.Visibility = Visibility.Collapsed;
        }
    }

    private void AiGroupSuggestionCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { Tag: DesktopOrganizationAiGroupSuggestion suggestion } cb)
        {
            suggestion.IsSelected = cb.IsChecked == true;
        }
    }
}
