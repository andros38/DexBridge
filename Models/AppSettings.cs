namespace DexBridge.Models;

public sealed class AppSettings
{
    public int SettingsSchemaVersion { get; set; } = 0;
    public string DisplayProfile { get; set; } = "1440p compact";
    public string Resolution { get; set; } = "2560x1440";
    public string Dpi { get; set; } = "180";
    public string Fps { get; set; } = "60";
    public string Bitrate { get; set; } = "24";
    public string VideoCodec { get; set; } = "h265";
    public bool NoDownsizeOnError { get; set; } = true;
    public string SessionMode { get; set; } = "desktop";
    // Screen mirroring captures physical display 0; it has no virtual resolution/DPI.
    // Keep its encoding/window/controller preferences separate from DeX/App profiles.
    public string MirrorFps { get; set; } = "60";
    public string MirrorBitrate { get; set; } = "16";
    public string MirrorVideoCodec { get; set; } = "h264";
    public string MirrorWindowMode { get; set; } = "windowed";
    public bool MirrorGamepadForwarding { get; set; } = false;
    public string GamePackage { get; set; } = string.Empty; // legacy name; now selected App/Game package
    public string GamingPreset { get; set; } = "balanced";
    public string GameWindowMode { get; set; } = "windowed";
    public string DesktopWindowMode { get; set; } = "windowed";
    public bool GameFullscreenWindow { get; set; } = false; // legacy
    public string Keyboard { get; set; } = "uhid";
    public string Mouse { get; set; } = "sdk";
    public bool GamepadForwarding { get; set; } = false;
    public bool Audio { get; set; } = true;
    public bool DisableMouseHover { get; set; } = true;
    public bool TurnPhoneScreenOff { get; set; } = true;
    public bool StayAwake { get; set; } = true;
    public bool ForceDpi { get; set; } = true;
    public bool CloseDexAppsOnStop { get; set; } = true;
    public bool HandoffDexAppsOnStop { get; set; } = true;
    public bool CloseFailedHandoffTasks { get; set; } = true;
    public bool PrintFps { get; set; } = false;
    public bool ShowTelemetry { get; set; } = true;
    public bool ShowCpuTemperatureTelemetry { get; set; } = true;
    public bool ShowSkinTemperatureTelemetry { get; set; } = false;
    public bool ShowThermalStateTelemetry { get; set; } = true;
    public bool ShowCpuTelemetry { get; set; } = false;
    public bool ShowCodecTelemetry { get; set; } = false;
    public bool ShowBitrateTelemetry { get; set; } = false;
    public bool AdvancedExpanded { get; set; } = false;
    public int MonitorIntervalSeconds { get; set; } = 2;
    public string Theme { get; set; } = "system";
    public string UiLayout { get; set; } = "compact"; // compact on first launch; standard remains available
    public string Language { get; set; } = "id"; // New installation or upgraded settings without this property
    // Remember the USB device and external scrcpy executable without pinning bundled runtime to an old installation path.
    public string PreferredDeviceSerial { get; set; } = string.Empty;
    public string ExternalScrcpyPath { get; set; } = string.Empty;
    public bool NightLowContrast { get; set; } = false;
    public List<string> FavoriteApps { get; set; } = new();
    public List<string> RecentApps { get; set; } = new();
    public Dictionary<string, AppLaunchProfile> AppProfiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
