using System;
using System.Collections.Generic;

namespace DexBridge.Services;

// Single source of truth for the virtual-display geometry AND stream encoder options.
// Presets are suggestions, not claims about the Android app's internal render resolution/FPS.
public sealed record StreamPreset(
    string Id, string Resolution, string Dpi, string Fps, string Bitrate, string Codec);

public static class StreamPresetCatalog
{
    private static readonly Dictionary<string, StreamPreset> Presets =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["desktop-balanced"] = new("desktop-balanced", "1920x1080", "220", "60", "16", "h264"),
            ["desktop-compact"] = new("desktop-compact", "1920x1080", "180", "60", "16", "h264"),
            ["desktop-sharp"] = new("desktop-sharp", "2560x1440", "200", "60", "24", "h265"),
            ["youtube-quality"] = new("youtube-quality", "1920x1080", "240", "60", "32", "h265"),
            ["youtube-balanced"] = new("youtube-balanced", "1920x1080", "240", "60", "20", "h265"),
            ["performance"] = new("performance", "1600x900", "240", "60", "16", "h264"),
            ["balanced"] = new("balanced", "1920x1080", "240", "60", "20", "h264"),
            ["quality"] = new("quality", "1920x1080", "240", "60", "24", "h265"),
            ["maximum"] = new("maximum", "2560x1440", "240", "60", "32", "h265"),
            ["media-portrait"] = new("media-portrait", "1080x1920", "420", "60", "16", "h265"),
            ["media-landscape"] = new("media-landscape", "1920x1080", "240", "60", "20", "h265"),
        };

    public static bool TryGet(string? id, out StreamPreset preset)
        => Presets.TryGetValue(id ?? string.Empty, out preset!);

    public static bool Matches(StreamPreset preset, string resolution, string dpi,
        string fps, string bitrate, string codec)
        => preset.Resolution == resolution && preset.Dpi == dpi
           && preset.Fps == fps && preset.Bitrate == bitrate
           && preset.Codec.Equals(codec, StringComparison.OrdinalIgnoreCase);

    // Desktop and App/Game have distinct quick-pick lists, although they can share
    // a preset definition (e.g. YouTube Quality).
    public static bool IsDesktopPreset(string id)
        => id.StartsWith("desktop-", StringComparison.OrdinalIgnoreCase)
           || id is "youtube-quality" or "youtube-balanced";

    public static bool Validate(string resolution, string dpi, string fps,
        string bitrate, string codec, out string error)
    {
        error = string.Empty;
        var parts = resolution.Split('x');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var width)
            || !int.TryParse(parts[1], out var height)
            || width < 640 || height < 640 || width > 3840 || height > 3840)
            error = LocalizationService.T("Unsupported virtual-display resolution.");
        else if (!int.TryParse(dpi, out var density) || density < 120 || density > 640)
            error = LocalizationService.T("Virtual-display DPI must be between 120 and 640.");
        else if (!int.TryParse(fps, out var frameRate) || frameRate < 15 || frameRate > 120)
            error = LocalizationService.T("Target FPS must be between 15 and 120.");
        else if (!int.TryParse(bitrate, out var mbps) || mbps < 2 || mbps > 80)
            error = LocalizationService.T("Video bitrate must be between 2 and 80 Mbps.");
        else if (codec != "h264" && codec != "h265")
            error = LocalizationService.T("Unsupported video codec.");
        return error.Length == 0;
    }
}
