using DeskBox.Helpers;
using DeskBox.Models;
using DeskBox.Platform;
using DeskBox.Services;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.Graphics;
using WinRT.Interop;

namespace DeskBox.Views;

public sealed partial class DesktopLayoutDesignerWindow : Window
{
    private const int DesiredWidth = 1280;
    private const int DesiredHeight = 840;

    private readonly IntPtr _hWnd;
    private readonly AppWindow _appWindow;
    private readonly DesktopLayoutDesignService _layoutService = new();
    private CancellationTokenSource? _aiCts;
    private ComputedDesktopLayoutPlan? _currentPreviewPlan;
    private bool _isInitialized;

    // 缩略图拖拽状态
    private Border? _draggingCard;
    private ComputedWidgetBounds? _draggingBounds;
    private Point _dragStartPointer;
    private double _dragStartLeft;
    private double _dragStartTop;

    public DesktopLayoutDesignerWindow()
    {
        InitializeComponent();
        Title = "DeskBox · AI 桌面空间布局设计师";
        if (WindowsCompatibilityService.ApplySafeBackdrop(this) != "Solid")
        {
            RootGrid.Background = null;
        }

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        _hWnd = WindowNative.GetWindowHandle(this);
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(_hWnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);
        AppBranding.ApplyWindowIcon(_appWindow);

        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = true;
            presenter.IsMaximizable = true;
            presenter.IsMinimizable = true;
        }

        ResizeAndCenter(windowId);
        _isInitialized = true;

        RefreshSavedLayoutsLibraryUI();
        GenerateInstantPreviewForSelectedStyle();
    }

    public IntPtr WindowHandle => _hWnd;

    public void ShowWindow()
    {
        _appWindow.Show();
        App.Current.WidgetManager?.BringAuxiliaryWindowToFront(_hWnd, "desktop-layout-designer-shown");
        Activate();
    }

    private void ResizeAndCenter(WindowId windowId)
    {
        try
        {
            DisplayArea displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
            RectInt32 workArea = displayArea.WorkArea;
            double scale = Math.Max(1.0, Win32Helper.GetDpiScaleForWindow(_hWnd, RootGrid?.XamlRoot));
            int width = Math.Min((int)Math.Round(DesiredWidth * scale), Math.Max(960, workArea.Width - 64));
            int height = Math.Min((int)Math.Round(DesiredHeight * scale), Math.Max(680, workArea.Height - 64));
            int x = workArea.X + (workArea.Width - width) / 2;
            int y = workArea.Y + (workArea.Height - height) / 2;
            _appWindow.MoveAndResize(new RectInt32(x, y, width, height));
        }
        catch
        {
        }
    }

    private (DesktopOrganizationRect WorkArea, double Scale, List<WidgetConfig> AllWidgets) GetDesktopMetrics()
    {
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(_hWnd);
        DisplayArea displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
        RectInt32 wa = displayArea.WorkArea;
        var workArea = new DesktopOrganizationRect(wa.X, wa.Y, wa.Width, wa.Height);
        double scale = Math.Max(1.0, Win32Helper.GetDpiScaleForWindow(_hWnd, RootGrid?.XamlRoot));
        var widgets = App.Current.SettingsService.Settings.WidgetLayout.Widgets.ToList();
        return (workArea, scale, widgets);
    }

    private string GetSelectedStyleTag()
    {
        if (LayoutStyleComboBox?.SelectedItem is ComboBoxItem item && item.Tag is string tag && !string.IsNullOrWhiteSpace(tag))
        {
            return tag;
        }

        return DesktopLayoutStyles.SurroundStage;
    }

    private void LayoutStyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        GenerateInstantPreviewForSelectedStyle();
    }

    private void QuickPromptChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string chipText)
        {
            string existing = CustomPromptTextBox.Text?.Trim() ?? string.Empty;
            CustomPromptTextBox.Text = string.IsNullOrWhiteSpace(existing)
                ? chipText
                : $"{existing}\n{chipText}";
            GenerateInstantPreviewForSelectedStyle();
        }
    }

    private void FastAlgorithmPreviewButton_Click(object sender, RoutedEventArgs e)
    {
        GenerateInstantPreviewForSelectedStyle();
        StatusFooterText.Text = $"⚡ 已按「{DesktopLayoutStyles.GetDisplayTitle(GetSelectedStyleTag())}」秒级生成缩略图预览（0ms 零延迟）。";
    }

    private void GenerateInstantPreviewForSelectedStyle()
    {
        var (workArea, scale, allWidgets) = GetDesktopMetrics();
        var visible = allWidgets.Where(w => w.IsVisible && !w.IsDisabled).ToList();
        string style = GetSelectedStyleTag();
        string customPrompt = CustomPromptTextBox?.Text?.Trim() ?? string.Empty;

        var plan = DesktopLayoutDesignService.ComputeBentoLayoutForLiveWidgets(
            visible,
            workArea,
            scale,
            style,
            aiDesign: null,
            customPrompt: customPrompt);

        RenderPreviewPlan(plan, workArea);
    }

    private async void GenerateAiDesignButton_Click(object sender, RoutedEventArgs e)
    {
        _aiCts?.Cancel();
        _aiCts = new CancellationTokenSource();
        CancellationToken token = _aiCts.Token;

        var aiService = new DesktopOrganizationAiService();
        var slice = App.Current.SettingsService.Settings.DesktopOrganization;
        DesktopOrganizationAiService.TryApplyLocalProfileOverrides(slice);
        string providerId = string.IsNullOrWhiteSpace(slice.DesktopOrganizationAiProviderId)
            ? DesktopOrganizationAiProviderIds.Gemini
            : slice.DesktopOrganizationAiProviderId;
        string? apiKey = await aiService.GetApiKeyAsync(providerId, token);
        var aiOptions = new DesktopOrganizationAiOptions
        {
            ProviderId = providerId,
            BaseUrl = string.IsNullOrWhiteSpace(slice.DesktopOrganizationAiBaseUrl)
                ? "https://generativelanguage.googleapis.com/v1beta/openai/"
                : slice.DesktopOrganizationAiBaseUrl,
            ApiKey = apiKey,
            Model = string.IsNullOrWhiteSpace(slice.DesktopOrganizationAiModel)
                ? "gemini-2.5-flash"
                : slice.DesktopOrganizationAiModel,
            CustomPrompt = CustomPromptTextBox.Text?.Trim() ?? string.Empty
        };

        string style = GetSelectedStyleTag();
        var (workArea, scale, allWidgets) = GetDesktopMetrics();

        GenerateAiDesignButton.IsEnabled = false;
        AiThinkingCard.Visibility = Visibility.Visible;
        AiProgressRing.IsActive = true;
        AiPhaseText.Text = $"🎨 正在请求 {aiOptions.Model} 重新构思全画幅空间布局...";
        AiStreamPreviewText.Text = "正在分析桌面组件密度、留白区域与用户定制提示词...";

        var progress = new Progress<DesktopOrganizationAiStreamProgress>(p =>
        {
            if (!string.IsNullOrWhiteSpace(p.StageSummary))
            {
                AiPhaseText.Text = p.StageSummary;
            }

            if (!string.IsNullOrWhiteSpace(p.ThinkingText))
            {
                string text = p.ThinkingText.Trim();
                AiStreamPreviewText.Text = text.Length > 260 ? "..." + text[^260..] : text;
            }
        });

        try
        {
            var plan = await _layoutService.DesignLiveDesktopWithAiAsync(
                allWidgets,
                workArea,
                scale,
                aiOptions,
                style,
                useAiModel: true,
                progress: progress,
                cancellationToken: token,
                customPrompt: aiOptions.CustomPrompt);

            RenderPreviewPlan(plan, workArea);
            StatusFooterText.Text = $"✨ AI 已完成「{plan.DesignTitle}」2D 空间设计！请在上方缩略图确认效果，点击右侧按钮即可应用到桌面。";
        }
        catch (OperationCanceledException)
        {
            StatusFooterText.Text = "⏹️ 已取消 AI 空间设计请求，当前保留上一次缩略图预览。";
        }
        catch (Exception ex)
        {
            StatusFooterText.Text = $"⚠️ AI 请求异常（已自动回退为架构几何引擎预览）：{ex.Message}";
            GenerateInstantPreviewForSelectedStyle();
        }
        finally
        {
            GenerateAiDesignButton.IsEnabled = true;
            AiProgressRing.IsActive = false;
            AiThinkingCard.Visibility = Visibility.Collapsed;
        }
    }

    private void CancelAiButton_Click(object sender, RoutedEventArgs e)
    {
        _aiCts?.Cancel();
    }

    private void SaveCurrentPreviewButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPreviewPlan is null) return;
        string customName = SavePresetNameBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(customName))
        {
            customName = $"{_currentPreviewPlan.DesignTitle} ({DateTime.Now:MM-dd HH:mm})";
        }

        var preset = SavedDesktopLayoutPreset.FromPlan(_currentPreviewPlan, customName);
        DesktopLayoutDesignService.SaveLayoutPreset(preset);
        SavePresetNameBox.Text = string.Empty;
        RefreshSavedLayoutsLibraryUI();
        StatusFooterText.Text = $"💾 已将方案「{preset.Name}」保存到本地布局方案库！随时可点击瞬时预览或恢复。";
    }

    private void CaptureLiveDesktopButton_Click(object sender, RoutedEventArgs e)
    {
        var (workArea, scale, allWidgets) = GetDesktopMetrics();
        string customName = SavePresetNameBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(customName))
        {
            customName = $"📸 桌面实时快照 ({DateTime.Now:MM-dd HH:mm})";
        }

        var plan = DesktopLayoutDesignService.CaptureCurrentDesktopAsPlan(allWidgets, workArea, scale, customName);
        var preset = SavedDesktopLayoutPreset.FromPlan(plan, customName);
        DesktopLayoutDesignService.SaveLayoutPreset(preset);
        SavePresetNameBox.Text = string.Empty;
        RefreshSavedLayoutsLibraryUI();
        RenderPreviewPlan(plan, workArea);
        StatusFooterText.Text = $"📸 已抓取当前真实桌面的所有组件位置与分组，并保存为「{preset.Name}」！";
    }

    private void ResetToCurrentDesktopButton_Click(object sender, RoutedEventArgs e)
    {
        var (workArea, scale, allWidgets) = GetDesktopMetrics();
        var plan = DesktopLayoutDesignService.CaptureCurrentDesktopAsPlan(allWidgets, workArea, scale, "当前真实桌面排布");
        RenderPreviewPlan(plan, workArea);
        StatusFooterText.Text = "🔄 缩略图已还原为当前真实桌面排布。";
    }

    private async void ApplyToDesktopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPreviewPlan is null || App.Current.WidgetManager is null) return;

        ApplyToDesktopButton.IsEnabled = false;
        ApplyToDesktopButton.Content = "⏳ 正在重排桌面窗口...";
        StatusFooterText.Text = $"🚀 正在将「{_currentPreviewPlan.DesignTitle}」同步到桌面窗口并合并标签组...";

        try
        {
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(_hWnd);
            DisplayArea displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
            double scale = Math.Max(1.0, Win32Helper.GetDpiScaleForWindow(_hWnd, RootGrid?.XamlRoot));

            await App.Current.WidgetManager.ApplyComputedDesktopLayoutAsync(
                _currentPreviewPlan,
                displayArea.WorkArea,
                scale);

            StatusFooterText.Text = $"✅ 已成功将「{_currentPreviewPlan.DesignTitle}」应用到桌面！（共排布 {_currentPreviewPlan.WidgetBounds.Count(x => !x.IsSecondaryTabMember)} 个主容器，合并 {_currentPreviewPlan.TabGroups.Count} 个多标签舱）";
        }
        catch (Exception ex)
        {
            StatusFooterText.Text = $"❌ 应用布局失败：{ex.Message}";
        }
        finally
        {
            ApplyToDesktopButton.IsEnabled = true;
            ApplyToDesktopButton.Content = "🚀 应用此预览布局到桌面";
        }
    }

    private void RefreshSavedLayoutsLibraryUI()
    {
        SavedPresetsStack.Children.Clear();
        var presets = DesktopLayoutDesignService.LoadSavedLayouts();
        SavedCountBadge.Text = presets.Count == 0 ? "暂无已存方案" : $"共 {presets.Count} 个方案";

        if (presets.Count == 0)
        {
            SavedPresetsStack.Children.Add(new Border
            {
                Padding = new Thickness(12, 10, 12, 10),
                CornerRadius = new CornerRadius(8),
                Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
                Child = new TextBlock
                {
                    Text = "尚未保存自定义布局。可点击上方「保存当前预览」或「备份当前桌面」随时存档。",
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                }
            });
            return;
        }

        foreach (var preset in presets)
        {
            var card = new Border
            {
                Padding = new Thickness(10, 8, 10, 8),
                CornerRadius = new CornerRadius(8),
                Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1)
            };

            var grid = new Grid { ColumnSpacing = 8 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var infoStack = new StackPanel { Spacing = 2 };
            infoStack.Children.Add(new TextBlock
            {
                Text = preset.Name,
                FontSize = 12.5,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            infoStack.Children.Add(new TextBlock
            {
                Text = $"留白 {preset.WallpaperBreathingRatio} · {preset.WidgetBounds.Count(b => !b.IsSecondaryTabMember)} 个容器 · {preset.SavedAt:MM-dd HH:mm}",
                FontSize = 11,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });

            var previewBtn = new Button
            {
                Content = "预览",
                Padding = new Thickness(10, 4, 10, 4),
                FontSize = 11.5,
                VerticalAlignment = VerticalAlignment.Center
            };
            previewBtn.Click += (_, _) =>
            {
                var (workArea, _, _) = GetDesktopMetrics();
                var plan = preset.ToPlan();
                RenderPreviewPlan(plan, workArea);
                StatusFooterText.Text = $"👁️ 正在预览已保存方案「{preset.Name}」，点击右上角「应用此布局到桌面」即可生效。";
            };

            var delBtn = new Button
            {
                Content = "删除",
                Padding = new Thickness(8, 4, 8, 4),
                FontSize = 11.5,
                VerticalAlignment = VerticalAlignment.Center
            };
            delBtn.Click += (_, _) =>
            {
                DesktopLayoutDesignService.DeleteLayoutPreset(preset.Id);
                RefreshSavedLayoutsLibraryUI();
            };

            Grid.SetColumn(infoStack, 0);
            Grid.SetColumn(previewBtn, 1);
            Grid.SetColumn(delBtn, 2);
            grid.Children.Add(infoStack);
            grid.Children.Add(previewBtn);
            grid.Children.Add(delBtn);
            card.Child = grid;

            SavedPresetsStack.Children.Add(card);
        }
    }

    private void PreviewStageHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        double availW = Math.Max(420, e.NewSize.Width - 20);
        double availH = Math.Max(260, e.NewSize.Height - 20);
        double targetW = availW;
        double targetH = targetW * 9.0 / 16.0;
        if (targetH > availH)
        {
            targetH = availH;
            targetW = targetH * 16.0 / 9.0;
        }

        MonitorFrameBorder.Width = Math.Round(targetW);
        MonitorFrameBorder.Height = Math.Round(targetH);

        if (_currentPreviewPlan is not null)
        {
            var (workArea, _, _) = GetDesktopMetrics();
            RenderPreviewPlan(_currentPreviewPlan, workArea);
        }
    }

    private void RenderPreviewPlan(ComputedDesktopLayoutPlan plan, DesktopOrganizationRect workArea)
    {
        _currentPreviewPlan = plan;
        PreviewTitleText.Text = plan.DesignTitle;
        PreviewBreathingText.Text = $"留白率 {plan.WallpaperBreathingRatio}";
        PreviewPhilosophyText.Text = plan.DesignPhilosophy;

        var primaryBoxes = plan.WidgetBounds.Where(b => !b.IsSecondaryTabMember).ToList();
        PreviewStatsText.Text = plan.TabGroups.Count > 0
            ? $"{primaryBoxes.Count} 个容器 · {plan.TabGroups.Count} 个多标签舱"
            : $"{primaryBoxes.Count} 个容器";

        PreviewCanvas.Children.Clear();

        double canvasW = MonitorFrameBorder.Width > 100 ? MonitorFrameBorder.Width : 800;
        double canvasH = (MonitorFrameBorder.Height > 100 ? MonitorFrameBorder.Height : 450) - 20;
        double scale = Math.Max(1.0, Win32Helper.GetDpiScaleForWindow(_hWnd, RootGrid?.XamlRoot));

        foreach (var box in primaryBoxes)
        {
            double normX = Math.Clamp((box.PhysicalX - workArea.X) / Math.Max(1.0, workArea.Width), 0.012, 0.92);
            double normY = Math.Clamp((box.PhysicalY - workArea.Y) / Math.Max(1.0, workArea.Height), 0.015, 0.90);
            double normW = Math.Clamp((box.LogicalWidth * scale) / Math.Max(1.0, workArea.Width), 0.105, 0.46);
            double normH = box.IsCollapsed
                ? 0.052
                : Math.Clamp((box.LogicalHeight * scale) / Math.Max(1.0, workArea.Height), 0.115, 0.52);

            double cardLeft = Math.Round(normX * canvasW);
            double cardTop = Math.Round(normY * canvasH);
            double cardW = Math.Max(78, Math.Round(normW * canvasW));
            double cardH = box.IsCollapsed ? 24 : Math.Max(42, Math.Round(normH * canvasH));

            if (cardLeft + cardW > canvasW - 6) cardLeft = Math.Max(6, canvasW - cardW - 6);
            if (cardTop + cardH > canvasH - 6) cardTop = Math.Max(6, canvasH - cardH - 6);

            var card = CreateThumbnailWidgetCard(box, cardW, cardH, workArea, canvasW, canvasH);
            Canvas.SetLeft(card, cardLeft);
            Canvas.SetTop(card, cardTop);
            PreviewCanvas.Children.Add(card);
        }
    }

    private Border CreateThumbnailWidgetCard(
        ComputedWidgetBounds box,
        double width,
        double height,
        DesktopOrganizationRect workArea,
        double canvasW,
        double canvasH)
    {
        bool isUtility = box.Kind != WidgetKind.File;
        bool isMultiTab = box.TabMemberNames is { Count: >= 2 };

        Windows.UI.Color bgColor = box.IsCollapsed
            ? Windows.UI.Color.FromArgb(220, 17, 94, 89)
            : isMultiTab
                ? Windows.UI.Color.FromArgb(215, 30, 27, 56)
                : isUtility
                    ? Windows.UI.Color.FromArgb(210, 23, 33, 54)
                    : Windows.UI.Color.FromArgb(215, 19, 28, 46);

        Windows.UI.Color strokeColor = box.IsCollapsed
            ? Windows.UI.Color.FromArgb(190, 45, 212, 191)
            : isMultiTab
                ? Windows.UI.Color.FromArgb(190, 168, 85, 247)
                : isUtility
                    ? Windows.UI.Color.FromArgb(150, 96, 165, 250)
                    : Windows.UI.Color.FromArgb(150, 56, 189, 248);

        var card = new Border
        {
            Width = width,
            Height = height,
            CornerRadius = new CornerRadius(box.IsCollapsed ? 12 : 7),
            Background = new SolidColorBrush(bgColor),
            BorderBrush = new SolidColorBrush(strokeColor),
            BorderThickness = new Thickness(1.2),
            Padding = new Thickness(8, 5, 8, 5),
            Tag = box
        };

        string iconPrefix = box.Kind switch
        {
            WidgetKind.Weather => "🌤️ ",
            WidgetKind.Todo => "☑️ ",
            WidgetKind.QuickCapture => "⚡ ",
            _ => box.IsCollapsed ? "💊 " : isMultiTab ? "🗂️ " : "📁 "
        };

        string headerTitle = !string.IsNullOrWhiteSpace(box.GroupTitle)
            ? box.GroupTitle
            : box.DisplayName;

        var rootGrid = new Grid { RowSpacing = 4 };
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // 顶部标题栏：左标题 + 右数量胶囊
        var headerGrid = new Grid { ColumnSpacing = 4 };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleText = new TextBlock
        {
            Text = iconPrefix + headerTitle,
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Colors.White),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(titleText, 0);
        headerGrid.Children.Add(titleText);

        if (!box.IsCollapsed && width >= 105)
        {
            string badgeText = isUtility
                ? "组件"
                : isMultiTab
                    ? $"{box.TabMemberNames.Count} 标签"
                    : $"{box.ItemCount}";

            var countBadge = new Border
            {
                Padding = new Thickness(5, 1, 5, 1),
                CornerRadius = new CornerRadius(4),
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(75, 255, 255, 255)),
                Child = new TextBlock
                {
                    Text = badgeText,
                    FontSize = 9.5,
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(230, 226, 232, 240))
                }
            };
            Grid.SetColumn(countBadge, 1);
            headerGrid.Children.Add(countBadge);
        }

        Grid.SetRow(headerGrid, 0);
        rootGrid.Children.Add(headerGrid);

        // 展开态内容区：多标签展示干净的子标签胶囊，普通分类展示模拟图标点阵
        if (!box.IsCollapsed && height >= 52)
        {
            var bodyPanel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 2, 0, 0) };

            if (isMultiTab)
            {
                var tabRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
                foreach (var tabName in box.TabMemberNames.Take(2))
                {
                    tabRow.Children.Add(new Border
                    {
                        Padding = new Thickness(5, 1, 5, 1),
                        CornerRadius = new CornerRadius(3),
                        Background = new SolidColorBrush(Windows.UI.Color.FromArgb(90, 168, 85, 247)),
                        Child = new TextBlock
                        {
                            Text = tabName,
                            FontSize = 9.5,
                            MaxWidth = Math.Max(42, (width - 28) / 2),
                            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(245, 243, 232, 255)),
                            TextTrimming = TextTrimming.CharacterEllipsis
                        }
                    });
                }
                bodyPanel.Children.Add(tabRow);
            }

            // 模拟内部文件图标网格线条，让缩略图更具真实桌面组件质感
            if (height >= 68)
            {
                var dotRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, Margin = new Thickness(1, 2, 0, 0) };
                int dotCount = Math.Clamp((int)((width - 20) / 18), 2, 6);
                for (int i = 0; i < dotCount; i++)
                {
                    dotRow.Children.Add(new Border
                    {
                        Width = 12,
                        Height = 12,
                        CornerRadius = new CornerRadius(3),
                        Background = new SolidColorBrush(Windows.UI.Color.FromArgb(42, 148, 163, 184))
                    });
                }
                bodyPanel.Children.Add(dotRow);
            }

            Grid.SetRow(bodyPanel, 1);
            rootGrid.Children.Add(bodyPanel);
        }

        card.Child = rootGrid;
        ToolTipService.SetToolTip(
            card,
            $"{headerTitle}\n屏幕坐标: ({box.PhysicalX:0}, {box.PhysicalY:0}) · 尺寸: {box.LogicalWidth:0}×{box.LogicalHeight:0}\n{(isMultiTab ? "包含标签页: " + string.Join("、", box.TabMemberNames) : $"包含 {box.ItemCount} 项 · 按住左键可直接拖动位置")}");

        // 绑定缩略图卡片拖拽微调事件
        card.PointerPressed += (s, e) =>
        {
            if (s is not Border b) return;
            _draggingCard = b;
            _draggingBounds = box;
            _dragStartPointer = e.GetCurrentPoint(PreviewCanvas).Position;
            _dragStartLeft = Canvas.GetLeft(b);
            _dragStartTop = Canvas.GetTop(b);
            b.CapturePointer(e.Pointer);
            b.Opacity = 0.85;
            e.Handled = true;
        };

        card.PointerMoved += (s, e) =>
        {
            if (_draggingCard != card || _draggingBounds is null) return;
            Point pt = e.GetCurrentPoint(PreviewCanvas).Position;
            double dx = pt.X - _dragStartPointer.X;
            double dy = pt.Y - _dragStartPointer.Y;

            double newLeft = Math.Clamp(_dragStartLeft + dx, 2, Math.Max(2, canvasW - card.Width - 2));
            double newTop = Math.Clamp(_dragStartTop + dy, 2, Math.Max(2, canvasH - card.Height - 2));
            Canvas.SetLeft(card, newLeft);
            Canvas.SetTop(card, newTop);

            // 同步换算回真实桌面物理坐标
            double newPhysX = Math.Round(workArea.X + (newLeft / Math.Max(1.0, canvasW)) * workArea.Width);
            double newPhysY = Math.Round(workArea.Y + (newTop / Math.Max(1.0, canvasH)) * workArea.Height);
            _draggingBounds.PhysicalX = newPhysX;
            _draggingBounds.PhysicalY = newPhysY;

            if (_currentPreviewPlan is not null && !string.IsNullOrWhiteSpace(_draggingBounds.GroupTitle))
            {
                foreach (var sec in _currentPreviewPlan.WidgetBounds.Where(x =>
                             x.IsSecondaryTabMember &&
                             string.Equals(x.GroupTitle, _draggingBounds.GroupTitle, StringComparison.OrdinalIgnoreCase)))
                {
                    sec.PhysicalX = newPhysX;
                    sec.PhysicalY = newPhysY;
                }
            }

            e.Handled = true;
        };

        card.PointerReleased += (s, e) =>
        {
            if (_draggingCard != card) return;
            card.ReleasePointerCapture(e.Pointer);
            card.Opacity = 1.0;
            _draggingCard = null;
            _draggingBounds = null;
            StatusFooterText.Text = $"🎯 已微调「{headerTitle}」位置，点击右下角「🚀 应用此预览布局到桌面」即可生效。";
            e.Handled = true;
        };

        return card;
    }
}
