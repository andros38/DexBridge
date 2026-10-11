using DexBridge.Models;
using DexBridge.Services;
using Loc = DexBridge.Services.LocalizationService;
using Microsoft.Win32;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace DexBridge;

public partial class MainWindow : Window
{
    private ScrcpySession? _session;
    private AdbService? _adb;
    private DeviceInfo? _activeDevice;
    private bool _phoneScreenPoweredOff;
    private bool _phoneScreenDesiredOff;
    private bool _phoneScreenGuardBusy;
    private int _fpsLogLineCount;
    private int _sessionLogLineCount;
    private readonly Queue<int> _recentStreamFps = new();
    private DateTime? _sustainedThermalSinceUtc;
    private DateTime _lastThermalAdvisoryUtc = DateTime.MinValue;
    private DateTime _lastRunningAppsPollUtc = DateTime.MinValue;
    private DateTime _lastRunningAppsWarningUtc = DateTime.MinValue;
    private DateTime _lastMonitorWarningUtc = DateTime.MinValue;
    private bool _dashboardPollBusy;
    private DateTime _lastPhoneScreenGuardLog = DateTime.MinValue;
    private bool _applyingPreset;
    private bool _applyingAppProfile;
    private bool _applyingTheme;
    private bool _loadingUi;
    private bool _stoppingSession;
    private bool _allowWindowClose;
    private bool _monitorBusy;
    private bool _runningAppsBusy;
    private bool _appLibraryBusy;
    private bool _restoringDevices;
    private bool _suppressAppSelection;
    private string? _selectedAppPackage;
    private string _lastSessionMode = "desktop";
    private bool _lastFreeformOk = true;
    private readonly DispatcherTimer _monitorTimer;
    private readonly DispatcherTimer _screenGuardTimer;
    private PhoneFilesWindow? _phoneFilesWindow;
    private TelemetryOverlayWindow? _telemetryOverlay;
    private AppSettings _settings;
    private DeviceStats? _latestStats;
    private int? _streamFps;
    private List<InstalledAppInfo> _installedApps = new();
    private Dictionary<string, InstalledAppInfo> _appLookup = new(StringComparer.OrdinalIgnoreCase);

    public MainWindow()
    {
        _settings = SettingsService.Load();
        _settings.FavoriteApps ??= new List<string>();
        _settings.RecentApps ??= new List<string>();
        _settings.AppProfiles ??= new Dictionary<string, AppLaunchProfile>(StringComparer.OrdinalIgnoreCase);
        MigrateSettings();
        Loc.SetLanguage(_settings.Language);
        ThemeService.Apply(_settings.Theme, _settings.NightLowContrast);
        InitializeComponent();

        // Read the actual version from build metadata instead of hardcoding a UI label.
        // InformationalVersion may include a build suffix (+hash); only show the release number.
        var currentVersion = typeof(MainWindow).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion.Split('+')[0]
            ?? typeof(MainWindow).Assembly.GetName().Version?.ToString(3)
            ?? "0.0.0";
        VersionCreditText.Text = $"v{currentVersion} © Ahmad Asyhari";

        var runtimeScrcpy = RuntimeLocator.FindScrcpy();
        var preferredRuntime = _settings.ExternalScrcpyPath;
        ScrcpyPathBox.Text = !string.IsNullOrWhiteSpace(preferredRuntime) && File.Exists(preferredRuntime)
            ? preferredRuntime
            : runtimeScrcpy ?? Path.Combine(AppContext.BaseDirectory, "runtime", "scrcpy", "scrcpy.exe");
        UpdateRuntimeStatus();

        // Separate the frequently needed phone-screen guard from expensive ADB monitoring.
        // Prevent DispatcherTimer ticks from stacking up when a Samsung dumpsys call is slow.
        _monitorTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(Math.Clamp(_settings.MonitorIntervalSeconds, 4, 15))
        };
        _monitorTimer.Tick += async (_, _) =>
        {
            if (_dashboardPollBusy) return;
            _dashboardPollBusy = true;
            try
            {
                await RefreshMonitorAsync();
                if (_session?.IsRunning == true &&
                    DateTime.UtcNow - _lastRunningAppsPollUtc >= TimeSpan.FromSeconds(8))
                    await RefreshRunningAppsAsync();
                UpdateTelemetrySurface();
            }
            finally { _dashboardPollBusy = false; }
        };
        _screenGuardTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _screenGuardTimer.Tick += async (_, _) =>
        {
            if (_session?.IsRunning == true) await EnforcePhoneScreenGuardAsync();
        };

        Loaded += async (_, _) =>
        {
            LoadSettingsIntoUi();
            Loc.Apply(this);
            ApplyUiLayout(_settings.UiLayout == "compact", resizeWindow: true);
            ThemeService.ApplyWindowPalette(this);
            ThemeService.ApplyWindowChrome(this);
            await RefreshDevicesAsync();
            _monitorTimer.Start();
            _screenGuardTimer.Start();
        };
        Closing += MainWindow_Closing;
    }

    private void MigrateSettings()
    {
        var changed = false;
        if (_settings.SettingsSchemaVersion < 13)
        {
            // v0.12 exposed codec+bitrate as one telemetry switch. Preserve that behavior.
            if (_settings.ShowCodecTelemetry) _settings.ShowBitrateTelemetry = true;

            if (string.Equals(_settings.GameWindowMode, "fullscreen", StringComparison.OrdinalIgnoreCase))
                _settings.GameWindowMode = "maximized";

            foreach (var profile in _settings.AppProfiles.Values)
                if (profile is not null && string.Equals(profile.WindowMode, "fullscreen", StringComparison.OrdinalIgnoreCase))
                    profile.WindowMode = "maximized";

            _settings.SettingsSchemaVersion = 14;
            changed = true;
        }

        if (_settings.SettingsSchemaVersion < 15)
        {
            // Older versions shared one saved window-mode setting for the desktop and apps.
            _settings.DesktopWindowMode = _settings.GameWindowMode;
            _settings.SettingsSchemaVersion = 15;
            changed = true;
        }

        // JSON restores Dictionary with its default comparer; normalize package keys.
        _settings.AppProfiles = new Dictionary<string, AppLaunchProfile>(_settings.AppProfiles, StringComparer.OrdinalIgnoreCase);
        if (changed) SettingsService.Save(_settings);
    }

    private string SelectedSessionMode()
        => (SessionModeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "desktop";

    private bool IsMirroring => SelectedSessionMode() == "mirror";

    // scrcpy announces display IDs only for --new-display. Mirroring explicitly targets
    // display 0, without creating a display or invoking virtual-display setup.
    private int? ActiveDisplayId => _session?.IsRunning == true
        ? (IsMirroring ? 0 : _session.DisplayId)
        : null;

    private string SelectedGameWindowMode()
        => (GameWindowModeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "windowed";

    private string SelectedGamingPreset()
        => (GamingProfileCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "balanced";

    private InstalledAppInfo? SelectedApp() => ApplicationCombo.SelectedItem as InstalledAppInfo;

    private async void BrowseScrcpy_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = Loc.T("scrcpy executable|scrcpy.exe|Executable files|*.exe|All files|*.*"),
            FileName = "scrcpy.exe",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) != true) return;
        ScrcpyPathBox.Text = dialog.FileName;
        SaveExternalRuntimeIfValid();
        UpdateRuntimeStatus();
        await RefreshDevicesAsync();
    }

    private async void RefreshDevices_Click(object sender, RoutedEventArgs e) => await RefreshDevicesAsync();

    private async void DeviceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded && !_restoringDevices && DeviceCombo.SelectedItem is DeviceInfo selectedDevice &&
            _settings.PreferredDeviceSerial != selectedDevice.Serial)
        {
            _settings.PreferredDeviceSerial = selectedDevice.Serial;
            SettingsService.Save(_settings);
        }
        UpdateUsbStatus();
        await RefreshMonitorAsync(force: true);
        if (DeviceCombo.SelectedItem is DeviceInfo d && d.State.Equals("device", StringComparison.OrdinalIgnoreCase))
            await RefreshAppLibraryAsync(force: true);
    }

    private void SessionModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _loadingUi) return;
        var nextMode = SelectedSessionMode();

        if (_lastSessionMode != nextMode)
        {
            switch (_lastSessionMode)
            {
                case "desktop": SaveDesktopStreamSettingsFromControls(); break;
                case "app":
                    if (SelectedApp() is { } previousApp) SaveCurrentAppProfile(previousApp.PackageName);
                    break;
                case "mirror": SaveMirrorStreamSettingsFromControls(); break;
            }
            switch (nextMode)
            {
                case "desktop": ApplyDesktopStreamSettingsToControls(); break;
                case "app":
                    if (SelectedApp() is { } nextApp) ApplyStoredOrDefaultAppProfile(nextApp.PackageName);
                    else SelectTaggedItem(GameWindowModeCombo, _settings.GameWindowMode);
                    break;
                case "mirror": ApplyMirrorStreamSettingsToControls(); break;
            }
        }

        _lastSessionMode = nextMode;
        UpdateSessionModeUi();
        SaveSettingsFromUi();
    }

    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _loadingUi || LanguageCombo.SelectedItem is not ComboBoxItem choice) return;
        var language = choice.Tag?.ToString() == "en" ? "en" : "id";
        _settings.Language = language;
        Loc.SetLanguage(language);
        Loc.Apply(this);
        ApplyUiLayout(_settings.UiLayout == "compact", resizeWindow: false);
        // Recompute live labels without waiting for the next ADB polling tick.
        UpdateRuntimeStatus();
        UpdateUsbStatus();
        UpdateSessionModeUi();
        UpdatePhoneScreenButton();
        UpdateAppLibrarySummary();
        UpdateControllerStatus();
        if (_latestStats is { } stats) ApplyDeviceMonitorLabels(stats);
        else ClearMonitor();
        UpdateStreamHealth();
        UpdateTelemetrySurface();
        RunningAppsList.Items.Refresh();
        if (_session?.IsRunning == true && ActiveDisplayId is int displayId)
        {
            if (SelectedSessionMode() == "app") UpdateAppStatus(displayId);
            else if (IsMirroring) UpdateMirrorStatus();
            else UpdateRunningStatus(displayId, _lastFreeformOk);
            var count = RunningAppsList.Items.Count;
            RunningAppsSummaryText.Text = IsMirroring
                ? Loc.F("{0} app task(s) on phone screen", count)
                : Loc.F("{0} app task(s) on display {1}", count, displayId);
        }
        else if (_session?.IsRunning != true)
        {
            RunningAppsSummaryText.Text = Loc.T("No active session");
            if (DeviceCombo.SelectedItem is DeviceInfo device && device.State.Equals("device", StringComparison.OrdinalIgnoreCase))
                StatusText.Text = _installedApps.Count > 0
                    ? Loc.F("Galaxy ready · {0} apps available.", _installedApps.Count)
                    : Loc.T("Galaxy ready.");
            else if (DeviceCombo.SelectedItem is not DeviceInfo)
                StatusText.Text = Loc.T("Connect a Galaxy and enable USB debugging.");
            else
                StatusText.Text = Loc.T("Galaxy detected — authorization required.");
        }
        SaveSettingsFromUi();
    }

    private void UpdateSessionModeUi()
    {
        var appMode = SelectedSessionMode().Equals("app", StringComparison.OrdinalIgnoreCase);
        var mirrorMode = IsMirroring;
        var idle = _session?.IsRunning != true;
        var appVisibility = appMode ? Visibility.Visible : Visibility.Collapsed;
        var desktopVisibility = !appMode && !mirrorMode ? Visibility.Visible : Visibility.Collapsed;

        DesktopProfileLabel.Visibility = desktopVisibility;
        PresetCombo.Visibility = desktopVisibility;
        PresetCombo.IsEnabled = !appMode && !mirrorMode && idle;


        ApplicationLabel.Visibility = appVisibility;
        ApplicationCombo.Visibility = appVisibility;
        ApplicationActionsPanel.Visibility = appVisibility;
        ApplicationSearchLabel.Visibility = appVisibility;
        ApplicationSearchBox.Visibility = appVisibility;
        GamingProfileLabel.Visibility = appVisibility;
        GamingProfileCombo.Visibility = appVisibility;
        AppLibrarySummaryText.Visibility = appVisibility;

        ApplicationCombo.IsEnabled = appMode && idle;
        FavoriteAppButton.IsEnabled = appMode && idle && SelectedApp() is not null;
        RefreshAppsButton.IsEnabled = appMode && idle;
        GamingProfileCombo.IsEnabled = appMode && idle;
        ApplicationSearchBox.IsEnabled = appMode && idle;
        StartButton.Content = appMode ? Loc.T("Start App") : mirrorMode ? Loc.T("Start Mirroring") : Loc.T("Start DeX");

        // A physical-screen mirror must never change the Android display geometry,
        // power off display 0 via ADB, or hand off/clean up the phone's own tasks.
        ResolutionCombo.IsEnabled = !mirrorMode && idle;
        DpiCombo.IsEnabled = !mirrorMode && idle;
        ForceDpiCheck.IsEnabled = !mirrorMode && idle;
        TurnPhoneScreenOffCheck.IsEnabled = !mirrorMode && idle;
        HandoffDexAppsOnStopCheck.IsEnabled = !mirrorMode && idle;
        CloseFailedHandoffTasksCheck.IsEnabled = !mirrorMode && idle;
        GameWindowModeCombo.IsEnabled = idle;
        // Keep explanatory guidance in hover tooltips instead of filling the dashboard.
        SessionModeCombo.ToolTip = mirrorMode
            ? Loc.T("Mirror the phone screen (display 0).")
            : appMode ? Loc.T("Open one app directly on a virtual display.")
                      : Loc.T("Open the Samsung DeX desktop.");
        DisplayStreamGroup.ToolTip = mirrorMode
            ? Loc.T("Mirroring uses the phone's resolution and DPI. Encoding options still apply.")
            : Loc.T("Resolution and DPI change the virtual display. New settings apply on the next session.");
        PhoneScreenButton.IsEnabled = !mirrorMode && _session?.IsRunning == true && ActiveDisplayId.HasValue;
        UpdatePhoneScreenButton();

        UpdateAppLibrarySummary();
        UpdateControllerStatus();
    }

    private void UpdateAppLibrarySummary()
    {
        if (AppLibrarySummaryText is null) return;
        if (_installedApps.Count == 0)
        {
            AppLibrarySummaryText.Text = Loc.T("App library not loaded");
            return;
        }
        var favoriteCount = _installedApps.Count(a => a.IsFavorite);
        var recentCount = _installedApps.Count(a => a.IsRecent);
        AppLibrarySummaryText.Text = Loc.F("{0} apps · ★ {1} favorites · ↺ {2} recent", _installedApps.Count, favoriteCount, recentCount);
    }

    private void UpdateControllerStatus()
    {
        if (ControllerStatusText is null) return;
        if (_session?.IsRunning == true && GamepadCheck.IsChecked == true)
        {
            ControllerStatusText.Text = Loc.T("Active");
            ControllerStatusText.Foreground = (Brush)FindResource("GoodBrush");
        }
        else if (GamepadCheck.IsChecked == true)
        {
            ControllerStatusText.Text = Loc.T("Enabled for next session");
            ControllerStatusText.Foreground = (Brush)FindResource("SecondaryTextBrush");
        }
        else
        {
            ControllerStatusText.Text = Loc.T("Off");
            ControllerStatusText.Foreground = (Brush)FindResource("SecondaryTextBrush");
        }
    }

    private async Task RefreshDevicesAsync()
    {
        try
        {
            var scrcpyPath = ScrcpyPathBox.Text.Trim();
            var adbPath = RuntimeLocator.FindAdb(scrcpyPath);
            if (string.IsNullOrWhiteSpace(adbPath) || !File.Exists(adbPath))
            {
                DeviceCombo.ItemsSource = null;
                RuntimeStatusText.Text = Loc.T("Runtime: ADB missing");
                UsbStatusText.Text = Loc.T("USB debugging: unavailable");
                StatusText.Text = Loc.T("DexBridge runtime is incomplete.");
                return;
            }

            _adb = new AdbService(adbPath);
            var devices = await _adb.GetDevicesAsync();
            var previousSerial = (DeviceCombo.SelectedItem as DeviceInfo)?.Serial;
            _restoringDevices = true;
            try
            {
                DeviceCombo.ItemsSource = devices;
                var wantedSerial = !string.IsNullOrWhiteSpace(previousSerial)
                    ? previousSerial : _settings.PreferredDeviceSerial;
                if (!string.IsNullOrWhiteSpace(wantedSerial))
                    DeviceCombo.SelectedItem = devices.FirstOrDefault(d => d.Serial == wantedSerial);
                if (DeviceCombo.SelectedIndex < 0 && devices.Count > 0) DeviceCombo.SelectedIndex = 0;
            }
            finally { _restoringDevices = false; }

            StatusText.Text = devices.Count switch
            {
                0 => Loc.T("Connect a Galaxy and enable USB debugging."),
                1 => devices[0].State.Equals("device", StringComparison.OrdinalIgnoreCase)
                    ? Loc.T("Galaxy ready.")
                    : Loc.T("Galaxy detected — authorization required."),
                _ => Loc.F("{0} ADB devices detected.", devices.Count)
            };

            UpdateRuntimeStatus();
            UpdateUsbStatus();
            await RefreshMonitorAsync(force: true);
            if (DeviceCombo.SelectedItem is DeviceInfo selected && selected.State.Equals("device", StringComparison.OrdinalIgnoreCase))
                await RefreshAppLibraryAsync(force: _installedApps.Count == 0);
        }
        catch (Exception ex)
        {
            StatusText.Text = Loc.T("Device detection failed.");
            UsbStatusText.Text = Loc.T("USB debugging: unavailable");
            AppendLog("ERROR: " + ex.Message);
        }
    }

    private void UpdateRuntimeStatus()
    {
        var path = ScrcpyPathBox.Text.Trim();
        if (!File.Exists(path))
        {
            RuntimeStatusText.ToolTip = Loc.T("Runtime: missing");
            RuntimeStatusText.Text = Loc.T("Runtime missing");
            return;
        }
        var bundled = RuntimeLocator.IsBundledRuntime(path);
        RuntimeStatusText.ToolTip = Loc.T(bundled ? "Runtime: bundled scrcpy + ADB" : "Runtime: external scrcpy");
        RuntimeStatusText.Text = Loc.T(bundled ? "scrcpy + ADB" : "External scrcpy");
    }

    private void UpdateUsbStatus()
    {
        if (DeviceCombo.SelectedItem is not DeviceInfo device)
        {
            UsbStatusText.ToolTip = Loc.T("USB debugging: no device");
            UsbStatusText.Text = Loc.T("USB: no device");
            FilesButton.IsEnabled = false;
            return;
        }

        UsbStatusText.ToolTip = device.State.ToLowerInvariant() switch
        {
            "device" => Loc.T("USB debugging: authorized"),
            "unauthorized" => Loc.T("USB debugging: authorize this PC on the phone"),
            "offline" => Loc.T("USB debugging: device offline — reconnect USB"),
            _ => Loc.F("USB debugging: {0}", device.State)
        };
        UsbStatusText.Text = device.State.ToLowerInvariant() switch
        {
            "device" => Loc.T("USB ready"),
            "unauthorized" => Loc.T("USB: authorization required"),
            "offline" => Loc.T("USB: offline"),
            _ => Loc.F("USB debugging: {0}", device.State)
        };
        FilesButton.IsEnabled = device.State.Equals("device", StringComparison.OrdinalIgnoreCase);
    }

    private async void RefreshApps_Click(object sender, RoutedEventArgs e) => await RefreshAppLibraryAsync(force: true);

    private async Task RefreshAppLibraryAsync(bool force = false)
    {
        if (_appLibraryBusy) return;
        if (!force && _installedApps.Count > 0) return;
        if (DeviceCombo.SelectedItem is not DeviceInfo device || !device.State.Equals("device", StringComparison.OrdinalIgnoreCase)) return;

        _appLibraryBusy = true;
        RefreshAppsButton.IsEnabled = false;
        var previousPackage = SelectedApp()?.PackageName ?? _selectedAppPackage ?? _settings.GamePackage;
        try
        {
            StatusText.Text = Loc.T("Reading installed app library…");
            IReadOnlyList<InstalledAppInfo> apps = Array.Empty<InstalledAppInfo>();
            var scrcpyPath = ScrcpyPathBox.Text.Trim();
            if (File.Exists(scrcpyPath))
            {
                try
                {
                    apps = await new ScrcpyAppDiscoveryService(scrcpyPath).ListAppsAsync(device.Serial);
                }
                catch (Exception ex)
                {
                    AppendLog("APP LIBRARY scrcpy fallback: " + ex.Message);
                }
            }

            if (apps.Count == 0 && _adb is not null)
                apps = await _adb.GetLaunchableAppsFallbackAsync(device.Serial);

            _installedApps = apps
                .GroupBy(a => a.PackageName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
            ApplyAppFlagsAndSort();
            SelectAppByPackage(previousPackage);
            StatusText.Text = _installedApps.Count > 0
                ? Loc.F("Galaxy ready · {0} apps available.", _installedApps.Count)
                : Loc.T("Galaxy ready · app library unavailable.");
        }
        catch (Exception ex)
        {
            AppendLog("APP LIBRARY ERROR: " + ex.Message);
            StatusText.Text = Loc.T("Could not read the Android app library.");
        }
        finally
        {
            _appLibraryBusy = false;
            RefreshAppsButton.IsEnabled = SelectedSessionMode() == "app" && _session?.IsRunning != true;
        }
    }

    private void ApplyAppFlagsAndSort()
    {
        var recentOrder = _settings.RecentApps
            .Select((pkg, index) => new { pkg, index })
            .ToDictionary(x => x.pkg, x => x.index, StringComparer.OrdinalIgnoreCase);

        foreach (var app in _installedApps)
        {
            app.IsFavorite = _settings.FavoriteApps.Any(p => p.Equals(app.PackageName, StringComparison.OrdinalIgnoreCase));
            app.IsRecent = recentOrder.ContainsKey(app.PackageName);
        }

        _installedApps = _installedApps
            .OrderByDescending(a => a.IsFavorite)
            .ThenBy(a => recentOrder.TryGetValue(a.PackageName, out var i) ? i : int.MaxValue)
            .ThenBy(a => a.IsSystem)
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        _appLookup = _installedApps.ToDictionary(a => a.PackageName, StringComparer.OrdinalIgnoreCase);
        ApplyAppFilter();
        UpdateAppLibrarySummary();
    }

    private void ApplicationSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        => ApplyAppFilter();

    private void ApplyAppFilter()
    {
        if (ApplicationCombo is null) return;
        var desiredPackage = _selectedAppPackage ?? _settings.GamePackage;
        var query = ApplicationSearchBox?.Text?.Trim() ?? string.Empty;
        IEnumerable<InstalledAppInfo> source = _installedApps;
        if (!string.IsNullOrWhiteSpace(query))
        {
            source = source.Where(a =>
                a.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || a.PackageName.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        var filtered = source.ToList();
        _suppressAppSelection = true;
        try
        {
            ApplicationCombo.ItemsSource = null;
            ApplicationCombo.ItemsSource = filtered;
            if (!string.IsNullOrWhiteSpace(desiredPackage))
                ApplicationCombo.SelectedItem = filtered.FirstOrDefault(a => a.PackageName.Equals(desiredPackage, StringComparison.OrdinalIgnoreCase));
            if (ApplicationCombo.SelectedIndex < 0 && filtered.Count > 0 && string.IsNullOrWhiteSpace(query) && string.IsNullOrWhiteSpace(desiredPackage))
                ApplicationCombo.SelectedIndex = 0;
        }
        finally { _suppressAppSelection = false; }
    }

    private void SelectAppByPackage(string? package)
    {
        var target = string.IsNullOrWhiteSpace(package)
            ? _installedApps.FirstOrDefault()
            : _installedApps.FirstOrDefault(a => a.PackageName.Equals(package, StringComparison.OrdinalIgnoreCase));

        _suppressAppSelection = true;
        try
        {
            ApplicationCombo.SelectedItem = target;
            _selectedAppPackage = target?.PackageName;
        }
        finally { _suppressAppSelection = false; }

        FavoriteAppButton.Content = target?.IsFavorite == true ? "★" : "☆";
        if (target is not null && SelectedSessionMode() == "app") ApplyStoredOrDefaultAppProfile(target.PackageName);
    }

    private void ApplicationCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _applyingAppProfile || _suppressAppSelection) return;
        var app = SelectedApp();
        if (app is null) return;

        if (!string.IsNullOrWhiteSpace(_selectedAppPackage)
            && !_selectedAppPackage.Equals(app.PackageName, StringComparison.OrdinalIgnoreCase))
        {
            SaveCurrentAppProfile(_selectedAppPackage);
        }

        _selectedAppPackage = app.PackageName;
        _settings.GamePackage = app.PackageName;
        FavoriteAppButton.Content = app.IsFavorite ? "★" : "☆";
        if (SelectedSessionMode() == "app") ApplyStoredOrDefaultAppProfile(app.PackageName);
        SaveSettingsFromUi();
    }

    private void FavoriteApp_Click(object sender, RoutedEventArgs e)
    {
        var app = SelectedApp();
        if (app is null) return;
        if (_settings.FavoriteApps.Any(p => p.Equals(app.PackageName, StringComparison.OrdinalIgnoreCase)))
            _settings.FavoriteApps.RemoveAll(p => p.Equals(app.PackageName, StringComparison.OrdinalIgnoreCase));
        else
            _settings.FavoriteApps.Insert(0, app.PackageName);
        SettingsService.Save(_settings);
        var package = app.PackageName;
        ApplyAppFlagsAndSort();
        SelectAppByPackage(package);
        FavoriteAppButton.Content = SelectedApp()?.IsFavorite == true ? "★" : "☆";
    }

    private void GamingProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _applyingPreset || _applyingAppProfile) return;
        ApplyGamingPreset(SelectedGamingPreset());
        SaveSettingsFromUi();
    }

    // Used by both Desktop and App/Game: changing a preset changes ALL coupled
    // geometry/encoding parameters rather than keeping stale bitrate/codec values.
    private void ApplyStreamPreset(string presetId)
    {
        if (!StreamPresetCatalog.TryGet(presetId, out var preset)) return;
        _applyingPreset = true;
        try
        {
            SelectComboItem(ResolutionCombo, preset.Resolution);
            SelectComboItem(DpiCombo, preset.Dpi);
            SelectComboItem(FpsCombo, preset.Fps);
            SelectComboItem(BitrateCombo, preset.Bitrate);
            SelectComboItem(CodecCombo, preset.Codec);
        }
        finally { _applyingPreset = false; }
    }

    private void ApplyGamingPreset(string preset) => ApplyStreamPreset(preset);

    private AppLaunchProfile? FindAppProfile(string package)
    {
        foreach (var pair in _settings.AppProfiles)
            if (pair.Key.Equals(package, StringComparison.OrdinalIgnoreCase)) return pair.Value;
        return null;
    }

    private void ApplyStoredOrDefaultAppProfile(string package)
    {
        var profile = FindAppProfile(package);
        if (profile is not null)
        {
            ApplyStoredAppProfile(package);
            return;
        }

        _applyingAppProfile = true;
        try
        {
            // Match the user's tested 1080p/240 DPI HEVC YouTube quality profile.
            // Existing saved profiles always take precedence; media portrait remains
            // selectable for phone-style watch-page/comments when necessary.
            var firstUsePreset = package.Equals("com.google.android.youtube", StringComparison.OrdinalIgnoreCase)
                ? "youtube-quality"
                : package.Equals("com.sec.android.app.music", StringComparison.OrdinalIgnoreCase)
                    ? "media-landscape" : "balanced";
            SelectTaggedItem(GamingProfileCombo, firstUsePreset);
            ApplyGamingPreset(firstUsePreset);
            SelectTaggedItem(GameWindowModeCombo, "windowed");
            GamepadCheck.IsChecked = false;
        }
        finally { _applyingAppProfile = false; }
        UpdateControllerStatus();
    }

    private void ApplyStoredAppProfile(string package)
    {
        var profile = FindAppProfile(package);
        if (profile is null) return;
        _applyingAppProfile = true;
        try
        {
            SelectTaggedItem(GamingProfileCombo, profile.GamingPreset);
            SelectComboItem(ResolutionCombo, profile.Resolution);
            SelectComboItem(DpiCombo, profile.Dpi);
            SelectComboItem(FpsCombo, profile.Fps);
            SelectComboItem(BitrateCombo, profile.Bitrate);
            SelectComboItem(CodecCombo, profile.VideoCodec);
            SelectTaggedItem(GameWindowModeCombo, profile.WindowMode);
            GamepadCheck.IsChecked = profile.GamepadForwarding;
        }
        finally { _applyingAppProfile = false; }
        UpdateControllerStatus();
    }

    private void SaveCurrentAppProfile(string package)
    {
        if (string.IsNullOrWhiteSpace(package)) return;
        _settings.AppProfiles[package] = new AppLaunchProfile
        {
            GamingPreset = SelectedGamingPreset(),
            Resolution = ComboValue(ResolutionCombo),
            Dpi = ComboValue(DpiCombo),
            Fps = ComboValue(FpsCombo),
            Bitrate = ComboValue(BitrateCombo),
            VideoCodec = ComboValue(CodecCombo),
            WindowMode = SelectedGameWindowMode(),
            GamepadForwarding = GamepadCheck.IsChecked == true,
        };
    }

    private string InferGamingPresetFromControls()
    {
        var resolution = ComboValue(ResolutionCombo);
        var dpi = ComboValue(DpiCombo);
        var fps = ComboValue(FpsCombo);
        var bitrate = ComboValue(BitrateCombo);
        var codec = ComboValue(CodecCombo);
        // Preserve the selected name when two profiles intentionally share stream
        // parameters (e.g. YouTube Balanced and generic Media Landscape).
        if (StreamPresetCatalog.TryGet(SelectedGamingPreset(), out var selectedPreset)
            && StreamPresetCatalog.Matches(selectedPreset, resolution, dpi, fps, bitrate, codec))
            return selectedPreset.Id;
        foreach (var entry in GamingProfileCombo.Items)
            if (entry is ComboBoxItem item && item.Tag?.ToString() is { } id
                && StreamPresetCatalog.TryGet(id, out var preset)
                && StreamPresetCatalog.Matches(preset, resolution, dpi, fps, bitrate, codec))
                return id;
        return "custom";
    }

    private void StreamSetting_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _loadingUi || _applyingPreset || _applyingAppProfile) return;

        if (SelectedSessionMode() == "app")
        {
            var inferred = InferGamingPresetFromControls();
            if (!SelectedGamingPreset().Equals(inferred, StringComparison.OrdinalIgnoreCase))
            {
                _applyingPreset = true;
                try { SelectTaggedItem(GamingProfileCombo, inferred); }
                finally { _applyingPreset = false; }
            }
            if (SelectedApp() is { } app) SaveCurrentAppProfile(app.PackageName);
        }
        else if (SelectedSessionMode() == "desktop")
        {
            _applyingPreset = true;
            try { SelectPresetForSettings(ComboValue(ResolutionCombo), ComboValue(DpiCombo)); }
            finally { _applyingPreset = false; }
        }

        SaveSettingsFromUi();
        UpdateStreamHealth();
        UpdateTelemetrySurface();
    }

    private void AppWindowMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _loadingUi || _applyingAppProfile) return;
        if (SelectedSessionMode() == "app" && SelectedApp() is { } app) SaveCurrentAppProfile(app.PackageName);
        SaveSettingsFromUi();
    }

    private void GamepadCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || _loadingUi || _applyingAppProfile) return;
        UpdateControllerStatus();
        if (SelectedSessionMode() == "app" && SelectedApp() is { } app) SaveCurrentAppProfile(app.PackageName);
        SaveSettingsFromUi();
    }

    private void TelemetryOption_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || _loadingUi) return;
        SaveSettingsFromUi();
        UpdateTelemetrySurface();
        UpdateStreamHealth();
    }

    private void PersistentOption_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || _loadingUi) return;
        SaveSettingsFromUi();
    }

    private void SettingsChoice_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _loadingUi) return;
        SaveSettingsFromUi();
    }

    private void ScrcpyPathBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded || _loadingUi) return;
        SaveExternalRuntimeIfValid();
        UpdateRuntimeStatus();
    }

    private void SaveExternalRuntimeIfValid()
    {
        var path = ScrcpyPathBox.Text.Trim();
        if (!File.Exists(path)) return;
        var externalPath = RuntimeLocator.IsBundledRuntime(path) ? string.Empty : path;
        if (_settings.ExternalScrcpyPath == externalPath) return;
        _settings.ExternalScrcpyPath = externalPath;
        SettingsService.Save(_settings);
    }

    private void ThermalDetails_Click(object sender, RoutedEventArgs e)
    {
        if (_latestStats is null)
        {
            AppendLog("THERMAL: no device sample available.");
            return;
        }

        AppendLog("=== THERMAL DIAGNOSTICS ===");
        AppendLog(_latestStats.ThermalDiagnostics);
        if (!string.IsNullOrWhiteSpace(_latestStats.ThermalRawDump))
        {
            foreach (var line in _latestStats.ThermalRawDump.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                AppendLog("THERMAL RAW: " + line);
        }
        AppendLog("=== END THERMAL DIAGNOSTICS ===");
        StatusText.Text = Loc.T("Thermal diagnostics written to Session log.");
    }

    private async void RefreshMonitor_Click(object sender, RoutedEventArgs e) => await RefreshMonitorAsync(force: true);

    private async Task RefreshMonitorAsync(bool force = false)
    {
        if (_monitorBusy) return;
        if (_adb is null || DeviceCombo.SelectedItem is not DeviceInfo device || !device.State.Equals("device", StringComparison.OrdinalIgnoreCase))
        {
            if (force) ClearMonitor();
            return;
        }

        _monitorBusy = true;
        try
        {
            var stats = await _adb.GetDeviceStatsAsync(device.Serial);
            // Ignore a late ADB reply for a device which is no longer selected.
            if ((DeviceCombo.SelectedItem as DeviceInfo)?.Serial != device.Serial) return;
            _latestStats = stats;
            TrackThermalPressure(stats);
            ApplyDeviceMonitorLabels(stats);
            UpdateStreamHealth();
            UpdateTelemetrySurface();
        }
        catch (Exception ex)
        {
            if (force) ClearMonitor();
            if (force || DateTime.UtcNow - _lastMonitorWarningUtc >= TimeSpan.FromSeconds(30))
            {
                AppendLog("MONITOR WARNING: " + ex.Message);
                _lastMonitorWarningUtc = DateTime.UtcNow;
            }
        }
        finally { _monitorBusy = false; }
    }

    // Labels are recomputed during a language change from the cached sample.
    private void ApplyDeviceMonitorLabels(DeviceStats stats)
    {
            CpuUsageText.Text = stats.CpuUsagePercent.HasValue ? $"{stats.CpuUsagePercent.Value:0}%" : "…";
            CpuTempText.Text = stats.CpuTemperatureC.HasValue ? Loc.F("CPU temp {0:0.#} °C", stats.CpuTemperatureC.Value) : Loc.T("CPU temp N/A");
            RamUsageText.Text = stats.RamUsedPercent.HasValue ? $"{stats.RamUsedPercent.Value:0}%" : "—";
            RamDetailText.Text = stats.RamUsedGb.HasValue && stats.RamTotalGb.HasValue ? $"{stats.RamUsedGb.Value:0.0} / {stats.RamTotalGb.Value:0.0} GB" : Loc.T("RAM N/A");
            BatteryLevelText.Text = stats.BatteryLevel.HasValue ? $"{stats.BatteryLevel.Value}%" : "—";
            BatteryTempText.Text = stats.BatteryTemperatureC.HasValue ? Loc.F("Battery {0:0.#} °C", stats.BatteryTemperatureC.Value) : Loc.T("Battery temp N/A");
            ThermalStatusText.Text = ThermalStatusName(stats.ThermalStatus);
            ThermalStatusText.ToolTip = stats.ThermalDiagnostics;
            SkinTempText.Text = stats.SkinTemperatureC.HasValue
                ? Loc.F("Skin {0:0.#} °C · sensor {1}", stats.SkinTemperatureC.Value, ThermalStatusName(stats.MaxSensorThermalStatus))
                : stats.GpuTemperatureC.HasValue ? $"GPU {stats.GpuTemperatureC.Value:0.#} °C" : Loc.T("Skin temp N/A");
            ApplyThermalColor(stats.ThermalStatus);
    }

    private void TrackThermalPressure(DeviceStats stats)
    {
        // Current Android framework throttling status only: no threshold guessing.
        if (_session?.IsRunning != true || stats.ThermalStatus is null || stats.ThermalStatus < 3)
        {
            _sustainedThermalSinceUtc = null;
            return;
        }

        var now = DateTime.UtcNow;
        _sustainedThermalSinceUtc ??= now;
        if (now - _sustainedThermalSinceUtc.Value < TimeSpan.FromSeconds(20) ||
            now - _lastThermalAdvisoryUtc < TimeSpan.FromMinutes(2)) return;

        _lastThermalAdvisoryUtc = now;
        AppendLog($"THERMAL ADVISORY: {ThermalStatusName(stats.ThermalStatus)} persisted for 20+ seconds. " +
                  "Consider a lighter profile for the NEXT session; DexBridge will not change an active stream.");
    }

    private void ApplyThermalColor(int? status)
    {
        ThermalStatusText.Foreground = status switch
        {
            >= 4 => (Brush)FindResource("CriticalBrush"),
            2 or 3 => (Brush)FindResource("WarningBrush"),
            0 or 1 => (Brush)FindResource("GoodBrush"),
            _ => (Brush)FindResource("PrimaryTextBrush")
        };
    }

    private static string ThermalStatusName(int? status)
    {
        // "Light" means light intensity for the theme, but mild severity here.
        // Keep the two translation contexts separate.
        if (status == 1) return Loc.Language == "id" ? "Ringan" : "Light";
        return Loc.T(status switch
        {
            0 => "Normal", 2 => "Moderate", 3 => "Severe", 4 => "Critical",
            5 => "Emergency", 6 => "Shutdown", _ => "N/A"
        });
    }

    private void ClearMonitor()
    {
        _latestStats = null;
        CpuUsageText.Text = "—"; CpuTempText.Text = Loc.T("CPU temp —");
        RamUsageText.Text = "—"; RamDetailText.Text = "—";
        BatteryLevelText.Text = "—"; BatteryTempText.Text = Loc.T("Temp —");
        ThermalStatusText.Text = "—"; SkinTempText.Text = Loc.T("Skin —");
        StreamFpsText.Text = "—"; StreamHealthText.Text = Loc.T("No active stream");
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (_session?.IsRunning == true) return;
        var scrcpyPath = ScrcpyPathBox.Text.Trim();
        if (!File.Exists(scrcpyPath))
        {
            MessageBox.Show(this, Loc.T("DexBridge runtime is missing. Build the portable package or select an external scrcpy.exe in Advanced settings."), "DexBridge", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _activeDevice = DeviceCombo.SelectedItem as DeviceInfo;
        if (_activeDevice is null)
        {
            MessageBox.Show(this, Loc.T("Connect a Galaxy device over USB first."), "DexBridge", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!_activeDevice.State.Equals("device", StringComparison.OrdinalIgnoreCase))
        {
            var msg = _activeDevice.State.Equals("unauthorized", StringComparison.OrdinalIgnoreCase)
                ? Loc.T("Unlock the phone, accept 'Allow USB debugging', and select 'Always allow from this computer'.")
                : Loc.F("The Galaxy ADB state is '{0}'. Reconnect USB and press Refresh.", _activeDevice.State);
            MessageBox.Show(this, msg, Loc.T("USB debugging"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var adbPath = RuntimeLocator.FindAdb(scrcpyPath);
        if (string.IsNullOrWhiteSpace(adbPath) || !File.Exists(adbPath))
        {
            MessageBox.Show(this, Loc.T("adb.exe was not found in the DexBridge runtime."), "DexBridge", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        _adb ??= new AdbService(adbPath);

        var appMode = SelectedSessionMode().Equals("app", StringComparison.OrdinalIgnoreCase);
        var app = SelectedApp();
        if (appMode && app is null)
        {
            MessageBox.Show(this, Loc.T("Select an installed application first. Press Apps to refresh the Galaxy app library."), Loc.T("App / Game session"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        LogBox.Clear();
        FpsLogBox.Clear();
        _sessionLogLineCount = 0;
        _fpsLogLineCount = 0;
        _recentStreamFps.Clear();
        _sustainedThermalSinceUtc = null;
        _lastThermalAdvisoryUtc = DateTime.MinValue;
        _lastRunningAppsPollUtc = DateTime.MinValue;
        _streamFps = null;
        _phoneScreenPoweredOff = false;
        _phoneScreenDesiredOff = false;
        UpdatePhoneScreenButton();

        var resolution = ComboValue(ResolutionCombo);
        var dpi = ComboValue(DpiCombo);
        var fps = ComboValue(FpsCombo);
        var bitrate = ComboValue(BitrateCombo);
        var codec = ComboValue(CodecCombo);
        var keyboard = ComboValue(KeyboardCombo);
        var mouse = ComboValue(MouseCombo);
        var appPackage = app?.PackageName ?? string.Empty;
        var mirrorMode = IsMirroring;
        // Screen mirror inherits the phone's physical geometry, not the virtual-display
        // resolution/DPI saved for Desktop or App/Game.
        if (!StreamPresetCatalog.Validate(mirrorMode ? "1920x1080" : resolution,
                mirrorMode ? "220" : dpi, fps, bitrate, codec, out var validationError))
        {
            MessageBox.Show(this, validationError, Loc.T("Invalid stream profile"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (appMode)
        {
            SaveCurrentAppProfile(appPackage);
            _settings.GamePackage = appPackage;
            _settings.RecentApps.RemoveAll(p => p.Equals(appPackage, StringComparison.OrdinalIgnoreCase));
            _settings.RecentApps.Insert(0, appPackage);
            if (_settings.RecentApps.Count > 12) _settings.RecentApps.RemoveRange(12, _settings.RecentApps.Count - 12);
        }

        var args = new StringBuilder();
        args.Append($"-s \"{_activeDevice.Serial}\" ");
        if (mirrorMode) args.Append("--display-id=0 ");
        else args.Append($"--new-display={resolution}/{dpi} ");
        args.Append($"--video-codec={codec} -b {bitrate}M --max-fps {fps} ");
        if (NoDownsizeCheck.IsChecked == true) args.Append("--no-downsize-on-error ");
        args.Append($"--keyboard={keyboard} --mouse={mouse} ");

        if (appMode)
        {
            args.Append("--no-vd-system-decorations ");
            args.Append($"--start-app={appPackage} ");
        }

        if (appMode || mirrorMode)
        {
            if (SelectedGameWindowMode() == "borderless") args.Append("--window-borderless --fullscreen ");
        }

        if (GamepadCheck.IsChecked == true) args.Append("--gamepad=uhid ");
        // Stream-health and telemetry always need the scrcpy stream FPS. FPS lines remain hidden
        // from Session log unless the user explicitly enables them.
        args.Append("--print-fps ");
        if (AudioCheck.IsChecked != true) args.Append("--no-audio ");
        if (mouse == "sdk" && NoHoverCheck.IsChecked == true) args.Append("--no-mouse-hover ");
        if (StayAwakeCheck.IsChecked == true) args.Append("--stay-awake ");
        if (!mirrorMode && HandoffDexAppsOnStopCheck.IsChecked == true) args.Append("--no-vd-destroy-content ");

        var baseTitle = appMode ? $"DexBridge · {app!.DisplayName}"
            : mirrorMode ? "Phone Mirror · DexBridge" : "Samsung DeX · DexBridge";
        args.Append($"--window-title=\"{baseTitle.Replace("\"", "'")}\"");

        try
        {
            if (!mirrorMode) await _adb.CleanupOverlayDisplayAsync(_activeDevice.Serial);
            _session = new ScrcpySession(scrcpyPath);
            _session.LogReceived += line => Dispatcher.Invoke(() =>
            {
                // FPS belongs in the dedicated FPS history panel.  Keep scrcpy command/output,
                // warnings and errors in Session log so debugging remains readable.
                if (!Regex.IsMatch(line, @"\b\d+\s+fps\b", RegexOptions.IgnoreCase)) AppendLog(line);
            });
            _session.FpsUpdated += value => Dispatcher.Invoke(() =>
            {
                _streamFps = value;
                _recentStreamFps.Enqueue(value);
                while (_recentStreamFps.Count > 15) _recentStreamFps.Dequeue();
                if (PrintFpsCheck.IsChecked == true) AppendFpsLog(value);
                UpdateStreamHealth();
                UpdateTelemetrySurface();
            });
            _session.DisplayCreated += id => Dispatcher.InvokeAsync(async () => await OnDisplayCreatedAsync(id));
            _session.Exited += code => Dispatcher.Invoke(() => OnSessionExited(code));

            AppendLog("> scrcpy " + args);
            _session.Start(args.ToString());
            SetSessionControls(running: true);
            StatusText.Text = appMode ? Loc.F("Starting {0}…", app!.DisplayName)
                : mirrorMode ? Loc.T("Starting phone screen mirroring…") : Loc.T("Starting DeX virtual display…");
            SaveSettingsFromUi();
            ApplyAppFlagsAndSort();
            SelectAppByPackage(appPackage);
            if (mirrorMode && _session is { } mirrorSession) await OnMirroringStartedAsync(mirrorSession);
        }
        catch (Exception ex)
        {
            AppendLog("ERROR: " + ex.Message);
            StatusText.Text = Loc.T("Failed to start session.");
            _session?.Stop(); _session = null;
            SetSessionControls(running: false);
        }
    }

    private void SetSessionControls(bool running)
    {
        StartButton.IsEnabled = !running;
        StopButton.IsEnabled = running;
        PhoneScreenButton.IsEnabled = running && !IsMirroring && _session?.DisplayId.HasValue == true;
        DeviceCombo.IsEnabled = !running;
        SessionModeCombo.IsEnabled = !running;
        ApplicationCombo.IsEnabled = !running && SelectedSessionMode() == "app";
        ApplicationSearchBox.IsEnabled = !running && SelectedSessionMode() == "app";
        RefreshAppsButton.IsEnabled = !running && SelectedSessionMode() == "app";
        FavoriteAppButton.IsEnabled = !running && SelectedSessionMode() == "app" && SelectedApp() is not null;
        GamepadCheck.IsEnabled = !running;
        GamingProfileCombo.IsEnabled = !running && SelectedSessionMode() == "app";
        PresetCombo.IsEnabled = !running && SelectedSessionMode() == "desktop";
        UpdateSessionModeUi();
        UpdateRunningAppsActions();
        UpdateControllerStatus();
    }

    // A mirror of display 0 does not emit scrcpy's "New display" event. Set up
    // monitoring and the existing running-app panel without DeX windowing commands.
    private async Task OnMirroringStartedAsync(ScrcpySession session)
    {
        await Task.Delay(750);
        if (!ReferenceEquals(_session, session) || !session.IsRunning || !IsMirroring) return;
        if (SelectedGameWindowMode() == "maximized" && !await session.MaximizeWindowAsync())
            AppendLog("WINDOW WARNING: could not maximize mirror window automatically.");
        UpdateMirrorStatus();
        await RefreshMonitorAsync(force: true);
        await RefreshRunningAppsAsync(force: true);
        UpdateTelemetrySurface();
    }

    private async Task OnDisplayCreatedAsync(int displayId)
    {
        if (_adb is null || _activeDevice is null) return;
        try
        {
            var appMode = SelectedSessionMode().Equals("app", StringComparison.OrdinalIgnoreCase);
            var targetWindowMode = appMode ? 1 : 5;
            AppendLog($"> adb shell wm set-display-windowing-mode -d {displayId} {targetWindowMode}");
            await Task.Delay(250);
            await _adb.SetDisplayWindowingModeAsync(_activeDevice.Serial, displayId, targetWindowMode);
            await Task.Delay(150);
            var mode = (await _adb.GetWindowingModeAsync(_activeDevice.Serial, displayId)).Trim();
            if (!string.IsNullOrWhiteSpace(mode)) AppendLog(mode);

            _phoneScreenDesiredOff = TurnPhoneScreenOffCheck.IsChecked == true;
            if (_phoneScreenDesiredOff)
            {
                AppendLog("> adb shell cmd display power-off 0");
                var result = await _adb.SetMainDisplayPowerAsync(_activeDevice.Serial, powerOn: false);
                AppendPowerDetails(result.Details);
                _phoneScreenPoweredOff = result.IsOn.HasValue ? !result.IsOn.Value : true;
            }
            UpdatePhoneScreenButton();
            PhoneScreenButton.IsEnabled = true;

            if (ForceDpiCheck.IsChecked == true && int.TryParse(ComboValue(DpiCombo), out var forcedDpi))
            {
                AppendLog($"> adb shell wm density {forcedDpi} -d {displayId}");
                var densitySet = (await _adb.SetDisplayDensityAsync(_activeDevice.Serial, displayId, forcedDpi)).Trim();
                if (!string.IsNullOrWhiteSpace(densitySet)) AppendLog(densitySet);
            }

            if (appMode && SelectedGameWindowMode() == "maximized" && _session is not null)
            {
                var maximized = await _session.MaximizeWindowAsync();
                if (!maximized) AppendLog("WINDOW WARNING: could not maximize scrcpy window automatically.");
            }

            if (appMode) UpdateAppStatus(displayId);
            else UpdateRunningStatus(displayId, mode.Contains("freeform", StringComparison.OrdinalIgnoreCase));

            await RefreshMonitorAsync(force: true);
            await RefreshRunningAppsAsync(force: true);
            await Task.Delay(350);
            UpdateTelemetrySurface();
        }
        catch (Exception ex)
        {
            AppendLog("SESSION SETUP ERROR: " + ex.Message);
            StatusText.Text = Loc.T("Session started, but setup was incomplete.");
        }
    }

    private void Files_Click(object sender, RoutedEventArgs e)
    {
        if (_adb is null || DeviceCombo.SelectedItem is not DeviceInfo device || !device.State.Equals("device", StringComparison.OrdinalIgnoreCase)) return;
        if (_phoneFilesWindow is { IsVisible: true }) { _phoneFilesWindow.Activate(); return; }
        _phoneFilesWindow = new PhoneFilesWindow(_adb, device.Serial) { Owner = this };
        _phoneFilesWindow.Closed += (_, _) => _phoneFilesWindow = null;
        _phoneFilesWindow.Show();
    }

    private async void PhoneScreen_Click(object sender, RoutedEventArgs e)
    {
        if (IsMirroring || _adb is null || _activeDevice is null || _session?.IsRunning != true) return;
        PhoneScreenButton.IsEnabled = false;
        try
        {
            // The button changes the desired state, not just the last observed state.  When the
            // desired state is OFF, the guard will keep display 0 off if One UI wakes it again.
            _phoneScreenDesiredOff = !_phoneScreenDesiredOff;
            var powerOn = !_phoneScreenDesiredOff;
            var result = await _adb.SetMainDisplayPowerAsync(_activeDevice.Serial, powerOn);
            AppendPowerDetails(result.Details);
            _phoneScreenPoweredOff = result.IsOn.HasValue ? !result.IsOn.Value : _phoneScreenDesiredOff;
            UpdatePhoneScreenButton();
        }
        catch (Exception ex) { AppendLog("PHONE SCREEN ERROR: " + ex.Message); }
        finally { PhoneScreenButton.IsEnabled = _session?.IsRunning == true; }
    }

    private async void Stop_Click(object sender, RoutedEventArgs e) => await StopSessionAsync();

    private async Task StopSessionAsync(bool closingApplication = false)
    {
        if (_stoppingSession) return;
        _stoppingSession = true;
        var mirrorMode = IsMirroring;
        StopButton.IsEnabled = false; PhoneScreenButton.IsEnabled = false;
        StatusText.Text = Loc.T("Stopping session safely…");
        var displayId = _session?.DisplayId;
        IReadOnlyList<RunningTaskInfo> preStopTasks = Array.Empty<RunningTaskInfo>();

        try
        {
            if (displayId.HasValue && _adb is not null && _activeDevice is not null)
            {
                try { preStopTasks = await _adb.GetRunningTasksAsync(_activeDevice.Serial, displayId.Value); }
                catch (Exception ex) { AppendLog("TASK SNAPSHOT WARNING: " + ex.Message); }
            }

            _phoneScreenDesiredOff = false;
            if (!mirrorMode)
            {
                await RestorePhoneScreenAsync();
                await Task.Delay(350);
            }
            var session = _session; _session = null;
            _telemetryOverlay?.Close(); _telemetryOverlay = null;
            if (session is not null)
            {
                AppendLog("> graceful scrcpy shutdown");
                await session.StopGracefullyAsync();
            }
            await Task.Delay(900);

            if (_adb is not null && _activeDevice is not null && HandoffDexAppsOnStopCheck.IsChecked == true && CloseFailedHandoffTasksCheck.IsChecked == true)
            {
                foreach (var task in preStopTasks)
                {
                    try
                    {
                        var ok = await _adb.EnsurePackageOnMainDisplayAsync(_activeDevice.Serial, task);
                        if (!ok) AppendLog($"  ! could not restore {task.PackageName} on display 0");
                    }
                    catch (Exception ex) { AppendLog($"  ! post-handoff check failed for {task.PackageName}: {ex.Message}"); }
                }
            }

            if (!mirrorMode && _adb is not null && _activeDevice is not null)
                try { await _adb.CleanupOverlayDisplayAsync(_activeDevice.Serial); } catch { }

            _streamFps = null;
            _recentStreamFps.Clear();
            _sustainedThermalSinceUtc = null;
            RunningAppsList.ItemsSource = null;
            RunningAppsSummaryText.Text = Loc.T("No active session");
            UpdateRunningAppsActions();
            StatusText.Text = mirrorMode ? Loc.T("Screen mirroring stopped. Phone apps remain open.") : Loc.T("Stopped · phone mode restored.");
            await RefreshMonitorAsync(force: true);
        }
        finally
        {
            SetSessionControls(running: false);
            if (closingApplication) StartButton.IsEnabled = false;
            _stoppingSession = false;
        }
    }

    private async void OnSessionExited(int exitCode)
    {
        AppendLog($"scrcpy exited with code {exitCode}.");
        if (_stoppingSession) return;
        var mirrorMode = IsMirroring;
        _phoneScreenDesiredOff = false;
        if (!mirrorMode) await RestorePhoneScreenAsync();
        _session = null;
        _recentStreamFps.Clear();
        _sustainedThermalSinceUtc = null;
        _telemetryOverlay?.Close(); _telemetryOverlay = null;
        SetSessionControls(running: false);
        RunningAppsList.ItemsSource = null;
        RunningAppsSummaryText.Text = Loc.T("No active session");
        UpdateRunningAppsActions();
        StatusText.Text = Loc.T("Session ended unexpectedly. Cleanup was attempted.");
        if (!mirrorMode && _adb is not null && _activeDevice is not null)
            try { await _adb.CleanupOverlayDisplayAsync(_activeDevice.Serial); } catch { }
    }

    private async Task RefreshRunningAppsAsync(bool force = false)
    {
        if (_runningAppsBusy) return;
        if (_adb is null || _activeDevice is null || _session?.IsRunning != true || !ActiveDisplayId.HasValue)
        {
            if (force) { RunningAppsList.ItemsSource = null; RunningAppsSummaryText.Text = Loc.T("No active session"); }
            UpdateRunningAppsActions();
            return;
        }
        _runningAppsBusy = true;
        _lastRunningAppsPollUtc = DateTime.UtcNow; // throttle even if an ADB request fails
        try
        {
            var session = _session;
            var displayId = ActiveDisplayId!.Value;
            var selectedId = (RunningAppsList.SelectedItem as RunningTaskInfo)?.TaskId;
            var tasks = await _adb.GetRunningTasksAsync(_activeDevice.Serial, displayId);
            // Session may have stopped while the Android activity query was in flight.
            if (!ReferenceEquals(session, _session) || _session?.IsRunning != true) return;
            _lastRunningAppsPollUtc = DateTime.UtcNow;
            var enriched = tasks.Select(EnrichTask).ToList();
            RunningAppsList.ItemsSource = enriched;
            if (selectedId.HasValue) RunningAppsList.SelectedItem = enriched.FirstOrDefault(t => t.TaskId == selectedId.Value);
            RunningAppsSummaryText.Text = enriched.Count == 0 ? Loc.T("No app tasks detected")
                : IsMirroring ? Loc.F("{0} app task(s) on phone screen", enriched.Count)
                : Loc.F("{0} app task(s) on display {1}", enriched.Count, displayId);
            UpdateRunningAppsActions();
        }
        catch (Exception ex)
        {
            if (force) RunningAppsSummaryText.Text = Loc.T("Running-app query failed");
            if (force || DateTime.UtcNow - _lastRunningAppsWarningUtc >= TimeSpan.FromSeconds(30))
            {
                AppendLog("RUNNING APPS WARNING: " + ex.Message);
                _lastRunningAppsWarningUtc = DateTime.UtcNow;
            }
        }
        finally { _runningAppsBusy = false; }
    }

    private RunningTaskInfo EnrichTask(RunningTaskInfo task)
    {
        _appLookup.TryGetValue(task.PackageName, out var app);
        return new RunningTaskInfo
        {
            TaskId = task.TaskId, DisplayId = task.DisplayId, PackageName = task.PackageName,
            ComponentName = task.ComponentName, IsVisible = task.IsVisible, IsFocused = task.IsFocused,
            AppName = app?.DisplayName ?? ScrcpyAppDiscoveryService.FriendlyNameFromPackage(task.PackageName),
        };
    }

    private async void RunningAppsList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_adb is null || _activeDevice is null || RunningAppsList.SelectedItem is not RunningTaskInfo task) return;
        var output = (await _adb.FocusTaskAsync(_activeDevice.Serial, task.TaskId)).Trim();
        if (!string.IsNullOrWhiteSpace(output)) AppendLog(output);
        await RefreshRunningAppsAsync(force: true);
    }

    private void RunningAppsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => UpdateRunningAppsActions();

    private void UpdateRunningAppsActions()
    {
        if (RunningAppsRefreshButton is null) return;
        var running = ActiveDisplayId.HasValue;
        var selected = running && RunningAppsList?.SelectedItem is RunningTaskInfo;
        var hasTasks = running && RunningAppsList?.Items.Count > 0;

        RunningAppsRefreshButton.IsEnabled = running;
        BringTaskFrontButton.IsEnabled = selected;
        MoveTaskPhoneButton.IsEnabled = selected && !IsMirroring;
        CloseTaskButton.IsEnabled = selected && !IsMirroring;
        // Never bulk-close all apps on the real phone display while mirroring.
        CloseAllTasksButton.IsEnabled = hasTasks && !IsMirroring;
    }

    private async void RefreshRunningApps_Click(object sender, RoutedEventArgs e) => await RefreshRunningAppsAsync(force: true);

    private async void BringTaskFront_Click(object sender, RoutedEventArgs e)
    {
        if (_adb is null || _activeDevice is null || RunningAppsList.SelectedItem is not RunningTaskInfo task) return;
        AppendLog($"> adb shell am task focus {task.TaskId}  # {task.PackageName}");
        var output = (await _adb.FocusTaskAsync(_activeDevice.Serial, task.TaskId)).Trim();
        if (!string.IsNullOrWhiteSpace(output)) AppendLog(output);
        await RefreshRunningAppsAsync(force: true);
    }

    private async void MoveTaskPhone_Click(object sender, RoutedEventArgs e)
    {
        if (IsMirroring || RunningAppsList.SelectedItem is not RunningTaskInfo task) return;
        await MoveTaskToPhoneAsync(task, closeOnFailure: false);
        await RefreshRunningAppsAsync(force: true);
    }

    private async void CloseTask_Click(object sender, RoutedEventArgs e)
    {
        if (IsMirroring || _adb is null || _activeDevice is null || RunningAppsList.SelectedItem is not RunningTaskInfo task) return;
        AppendLog($"> close task {task.TaskId}  # {task.PackageName}");
        await _adb.RemoveTaskAsync(_activeDevice.Serial, task.TaskId);
        await RefreshRunningAppsAsync(force: true);
    }

    private async void CloseAllTasks_Click(object sender, RoutedEventArgs e)
    {
        if (IsMirroring) return;
        if (_session?.DisplayId is not int displayId) return;
        await CloseDexTasksAsync(displayId, logEachTask: true);
        await RefreshRunningAppsAsync(force: true);
    }

    private async Task<bool> MoveTaskToPhoneAsync(RunningTaskInfo task, bool closeOnFailure)
    {
        if (_adb is null || _activeDevice is null) return false;
        AppendLog($"> handoff task {task.TaskId} to display 0  # {task.PackageName}");
        try
        {
            var result = await _adb.HandoffTaskToMainDisplayAsync(_activeDevice.Serial, task);
            if (result.Success) return true;
            if (closeOnFailure) await _adb.RemoveTaskAsync(_activeDevice.Serial, task.TaskId);
        }
        catch (Exception ex)
        {
            AppendLog($"HANDOFF ERROR: {ex.Message}");
            if (closeOnFailure) try { await _adb.RemoveTaskAsync(_activeDevice.Serial, task.TaskId); } catch { }
        }
        return false;
    }

    private async Task CloseDexTasksAsync(int displayId, bool logEachTask)
    {
        if (_adb is null || _activeDevice is null) return;
        try
        {
            var tasks = await _adb.GetRunningTasksAsync(_activeDevice.Serial, displayId);
            foreach (var task in tasks)
            {
                if (logEachTask) AppendLog($"> close task {task.TaskId}  # {task.PackageName}");
                await _adb.RemoveTaskAsync(_activeDevice.Serial, task.TaskId);
                await Task.Delay(80);
            }
        }
        catch (Exception ex) { AppendLog("TASK CLEANUP WARNING: " + ex.Message); }
    }

    private async Task RestorePhoneScreenAsync()
    {
        _phoneScreenDesiredOff = false;
        if (_adb is null || _activeDevice is null) return;
        try
        {
            // Always request ON during teardown.  The cached state can be stale if One UI changed
            // the physical panel independently while the virtual display was active.
            var result = await _adb.SetMainDisplayPowerAsync(_activeDevice.Serial, powerOn: true);
            AppendPowerDetails(result.Details);
            _phoneScreenPoweredOff = result.IsOn.HasValue ? !result.IsOn.Value : false;
        }
        catch { _phoneScreenPoweredOff = false; }
        finally { UpdatePhoneScreenButton(); }
    }

    private async Task EnforcePhoneScreenGuardAsync()
    {
        if (IsMirroring || _phoneScreenGuardBusy || !_phoneScreenDesiredOff || _stoppingSession) return;
        if (_session?.IsRunning != true || _adb is null || _activeDevice is null) return;

        _phoneScreenGuardBusy = true;
        try
        {
            var isOn = await _adb.GetMainDisplayPowerStateAsync(_activeDevice.Serial);
            if (isOn == false)
            {
                _phoneScreenPoweredOff = true;
                UpdatePhoneScreenButton();
                return;
            }

            // Only issue another power-off when we can positively see display 0 is ON.  This
            // avoids command spam on Samsung builds where dumpsys does not expose a readable state.
            if (isOn == true)
            {
                var result = await _adb.SetMainDisplayPowerAsync(_activeDevice.Serial, powerOn: false);
                _phoneScreenPoweredOff = result.IsOn.HasValue ? !result.IsOn.Value : true;
                UpdatePhoneScreenButton();

                // Do not flood Session log if repeated Samsung/input events wake the panel.
                if ((DateTime.Now - _lastPhoneScreenGuardLog).TotalSeconds >= 8)
                {
                    AppendLog("PHONE SCREEN GUARD: display 0 woke during the session; turned it off again.");
                    _lastPhoneScreenGuardLog = DateTime.Now;
                }
            }
        }
        catch (Exception ex)
        {
            if ((DateTime.Now - _lastPhoneScreenGuardLog).TotalSeconds >= 15)
            {
                AppendLog("PHONE SCREEN GUARD WARNING: " + ex.Message);
                _lastPhoneScreenGuardLog = DateTime.Now;
            }
        }
        finally { _phoneScreenGuardBusy = false; }
    }

    private async void EmergencyCleanup_Click(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(this,
            Loc.T("Run DexBridge emergency cleanup? This restores the phone panel and removes temporary virtual-display residue. App data is not deleted."),
            Loc.T("DexBridge emergency cleanup"), MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;
        if (_session?.IsRunning == true) { await StopSessionAsync(); return; }

        var device = DeviceCombo.SelectedItem as DeviceInfo ?? _activeDevice;
        if (_adb is null || device is null || !device.State.Equals("device", StringComparison.OrdinalIgnoreCase))
        {
            StatusText.Text = Loc.T("Emergency cleanup requires an authorized ADB device.");
            return;
        }
        try
        {
            _phoneScreenDesiredOff = false;
            var power = await _adb.SetMainDisplayPowerAsync(device.Serial, powerOn: true);
            AppendPowerDetails(power.Details);
            _phoneScreenPoweredOff = false;
            await _adb.CleanupOverlayDisplayAsync(device.Serial);
            StatusText.Text = Loc.T("Emergency cleanup complete.");
        }
        catch (Exception ex) { AppendLog("EMERGENCY CLEANUP ERROR: " + ex.Message); }
    }

    private void PresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _applyingPreset) return;
        ApplySelectedPreset();
        SaveSettingsFromUi();
    }

    private void ApplySelectedPreset()
    {
        if (PresetCombo.SelectedItem is not ComboBoxItem item) return;
        var tag = item.Tag?.ToString();
        if (string.IsNullOrWhiteSpace(tag) || tag == "custom") return;
        ApplyStreamPreset(tag);
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || _applyingTheme || _loadingUi) return;
        var theme = (ThemeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "system";
        _settings.Theme = theme;
        ThemeService.Apply(theme, NightLowContrastCheck.IsChecked == true);
        ThemeService.ApplyWindowPalette(this);
        ThemeService.ApplyWindowChrome(this);
        SettingsService.Save(_settings);
    }

    private void NightLowContrastCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || _loadingUi) return;
        _settings.NightLowContrast = NightLowContrastCheck.IsChecked == true;
        ThemeService.Apply(_settings.Theme, _settings.NightLowContrast);
        ThemeService.ApplyWindowPalette(this);
        ThemeService.ApplyWindowChrome(this);
        SettingsService.Save(_settings);
    }

    private void UpdateStreamHealth()
    {
        var target = int.TryParse(ComboValue(FpsCombo), out var targetFps) ? targetFps : 60;
        if (_session?.IsRunning != true)
        {
            StreamFpsText.Text = "—";
            StreamHealthText.Text = Loc.T("No active stream");
            StreamHealthText.Foreground = (Brush)FindResource("SecondaryTextBrush");
            return;
        }

        StreamFpsText.Text = _streamFps.HasValue ? $"{_streamFps} / {target} FPS" : $"— / {target} FPS";
        var average = _recentStreamFps.Count > 0 ? _recentStreamFps.Average() : (double?)null;
        StreamFpsText.ToolTip = average.HasValue
            ? Loc.F("scrcpy stream FPS (last 15 updates average {0:0.#}). Not game FPS; target {1} is a limit, not a guarantee.", average.Value, target)
            : Loc.T("scrcpy stream FPS, not the game's internal frame rate.");
        if (_sustainedThermalSinceUtc.HasValue &&
            DateTime.UtcNow - _sustainedThermalSinceUtc.Value >= TimeSpan.FromSeconds(20))
        {
            StreamHealthText.Text = Loc.T("Sustained thermal pressure · consider a lighter profile on next session");
            StreamHealthText.Foreground = (Brush)FindResource("WarningBrush");
            return;
        }
        if (!_streamFps.HasValue)
        {
            StreamHealthText.Text = Loc.T("Waiting for FPS sample…");
            StreamHealthText.Foreground = (Brush)FindResource("SecondaryTextBrush");
            return;
        }

        if (IsMirroring)
        {
            StreamHealthText.Text = Loc.F("Mirror stream · avg {0:0} FPS; idle or static screens may show fewer frames", average);
            StreamHealthText.Foreground = (Brush)FindResource("SecondaryTextBrush");
            return;
        }
        if (SelectedSessionMode() != "app")
        {
            StreamHealthText.Text = Loc.F("Desktop event-driven · avg {0:0} FPS; idle/24–30 FPS video may be normal", average);
            StreamHealthText.Foreground = (Brush)FindResource("SecondaryTextBrush");
            return;
        }

        // Avoid false low-FPS alarms in video/music apps where frames follow content changes.
        var package = SelectedApp()?.PackageName ?? _selectedAppPackage ?? "";
        if (package is "com.google.android.youtube" or "com.sec.android.app.music" or "com.samsung.android.video")
        {
            StreamHealthText.Text = Loc.F("Media stream · avg {0:0} FPS; content may be 24/30 FPS or static", average);
            StreamHealthText.Foreground = (Brush)FindResource("SecondaryTextBrush");
            return;
        }

        var ratio = target > 0 ? (average ?? _streamFps.Value) / target : 1;
        var frameworkThermal = _latestStats?.ThermalStatus;
        var sensorThermal = _latestStats?.MaxSensorThermalStatus;
        var thermalPressure = Math.Max(frameworkThermal ?? 0, sensorThermal ?? 0);
        if (frameworkThermal >= 4 && ratio < 0.85)
        {
            StreamHealthText.Text = Loc.T("Thermal throttling suspected");
            StreamHealthText.Foreground = (Brush)FindResource("CriticalBrush");
        }
        else if (ratio >= 0.90)
        {
            StreamHealthText.Text = thermalPressure >= 3 ? Loc.T("Good FPS · device is thermally warm") : Loc.T("Good");
            StreamHealthText.Foreground = thermalPressure >= 3
                ? (Brush)FindResource("WarningBrush")
                : (Brush)FindResource("GoodBrush");
        }
        else if (thermalPressure >= 3)
        {
            StreamHealthText.Text = frameworkThermal >= 3
                ? Loc.T("Warm · throttling may limit stream FPS")
                : Loc.T("Live thermal sensor pressure may limit stream FPS");
            StreamHealthText.Foreground = (Brush)FindResource("WarningBrush");
        }
        else if (ratio < 0.60)
        {
            StreamHealthText.Text = Loc.T("Low stream FPS · check in-game FPS cap/render load");
            StreamHealthText.Foreground = (Brush)FindResource("WarningBrush");
        }
        else
        {
            StreamHealthText.Text = Loc.T("Moderate");
            StreamHealthText.Foreground = (Brush)FindResource("WarningBrush");
        }
    }

    private string BaseScrcpyTitle()
    {
        if (IsMirroring) return "Phone Mirror · DexBridge";
        if (SelectedSessionMode() == "app")
            return SelectedApp() is { } app ? $"DexBridge · {app.DisplayName}" : "DexBridge App";
        return "Samsung DeX · DexBridge";
    }

    private string BuildTelemetryText()
    {
        var parts = new List<string> { BaseScrcpyTitle() };
        parts.Add(_streamFps.HasValue ? $"{_streamFps} FPS" : "— FPS");
        if (CpuTempTelemetryCheck.IsChecked == true && _latestStats?.CpuTemperatureC is double cpuTemp) parts.Add($"CPU {cpuTemp:0.#}°C");
        if (SkinTempTelemetryCheck.IsChecked == true && _latestStats?.SkinTemperatureC is double skinTemp) parts.Add(Loc.F("Skin {0:0.#}°C", skinTemp));
        if (ThermalTelemetryCheck.IsChecked == true && _latestStats?.ThermalStatus is int thermal) parts.Add(ThermalStatusName(thermal));
        if (CpuTelemetryCheck.IsChecked == true && _latestStats?.CpuUsagePercent is double cpu) parts.Add($"CPU {cpu:0}%");
        if (CodecTelemetryCheck.IsChecked == true) parts.Add(ComboValue(CodecCombo).ToUpperInvariant());
        if (BitrateTelemetryCheck.IsChecked == true) parts.Add($"{ComboValue(BitrateCombo)} Mbps");
        return string.Join(" • ", parts);
    }

    private void UpdateTelemetrySurface()
    {
        if (_session?.IsRunning != true || TelemetryCheck.IsChecked != true)
        {
            _telemetryOverlay?.Close(); _telemetryOverlay = null;
            if (_session?.IsRunning == true) _session.UpdateWindowTitle(BaseScrcpyTitle());
            return;
        }

        var text = BuildTelemetryText();
        // Mirror mode should follow the same pattern as DeX mode: telemetry lives in the
        // scrcpy window title bar on the same row as the base title. Overlay telemetry is
        // reserved only for layouts that intentionally hide the native title bar.
        var useOverlay = SelectedSessionMode() == "app" && SelectedGameWindowMode() == "borderless";
        if (!useOverlay)
        {
            _telemetryOverlay?.Close();
            _telemetryOverlay = null;
            _session.UpdateWindowTitle(text);
            return;
        }

        _session.UpdateWindowTitle(BaseScrcpyTitle());
        _telemetryOverlay ??= new TelemetryOverlayWindow();
        if (!_telemetryOverlay.IsVisible) _telemetryOverlay.Show();
        double left = 14, top = 14;
        if (_session.TryGetWindowBounds(out var bounds))
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            left = bounds.Left / dpi.DpiScaleX + 12;
            top = bounds.Top / dpi.DpiScaleY + 12;
        }
        _telemetryOverlay.UpdateTelemetry(text.Replace(BaseScrcpyTitle() + " • ", string.Empty), left, top);
    }

    private void UpdatePhoneScreenButton()
    {
        if (IsMirroring)
        {
            PhoneScreenButton.Content = Loc.T("Phone screen: mirroring");
            PhoneScreenButton.ToolTip = Loc.T("The DeX phone-screen power guard is disabled while mirroring display 0.");
            PhoneScreenButton.IsEnabled = false;
            return;
        }
        PhoneScreenButton.Content = _phoneScreenDesiredOff ? Loc.T("Turn phone screen on") : Loc.T("Turn phone screen off");
        PhoneScreenButton.ToolTip = _phoneScreenDesiredOff
            ? Loc.T("Phone panel guard is active. Click to allow the phone panel to stay on.")
            : Loc.T("Turn off display 0 without locking Android; the DeX/App session remains active.");
    }

    private void UpdateRunningStatus(int displayId, bool freeformOk)
    {
        _lastFreeformOk = freeformOk;
        var screen = _phoneScreenPoweredOff ? Loc.T("phone panel OFF") : Loc.T("phone panel ON");
        var gamepad = GamepadCheck.IsChecked == true ? Loc.T(" · gamepad UHID") : string.Empty;
        StatusText.Text = freeformOk
            ? Loc.F("DeX running · display {0} · freeform · {1}{2}", displayId, screen, gamepad)
            : Loc.F("DeX running · display {0} · window mode not confirmed · {1}{2}", displayId, screen, gamepad);
        UpdateControllerStatus();
    }

    private void UpdateAppStatus(int displayId)
    {
        var appName = SelectedApp()?.DisplayName ?? Loc.T("App");
        var screen = _phoneScreenPoweredOff ? Loc.T("phone panel OFF") : Loc.T("phone panel ON");
        var gamepad = GamepadCheck.IsChecked == true ? Loc.T(" · gamepad UHID active") : string.Empty;
        StatusText.Text = Loc.F("{0} running · display {1} · {2} · {3} {4} Mbps · {5}{6}", appName, displayId, SelectedGameWindowMode(), ComboValue(CodecCombo), ComboValue(BitrateCombo), screen, gamepad);
        UpdateControllerStatus();
    }

    private void UpdateMirrorStatus()
    {
        var gamepad = GamepadCheck.IsChecked == true ? Loc.T(" · gamepad UHID active") : string.Empty;
        StatusText.Text = Loc.F("Mirroring phone screen · display 0 · {0} {1} Mbps · {2}{3}",
            ComboValue(CodecCombo), ComboValue(BitrateCombo), SelectedGameWindowMode(), gamepad);
        UpdateControllerStatus();
    }

    private void SaveDesktopStreamSettingsFromControls()
    {
        _settings.Resolution = ComboValue(ResolutionCombo);
        _settings.Dpi = ComboValue(DpiCombo);
        _settings.Fps = ComboValue(FpsCombo);
        _settings.Bitrate = ComboValue(BitrateCombo);
        _settings.VideoCodec = ComboValue(CodecCombo);
        _settings.GamepadForwarding = GamepadCheck.IsChecked == true;
        _settings.DisplayProfile = (PresetCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "custom";
        _settings.DesktopWindowMode = SelectedGameWindowMode();
    }

    private void SaveMirrorStreamSettingsFromControls()
    {
        _settings.MirrorFps = ComboValue(FpsCombo);
        _settings.MirrorBitrate = ComboValue(BitrateCombo);
        _settings.MirrorVideoCodec = ComboValue(CodecCombo);
        _settings.MirrorWindowMode = SelectedGameWindowMode();
        _settings.MirrorGamepadForwarding = GamepadCheck.IsChecked == true;
    }

    private void ApplyMirrorStreamSettingsToControls()
    {
        _applyingPreset = true;
        _applyingAppProfile = true;
        try
        {
            SelectComboItem(FpsCombo, _settings.MirrorFps);
            SelectComboItem(BitrateCombo, _settings.MirrorBitrate);
            SelectComboItem(CodecCombo, _settings.MirrorVideoCodec);
            SelectTaggedItem(GameWindowModeCombo, _settings.MirrorWindowMode);
            GamepadCheck.IsChecked = _settings.MirrorGamepadForwarding;
        }
        finally
        {
            _applyingAppProfile = false;
            _applyingPreset = false;
        }
        UpdateControllerStatus();
    }

    private void ApplyDesktopStreamSettingsToControls()
    {
        _applyingPreset = true;
        _applyingAppProfile = true;
        try
        {
            SelectComboItem(ResolutionCombo, _settings.Resolution);
            SelectComboItem(DpiCombo, _settings.Dpi);
            SelectComboItem(FpsCombo, _settings.Fps);
            SelectComboItem(BitrateCombo, _settings.Bitrate);
            SelectComboItem(CodecCombo, _settings.VideoCodec);
            SelectPresetForSettings(_settings.Resolution, _settings.Dpi);
            SelectTaggedItem(GameWindowModeCombo, _settings.DesktopWindowMode);
            GamepadCheck.IsChecked = _settings.GamepadForwarding;
        }
        finally
        {
            _applyingAppProfile = false;
            _applyingPreset = false;
        }
        UpdateControllerStatus();
    }

    private void LoadSettingsIntoUi()
    {
        _loadingUi = true;
        SelectTaggedItem(LanguageCombo, _settings.Language == "en" ? "en" : "id");
        SelectTaggedItem(LayoutCombo, _settings.UiLayout == "standard" ? "standard" : "compact");
        _applyingTheme = true;
        try { SelectTaggedItem(ThemeCombo, _settings.Theme); }
        finally { _applyingTheme = false; }
        NightLowContrastCheck.IsChecked = _settings.NightLowContrast;
        NoDownsizeCheck.IsChecked = _settings.NoDownsizeOnError;
        SelectComboItem(ResolutionCombo, _settings.Resolution);
        SelectComboItem(DpiCombo, _settings.Dpi);
        SelectComboItem(FpsCombo, _settings.Fps);
        SelectComboItem(BitrateCombo, _settings.Bitrate);
        SelectComboItem(CodecCombo, _settings.VideoCodec);
        SelectPresetForSettings(_settings.Resolution, _settings.Dpi);
        SelectSessionMode(_settings.SessionMode == "game" ? "app" : _settings.SessionMode);
        SelectTaggedItem(GamingProfileCombo, _settings.GamingPreset);
        var savedWindowMode = string.Equals(_settings.GameWindowMode, "fullscreen", StringComparison.OrdinalIgnoreCase) ? "maximized" : _settings.GameWindowMode;
        SelectTaggedItem(GameWindowModeCombo, SelectedSessionMode() == "desktop" ? _settings.DesktopWindowMode : savedWindowMode);
        if (GameWindowModeCombo.SelectedItem is null) SelectTaggedItem(GameWindowModeCombo, _settings.GameFullscreenWindow ? "maximized" : "windowed");
        SelectComboItem(KeyboardCombo, _settings.Keyboard);
        SelectComboItem(MouseCombo, _settings.Mouse);
        GamepadCheck.IsChecked = _settings.GamepadForwarding;
        // Restore mirror-specific FPS/codec/bitrate/window/gamepad only after generic
        // controls have loaded, otherwise generic startup settings overwrite them.
        if (IsMirroring) ApplyMirrorStreamSettingsToControls();
        AudioCheck.IsChecked = _settings.Audio;
        NoHoverCheck.IsChecked = _settings.DisableMouseHover;
        TurnPhoneScreenOffCheck.IsChecked = _settings.TurnPhoneScreenOff;
        StayAwakeCheck.IsChecked = _settings.StayAwake;
        ForceDpiCheck.IsChecked = _settings.ForceDpi;
        HandoffDexAppsOnStopCheck.IsChecked = _settings.HandoffDexAppsOnStop;
        CloseFailedHandoffTasksCheck.IsChecked = _settings.CloseFailedHandoffTasks;
        PrintFpsCheck.IsChecked = _settings.PrintFps;
        TelemetryCheck.IsChecked = _settings.ShowTelemetry;
        CpuTempTelemetryCheck.IsChecked = _settings.ShowCpuTemperatureTelemetry;
        SkinTempTelemetryCheck.IsChecked = _settings.ShowSkinTemperatureTelemetry;
        ThermalTelemetryCheck.IsChecked = _settings.ShowThermalStateTelemetry;
        CpuTelemetryCheck.IsChecked = _settings.ShowCpuTelemetry;
        CodecTelemetryCheck.IsChecked = _settings.ShowCodecTelemetry;
        BitrateTelemetryCheck.IsChecked = _settings.ShowBitrateTelemetry;
        UpdateSessionModeUi();
        UpdateControllerStatus();
        _lastSessionMode = SelectedSessionMode();
        _loadingUi = false;
    }

    private void SaveSettingsFromUi()
    {
        _settings.SessionMode = SelectedSessionMode();
        _settings.GamePackage = SelectedApp()?.PackageName ?? _selectedAppPackage ?? _settings.GamePackage;
        if (SelectedSessionMode() == "desktop")
        {
            SaveDesktopStreamSettingsFromControls();
        }
        else if (IsMirroring)
        {
            SaveMirrorStreamSettingsFromControls();
        }
        else
        {
            _settings.GamingPreset = SelectedGamingPreset();
            _settings.GameWindowMode = SelectedGameWindowMode();
        }
        _settings.NoDownsizeOnError = NoDownsizeCheck.IsChecked == true;
        _settings.Keyboard = ComboValue(KeyboardCombo);
        _settings.Mouse = ComboValue(MouseCombo);
        _settings.Audio = AudioCheck.IsChecked == true;
        _settings.DisableMouseHover = NoHoverCheck.IsChecked == true;
        _settings.TurnPhoneScreenOff = TurnPhoneScreenOffCheck.IsChecked == true;
        _settings.StayAwake = StayAwakeCheck.IsChecked == true;
        _settings.ForceDpi = ForceDpiCheck.IsChecked == true;
        _settings.HandoffDexAppsOnStop = HandoffDexAppsOnStopCheck.IsChecked == true;
        _settings.CloseFailedHandoffTasks = CloseFailedHandoffTasksCheck.IsChecked == true;
        _settings.PrintFps = PrintFpsCheck.IsChecked == true;
        _settings.ShowTelemetry = TelemetryCheck.IsChecked == true;
        _settings.ShowCpuTemperatureTelemetry = CpuTempTelemetryCheck.IsChecked == true;
        _settings.ShowSkinTemperatureTelemetry = SkinTempTelemetryCheck.IsChecked == true;
        _settings.ShowThermalStateTelemetry = ThermalTelemetryCheck.IsChecked == true;
        _settings.ShowCpuTelemetry = CpuTelemetryCheck.IsChecked == true;
        _settings.ShowCodecTelemetry = CodecTelemetryCheck.IsChecked == true;
        _settings.ShowBitrateTelemetry = BitrateTelemetryCheck.IsChecked == true;
        _settings.SettingsSchemaVersion = 15;
        _settings.AdvancedExpanded = true;
        _settings.Theme = (ThemeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? _settings.Theme;
        _settings.UiLayout = (LayoutCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "standard" ? "standard" : "compact";
        _settings.Language = (LanguageCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "en" ? "en" : "id";
        _settings.NightLowContrast = NightLowContrastCheck.IsChecked == true;
        if (SelectedSessionMode() == "app" && SelectedApp() is { } app) SaveCurrentAppProfile(app.PackageName);
        SettingsService.Save(_settings);
    }

    private void SelectSessionMode(string mode)
    {
        SelectTaggedItem(SessionModeCombo, mode);
        if (SessionModeCombo.SelectedItem is null) SessionModeCombo.SelectedIndex = 0;
        UpdateSessionModeUi();
    }

    private void SelectPresetForSettings(string resolution, string dpi)
    {
        var fps = ComboValue(FpsCombo);
        var bitrate = ComboValue(BitrateCombo);
        var codec = ComboValue(CodecCombo);
        foreach (var entry in PresetCombo.Items)
            if (entry is ComboBoxItem item && item.Tag?.ToString() is { } id
                && StreamPresetCatalog.IsDesktopPreset(id)
                && StreamPresetCatalog.TryGet(id, out var preset)
                && StreamPresetCatalog.Matches(preset, resolution, dpi, fps, bitrate, codec))
            {
                PresetCombo.SelectedItem = item;
                return;
            }
        // Nonmatching saved settings are Custom; never clobber a user's encoder options.
        SelectTaggedItem(PresetCombo, "custom");
    }

    private static void SelectComboItem(ComboBox combo, string value)
    {
        foreach (var entry in combo.Items)
            if (entry is ComboBoxItem item && string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase)) { combo.SelectedItem = item; return; }
        if (combo.Items.Count > 0 && combo.SelectedIndex < 0) combo.SelectedIndex = 0;
    }

    private static void SelectTaggedItem(ComboBox combo, string value)
    {
        foreach (var entry in combo.Items)
            if (entry is ComboBoxItem item && string.Equals(item.Tag?.ToString(), value, StringComparison.OrdinalIgnoreCase)) { combo.SelectedItem = item; return; }
        if (combo.Items.Count > 0 && combo.SelectedIndex < 0) combo.SelectedIndex = 0;
    }

    private void AppendPowerDetails(string details)
    {
        if (string.IsNullOrWhiteSpace(details)) return;
        foreach (var line in details.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) AppendLog("  " + line.Trim());
    }

    private void AppendFpsLog(int fps)
    {
        if (FpsLogBox is null) return;
        if (_fpsLogLineCount >= 360)
        {
            FpsLogBox.Clear();
            _fpsLogLineCount = 0;
        }
        FpsLogBox.AppendText($"{DateTime.Now:HH:mm:ss}  {fps,3} FPS{Environment.NewLine}");
        FpsLogBox.ScrollToEnd();
        _fpsLogLineCount++;
    }

    private void AppendLog(string line)
    {
        // WPF TextBox otherwise holds every scrcpy command and diagnostic forever.
        if (_sessionLogLineCount >= 2000)
        {
            var text = LogBox.Text;
            var cut = 0;
            var linesRemoved = 0;
            while (linesRemoved < 500 && cut < text.Length)
            {
                var next = text.IndexOf('\n', cut);
                if (next < 0) break;
                cut = next + 1;
                linesRemoved++;
            }
            if (linesRemoved > 0)
            {
                LogBox.Text = "[Earlier session lines trimmed to limit memory]" + Environment.NewLine + text[cut..];
                _sessionLogLineCount = _sessionLogLineCount - linesRemoved + 1;
            }
        }
        LogBox.AppendText(line + Environment.NewLine);
        _sessionLogLineCount += line.Count(c => c == '\n') + 1;
        LogBox.ScrollToEnd();
    }

    private static string ComboValue(ComboBox combo)
        => (combo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? string.Empty;

    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        SaveExternalRuntimeIfValid();
        SaveSettingsFromUi();
        if (_allowWindowClose) { _monitorTimer.Stop(); _screenGuardTimer.Stop(); return; }
        if (_session?.IsRunning == true || _stoppingSession)
        {
            e.Cancel = true;
            await StopSessionAsync(closingApplication: true);
            _allowWindowClose = true;
            Close();
            return;
        }
        _monitorTimer.Stop();
        _screenGuardTimer.Stop();
        _telemetryOverlay?.Close();
    }
}
