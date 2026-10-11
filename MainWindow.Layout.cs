using DexBridge.Services;
using Loc = DexBridge.Services.LocalizationService;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace DexBridge;

/// <summary>
/// Display-only dashboard density controls. Does not touch scrcpy/ADB session or stream settings.
/// The standard view is always available and all advanced controls remain accessible in compact mode.
/// </summary>
public partial class MainWindow
{
    private const double StandardWidth = 1280;
    private const double StandardHeight = 860;
    private bool _layoutInitialized;
    private bool _currentCompact;
    private double _standardWindowWidth = StandardWidth;
    private double _standardWindowHeight = StandardHeight;

    private void LayoutCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _loadingUi || LayoutCombo.SelectedItem is not ComboBoxItem choice)
            return;

        var compact = choice.Tag?.ToString() == "compact";
        _settings.UiLayout = compact ? "compact" : "standard";
        ApplyUiLayout(compact, resizeWindow: true);
        SettingsService.Save(_settings);
    }

    private void ApplyUiLayout(bool compact, bool resizeWindow)
    {
        // Resize the main window only; stream/mirroring windows are never adjusted here.
        if (resizeWindow && WindowState == System.Windows.WindowState.Normal)
        {
            if (compact)
            {
                if (_layoutInitialized && !_currentCompact)
                {
                    _standardWindowWidth = Width;
                    _standardWindowHeight = Height;
                }
                MinWidth = 1040;
                MinHeight = 630;
                Width = Math.Min(Width, 1140);
                Height = Math.Min(Height, 760);
            }
            else
            {
                MinWidth = 1120;
                MinHeight = 660;
                if (_layoutInitialized && _currentCompact)
                {
                    Width = Math.Max(MinWidth, _standardWindowWidth);
                    Height = Math.Max(MinHeight, _standardWindowHeight);
                }
            }
        }
        else
        {
            MinWidth = compact ? 1040 : 1120;
            MinHeight = compact ? 630 : 660;
        }

        RootGrid.Margin = compact ? new Thickness(12) : new Thickness(18);
        TextElement.SetFontSize(RootGrid, compact ? 12 : 13);
        HeaderBar.Margin = new Thickness(0, 0, 0, compact ? 5 : 10);
        BrandLogoBorder.Width = compact ? 36 : 44;
        BrandLogoBorder.Height = compact ? 36 : 44;
        BrandLogoBorder.Margin = new Thickness(0, 2, compact ? 8 : 12, 0);
        BrandTitleText.FontSize = compact ? 25 : 30;
        VersionCreditText.Margin = new Thickness(0, compact ? 1 : 4, 0, 0);
        VersionCreditText.FontSize = compact ? 11 : 12;

        ContentSectionsPanel.Margin = compact ? new Thickness(0, 0, 4, 2) : new Thickness(0, 0, 8, 6);
        DashboardGrid.Margin = new Thickness(0, 0, 0, compact ? 6 : 12);
        QuickSetupGroup.Margin = new Thickness(0, 0, compact ? 4 : 7, 0);
        DeviceMonitorGroup.Margin = new Thickness(compact ? 4 : 7, 0, 0, 0);
        QuickSetupGrid.Margin = new Thickness(compact ? 8 : 12);
        DeviceMonitorGrid.Margin = new Thickness(compact ? 6 : 10);
        MonitorActionsPanel.Margin = new Thickness(0, compact ? 4 : 8, 0, 0);
        DeviceCombo.MinHeight = compact ? 27 : 30;
        ApplicationCombo.MinHeight = compact ? 27 : 30;
        ApplicationSearchBox.MinHeight = compact ? 26 : 28;

        AdvancedSettingsGroup.Margin = new Thickness(0, 0, 0, compact ? 5 : 12);
        foreach (var group in new[] { DisplayStreamGroup, TelemetryOptionsGroup, CompatibilityOptionsGroup, DeveloperRuntimeGroup })
            group.Margin = compact ? new Thickness(0, 3, 0, 3) : new Thickness(0, 7, 0, 7);
        DisplayControlsGrid.Margin = new Thickness(compact ? 7 : 12);
        DeveloperRuntimeGrid.Margin = new Thickness(compact ? 7 : 12);
        TelemetryOptionsPanel.Margin = new Thickness(compact ? 6 : 12);
        CompatibilityOptionsPanel.Margin = new Thickness(compact ? 6 : 12);
        foreach (var box in TelemetryOptionsPanel.Children.OfType<CheckBox>())
            box.Margin = new Thickness(compact ? 3 : 6);
        foreach (var box in CompatibilityOptionsPanel.Children.OfType<CheckBox>())
            box.Margin = new Thickness(compact ? 3 : 6);

        // Keep log panels equal-height, with shorter log viewports in compact mode.
        var logHeight = compact ? 160 : 220;
        var logMinHeight = compact ? 140 : 205;
        LogBox.MinHeight = logMinHeight;
        FpsLogBox.MinHeight = logMinHeight;
        LogBox.Height = logHeight;
        FpsLogBox.Height = logHeight;
        RunningAppsList.MinHeight = compact ? 112 : 170;
        LogBox.Margin = new Thickness(compact ? 6 : 10);
        FpsLogGrid.Margin = new Thickness(compact ? 6 : 10);
        RunningAppsGrid.Margin = new Thickness(compact ? 6 : 10);
        SessionLogGroup.Margin = new Thickness(0, 0, compact ? 3 : 6, 0);
        FpsLogGroup.Margin = new Thickness(compact ? 3 : 6, 0, compact ? 3 : 6, 0);
        RunningAppsGroup.Margin = new Thickness(compact ? 3 : 6, 0, 0, 0);
        RunningAppsSummaryText.Margin = compact ? new Thickness(2, 4, 2, 3) : new Thickness(2, 7, 2, 4);

        // Short action captions prevent the Running Apps buttons from wrapping
        // in compact width. Full descriptions are preserved in tooltips.
        BringTaskFrontButton.Content = Loc.T(compact ? "Front" : "Bring to front");
        MoveTaskPhoneButton.Content = Loc.T(compact ? "To phone" : "Move to phone");
        BringTaskFrontButton.ToolTip = Loc.T("Bring to front");
        MoveTaskPhoneButton.ToolTip = Loc.T("Move to phone");
        foreach (var button in RunningAppsActionsPanel.Children.OfType<Button>())
        {
            button.Margin = new Thickness(compact ? 2 : 3);
            button.Padding = compact ? new Thickness(5, 3, 5, 3) : new Thickness(8, 5, 8, 5);
        }
        FooterGrid.Margin = new Thickness(0, compact ? 5 : 10, 0, 0);
        foreach (var button in FooterActionsPanel.Children.OfType<Button>())
        {
            button.Margin = new Thickness(compact ? 3 : 5, 0, compact ? 3 : 5, 0);
            button.Padding = compact ? new Thickness(10, 4, 10, 4) : new Thickness(14, 6, 14, 6);
        }

        _layoutInitialized = true;
        _currentCompact = compact;
    }
}
