using DeskBox.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DeskBox.Views.SettingsSections;

public sealed partial class FileWidgetSettingsSection : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(SettingsViewModel),
            typeof(FileWidgetSettingsSection),
            new PropertyMetadata(null));

    public FileWidgetSettingsSection()
    {
        InitializeComponent();
    }

    public SettingsViewModel? ViewModel
    {
        get => (SettingsViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public event EventHandler<SettingsSectionNavigationRequestedEventArgs>? NavigationRequested;

    private void NestedSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string sectionTag })
        {
            NavigationRequested?.Invoke(this, new SettingsSectionNavigationRequestedEventArgs(sectionTag));
        }
    }

    private void OrganizeDesktopButton_Click(object sender, RoutedEventArgs e)
    {
        global::DeskBox.App.Current.ShowDesktopOrganizationWindow();
    }

    private void DesignDesktopLayoutButton_Click(object sender, RoutedEventArgs e)
    {
        global::DeskBox.App.Current.ShowDesktopLayoutDesignerWindow();
    }
}
