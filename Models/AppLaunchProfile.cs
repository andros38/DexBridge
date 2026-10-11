namespace DexBridge.Models;

public sealed class AppLaunchProfile
{
    public string GamingPreset { get; set; } = "balanced";
    public string Resolution { get; set; } = "1920x1080";
    public string Dpi { get; set; } = "240";
    public string Fps { get; set; } = "60";
    public string Bitrate { get; set; } = "20";
    public string VideoCodec { get; set; } = "h264";
    public string WindowMode { get; set; } = "windowed";
    public bool GamepadForwarding { get; set; }
}
