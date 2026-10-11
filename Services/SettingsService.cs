using DexBridge.Models;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace DexBridge.Services;

public static class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static string SettingsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DexBridge");
    private static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");
    private static string BackupPath => SettingsPath + ".bak";

    public static AppSettings Load()
    {
        // If the most recent write was interrupted or the JSON is corrupted,
        // fall back to the last successful settings file instead of resetting every control.
        foreach (var path in new[] { SettingsPath, BackupPath })
        {
            if (!File.Exists(path)) continue;
            try
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path));
                if (settings is not null) return settings;
            }
            catch (Exception ex) { Debug.WriteLine($"DexBridge settings read ({path}): {ex.Message}"); }
        }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        string? temporaryPath = null;
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            temporaryPath = SettingsPath + ".tmp";
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(temporaryPath, json);
            // Keep a recoverable copy of the previous valid settings.
            if (File.Exists(SettingsPath))
            {
                try
                {
                    var previous = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath));
                    if (previous is not null) File.Copy(SettingsPath, BackupPath, overwrite: true);
                }
                catch (JsonException) { /* do not back up a corrupt file */ }
            }
            File.Move(temporaryPath, SettingsPath, overwrite: true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"DexBridge settings save failed: {ex}");
        }
        finally
        {
            if (temporaryPath is not null)
                try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch { }
        }
    }
}
