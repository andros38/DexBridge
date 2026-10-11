using DexBridge.Models;
using System.IO;
using System.Text.RegularExpressions;

namespace DexBridge.Services;

public sealed class ScrcpyAppDiscoveryService
{
    private readonly string _scrcpyPath;
    private readonly string _workingDirectory;

    public ScrcpyAppDiscoveryService(string scrcpyPath)
    {
        _scrcpyPath = scrcpyPath;
        _workingDirectory = Path.GetDirectoryName(scrcpyPath) ?? Environment.CurrentDirectory;
    }

    public async Task<IReadOnlyList<InstalledAppInfo>> ListAppsAsync(string serial, CancellationToken cancellationToken = default)
    {
        var safeSerial = serial.Replace("\"", "\\\"");
        var args = $"-s \"{safeSerial}\" --list-apps";
        var (_, stdout, stderr) = await ProcessRunner.RunAsync(_scrcpyPath, args, _workingDirectory, cancellationToken);
        var combined = stdout + Environment.NewLine + stderr;
        return ParseListAppsOutput(combined);
    }

    internal static IReadOnlyList<InstalledAppInfo> ParseListAppsOutput(string output)
    {
        var apps = new Dictionary<string, InstalledAppInfo>(StringComparer.OrdinalIgnoreCase);
        string? pendingName = null;
        bool pendingSystem = false;
        var inList = false;

        foreach (var raw in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = Regex.Replace(raw, @"^\[[^\]]+\]\s+[A-Z]+:\s*", string.Empty).TrimEnd();
            if (line.Contains("List of apps:", StringComparison.OrdinalIgnoreCase))
            {
                inList = true;
                continue;
            }
            if (!inList) continue;

            var entry = Regex.Match(line, @"^\s*(?<kind>[*-])\s+(?<body>.+)$");
            if (entry.Success)
            {
                var body = entry.Groups["body"].Value.TrimEnd();
                var packageMatch = Regex.Match(body, @"(?<pkg>[A-Za-z0-9_]+(?:\.[A-Za-z0-9_]+)+)$");
                if (packageMatch.Success)
                {
                    var package = packageMatch.Groups["pkg"].Value;
                    var name = body[..packageMatch.Index].Trim();
                    if (string.IsNullOrWhiteSpace(name)) name = FriendlyNameFromPackage(package);
                    apps[package] = new InstalledAppInfo
                    {
                        Name = name,
                        PackageName = package,
                        IsSystem = entry.Groups["kind"].Value == "*",
                    };
                    pendingName = null;
                }
                else
                {
                    pendingName = body.Trim();
                    pendingSystem = entry.Groups["kind"].Value == "*";
                }
                continue;
            }

            if (pendingName is not null)
            {
                var packageOnly = Regex.Match(line.Trim(), @"^(?<pkg>[A-Za-z0-9_]+(?:\.[A-Za-z0-9_]+)+)$");
                if (packageOnly.Success)
                {
                    var package = packageOnly.Groups["pkg"].Value;
                    apps[package] = new InstalledAppInfo
                    {
                        Name = pendingName,
                        PackageName = package,
                        IsSystem = pendingSystem,
                    };
                    pendingName = null;
                }
            }
        }

        return apps.Values.OrderBy(a => a.IsSystem).ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static string FriendlyNameFromPackage(string package)
    {
        var tail = package.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? package;
        tail = Regex.Replace(tail, @"(?<=[a-z])(?=[A-Z])", " ");
        if (tail.Equals("app", StringComparison.OrdinalIgnoreCase) || tail.Equals("android", StringComparison.OrdinalIgnoreCase))
        {
            var parts = package.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1) tail = parts[^2];
        }
        if (tail.Length == 0) return package;
        return char.ToUpperInvariant(tail[0]) + tail[1..];
    }
}
