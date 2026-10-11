using System.IO;

namespace DexBridge.Services;

public static class RuntimeLocator
{
    public static string? FindScrcpy()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "runtime", "scrcpy", "scrcpy.exe"),
            Path.Combine(AppContext.BaseDirectory, "runtime", "scrcpy.exe"),
            Path.Combine(AppContext.BaseDirectory, "scrcpy.exe"),
            @"C:\scrcpy-win64-v5.0\scrcpy.exe",
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    public static string? FindAdb(string? scrcpyPath = null)
    {
        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "runtime", "scrcpy", "adb.exe"),
            Path.Combine(AppContext.BaseDirectory, "runtime", "adb.exe"),
            Path.Combine(AppContext.BaseDirectory, "adb.exe"),
        };

        if (!string.IsNullOrWhiteSpace(scrcpyPath))
        {
            var folder = Path.GetDirectoryName(scrcpyPath);
            if (!string.IsNullOrWhiteSpace(folder)) candidates.Insert(0, Path.Combine(folder, "adb.exe"));
        }

        candidates.Add(@"C:\scrcpy-win64-v5.0\adb.exe");
        return candidates.FirstOrDefault(File.Exists);
    }

    public static bool IsBundledRuntime(string? scrcpyPath)
    {
        if (string.IsNullOrWhiteSpace(scrcpyPath)) return false;
        var runtimeRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "runtime"));
        var full = Path.GetFullPath(scrcpyPath);
        return full.StartsWith(runtimeRoot, StringComparison.OrdinalIgnoreCase);
    }
}
