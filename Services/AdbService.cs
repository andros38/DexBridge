using System.IO;
using System.Diagnostics;
using DexBridge.Models;
using System.Text.RegularExpressions;
using System.Globalization;

namespace DexBridge.Services;

public sealed class AdbService
{
    private (long Total, long Idle)? _lastCpuSample;

    public string AdbPath { get; }
    public string WorkingDirectory { get; }

    public AdbService(string adbPath)
    {
        AdbPath = adbPath;
        WorkingDirectory = Path.GetDirectoryName(adbPath) ?? Environment.CurrentDirectory;
    }

    public async Task<IReadOnlyList<DeviceInfo>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        var (_, output, error) = await ProcessRunner.RunAsync(AdbPath, "devices", WorkingDirectory, cancellationToken);
        var source = output + Environment.NewLine + error;
        var result = new List<DeviceInfo>();

        foreach (var rawLine in source.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase)) continue;
            var match = Regex.Match(line, @"^(\S+)\s+(device|offline|unauthorized)$", RegexOptions.IgnoreCase);
            if (!match.Success) continue;

            var serial = match.Groups[1].Value;
            var state = match.Groups[2].Value;
            var model = string.Empty;
            var android = string.Empty;

            if (state.Equals("device", StringComparison.OrdinalIgnoreCase))
            {
                model = (await ShellAsync(serial, "getprop ro.product.model", cancellationToken)).Trim();
                android = (await ShellAsync(serial, "getprop ro.build.version.release", cancellationToken)).Trim();
            }

            result.Add(new DeviceInfo(serial, state, model, android));
        }

        return result;
    }

    public async Task<string> ShellAsync(string serial, string command, CancellationToken cancellationToken = default)
    {
        var args = $"-s {Quote(serial)} shell {command}";
        var (_, output, error) = await ProcessRunner.RunAsync(AdbPath, args, WorkingDirectory, cancellationToken);
        return string.IsNullOrWhiteSpace(output) ? error : output;
    }


    public async Task<DeviceStats> GetDeviceStatsAsync(string serial, CancellationToken cancellationToken = default)
    {
        double? cpuUsage = null;
        long? ramTotal = null;
        long? ramAvailable = null;
        int? batteryLevel = null;
        double? batteryTemp = null;
        double? cpuTemp = null;
        double? gpuTemp = null;
        double? skinTemp = null;
        int? thermalStatus = null;
        int? maxSensorThermalStatus = null;

        // Aggregate CPU utilization from /proc/stat. The first sample is a baseline; subsequent
        // calls calculate usage from deltas and do not spawn long-running shell processes.
        var procStat = await ShellAsync(serial, "cat /proc/stat", cancellationToken);
        var cpuLine = procStat.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(l => l.StartsWith("cpu ", StringComparison.Ordinal));
        if (cpuLine is not null)
        {
            var parts = cpuLine.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();
            var values = new List<long>();
            foreach (var part in parts)
            {
                if (long.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                    values.Add(value);
            }

            if (values.Count >= 4)
            {
                var total = values.Sum();
                var idle = values[3] + (values.Count > 4 ? values[4] : 0); // idle + iowait
                if (_lastCpuSample.HasValue)
                {
                    var totalDelta = total - _lastCpuSample.Value.Total;
                    var idleDelta = idle - _lastCpuSample.Value.Idle;
                    if (totalDelta > 0)
                        cpuUsage = Math.Clamp((totalDelta - idleDelta) * 100.0 / totalDelta, 0, 100);
                }
                _lastCpuSample = (total, idle);
            }
        }

        var memInfo = await ShellAsync(serial, "cat /proc/meminfo", cancellationToken);
        foreach (var rawLine in memInfo.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var match = Regex.Match(rawLine, @"^(MemTotal|MemAvailable):\s+(\d+)\s+kB", RegexOptions.IgnoreCase);
            if (!match.Success) continue;
            if (!long.TryParse(match.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) continue;
            if (match.Groups[1].Value.Equals("MemTotal", StringComparison.OrdinalIgnoreCase)) ramTotal = value;
            else ramAvailable = value;
        }

        var battery = await ShellAsync(serial, "dumpsys battery", cancellationToken);
        foreach (var rawLine in battery.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            var levelMatch = Regex.Match(line, @"^level:\s*(\d+)", RegexOptions.IgnoreCase);
            if (levelMatch.Success && int.TryParse(levelMatch.Groups[1].Value, out var level)) batteryLevel = level;

            var tempMatch = Regex.Match(line, @"^temperature:\s*(-?\d+)", RegexOptions.IgnoreCase);
            if (tempMatch.Success && int.TryParse(tempMatch.Groups[1].Value, out var temp10)) batteryTemp = temp10 / 10.0;
        }

        var thermal = await ShellAsync(serial, "dumpsys thermalservice", cancellationToken);

        // `Thermal Status:` is ThermalManagerService.mStatus: the current framework throttling
        // state, not a static temperature threshold. Parse the exact dump line only.
        foreach (var rawLine in thermal.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            var statusMatch = Regex.Match(line, @"^Thermal Status:\s*(?<status>\d+)\s*$", RegexOptions.IgnoreCase);
            if (statusMatch.Success && int.TryParse(statusMatch.Groups["status"].Value, out var status))
            {
                thermalStatus = status;
                break;
            }
        }

        // Parse only the live HAL temperature section and stop before cooling devices / static
        // thresholds. This provides a second, independent severity signal for validation.
        var thermalSection = thermal;
        var marker = thermal.IndexOf("Current temperatures from HAL", StringComparison.OrdinalIgnoreCase);
        if (marker >= 0)
        {
            thermalSection = thermal[marker..];
            var end = thermalSection.IndexOf("Current cooling devices from HAL", StringComparison.OrdinalIgnoreCase);
            if (end > 0) thermalSection = thermalSection[..end];
        }

        foreach (Match match in Regex.Matches(thermalSection,
            @"Temperature\{mValue=(?<value>-?[0-9.]+),\s*mType=(?<type>\d+),\s*mName=(?<name>[^,}]+),\s*mStatus=(?<status>\d+)",
            RegexOptions.IgnoreCase))
        {
            if (!double.TryParse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) continue;
            if (value > 1000) value /= 1000.0;
            if (value < -20 || value > 150) continue;
            if (!int.TryParse(match.Groups["type"].Value, out var type)) continue;
            if (int.TryParse(match.Groups["status"].Value, out var sensorStatus))
                maxSensorThermalStatus = !maxSensorThermalStatus.HasValue ? sensorStatus : Math.Max(maxSensorThermalStatus.Value, sensorStatus);

            switch (type)
            {
                case 0: cpuTemp = !cpuTemp.HasValue ? value : Math.Max(cpuTemp.Value, value); break;
                case 1: gpuTemp = !gpuTemp.HasValue ? value : Math.Max(gpuTemp.Value, value); break;
                case 2: if (!batteryTemp.HasValue) batteryTemp = value; break;
                case 3: skinTemp = !skinTemp.HasValue ? value : Math.Max(skinTemp.Value, value); break;
            }
        }

        var thermalConfirmed = thermalStatus.HasValue
            && (!maxSensorThermalStatus.HasValue || thermalStatus.Value >= maxSensorThermalStatus.Value);
        var thermalDiagnostics = $"Framework status={thermalStatus?.ToString() ?? "N/A"}; "
            + $"max live sensor status={maxSensorThermalStatus?.ToString() ?? "N/A"}; "
            + $"CPU={cpuTemp?.ToString("0.#", CultureInfo.InvariantCulture) ?? "N/A"}C; "
            + $"GPU={gpuTemp?.ToString("0.#", CultureInfo.InvariantCulture) ?? "N/A"}C; "
            + $"skin={skinTemp?.ToString("0.#", CultureInfo.InvariantCulture) ?? "N/A"}C";

        return new DeviceStats
        {
            CpuUsagePercent = cpuUsage,
            RamTotalKb = ramTotal,
            RamAvailableKb = ramAvailable,
            BatteryLevel = batteryLevel,
            BatteryTemperatureC = batteryTemp,
            CpuTemperatureC = cpuTemp,
            GpuTemperatureC = gpuTemp,
            SkinTemperatureC = skinTemp,
            ThermalStatus = thermalStatus,
            MaxSensorThermalStatus = maxSensorThermalStatus,
            ThermalStatusConfirmed = thermalConfirmed,
            ThermalDiagnostics = thermalDiagnostics,
            ThermalRawDump = thermal,
        };
    }

    public async Task SetFreeformAsync(string serial, int displayId, CancellationToken cancellationToken = default)
    {
        await ShellAsync(serial, $"wm set-display-windowing-mode -d {displayId} 5", cancellationToken);
    }

    public async Task SetDisplayWindowingModeAsync(string serial, int displayId, int mode, CancellationToken cancellationToken = default)
    {
        await ShellAsync(serial, $"wm set-display-windowing-mode -d {displayId} {mode}", cancellationToken);
    }

    public async Task<string> GetFocusedPackageAsync(string serial, CancellationToken cancellationToken = default)
    {
        var windowDump = await ShellAsync(serial, "dumpsys window windows", cancellationToken);
        foreach (var pattern in new[]
        {
            @"mCurrentFocus=Window\{[^}]*\s(?<pkg>[A-Za-z0-9_.$]+)/[A-Za-z0-9_.$]+",
            @"mFocusedApp=.*?\s(?<pkg>[A-Za-z0-9_.$]+)/[A-Za-z0-9_.$]+"
        })
        {
            var match = Regex.Match(windowDump, pattern, RegexOptions.IgnoreCase);
            if (match.Success) return match.Groups["pkg"].Value;
        }

        var activityDump = await ShellAsync(serial, "dumpsys activity activities", cancellationToken);
        foreach (var pattern in new[]
        {
            @"topResumedActivity=.*?\s(?<pkg>[A-Za-z0-9_.$]+)/[A-Za-z0-9_.$]+",
            @"mResumedActivity:.*?\s(?<pkg>[A-Za-z0-9_.$]+)/[A-Za-z0-9_.$]+"
        })
        {
            var match = Regex.Match(activityDump, pattern, RegexOptions.IgnoreCase);
            if (match.Success) return match.Groups["pkg"].Value;
        }

        return string.Empty;
    }

    public async Task<IReadOnlyList<InstalledAppInfo>> GetLaunchableAppsFallbackAsync(string serial, CancellationToken cancellationToken = default)
    {
        var output = await ShellAsync(serial,
            "cmd package query-activities --brief -a android.intent.action.MAIN -c android.intent.category.LAUNCHER",
            cancellationToken);
        var packages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            var match = Regex.Match(line, @"(?<pkg>[A-Za-z0-9_]+(?:\.[A-Za-z0-9_]+)+)/[A-Za-z0-9_.$]+$");
            if (match.Success) packages.Add(match.Groups["pkg"].Value);
        }

        return packages
            .Select(pkg => new InstalledAppInfo
            {
                PackageName = pkg,
                Name = ScrcpyAppDiscoveryService.FriendlyNameFromPackage(pkg),
                IsSystem = pkg.StartsWith("com.android.", StringComparison.OrdinalIgnoreCase)
                    || pkg.StartsWith("com.samsung.", StringComparison.OrdinalIgnoreCase)
                    || pkg.StartsWith("com.sec.", StringComparison.OrdinalIgnoreCase),
            })
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<string> GetWindowingModeAsync(string serial, int displayId, CancellationToken cancellationToken = default)
        => await ShellAsync(serial, $"wm get-display-windowing-mode -d {displayId}", cancellationToken);

    public async Task<string> SetDisplayDensityAsync(string serial, int displayId, int density, CancellationToken cancellationToken = default)
        => await ShellAsync(serial, $"wm density {density} -d {displayId}", cancellationToken);

    public async Task<string> GetDisplayDensityAsync(string serial, int displayId, CancellationToken cancellationToken = default)
        => await ShellAsync(serial, $"wm density -d {displayId}", cancellationToken);

    public async Task<IReadOnlyList<RunningTaskInfo>> GetRunningTasksAsync(
        string serial,
        int displayId,
        CancellationToken cancellationToken = default)
    {
        var dump = await ShellAsync(serial, "dumpsys activity activities", cancellationToken);
        var result = new Dictionary<int, RunningTaskInfo>();
        int currentDisplay = -1;
        int? currentTaskId = null;
        string currentPackage = string.Empty;
        string currentComponent = string.Empty;
        bool currentVisible = false;
        bool currentFocused = false;

        void Commit()
        {
            if (!currentTaskId.HasValue || currentDisplay != displayId) return;

            var packageName = currentComponent.Contains('/')
                ? currentComponent.Split('/')[0]
                : currentPackage;
            if (string.IsNullOrWhiteSpace(packageName) || Regex.IsMatch(packageName, @"^\d+$")) return;
            if (packageName.Contains("launcher", StringComparison.OrdinalIgnoreCase)
                || packageName.Equals("com.android.systemui", StringComparison.OrdinalIgnoreCase)) return;

            result[currentTaskId.Value] = new RunningTaskInfo
            {
                TaskId = currentTaskId.Value,
                DisplayId = displayId,
                PackageName = packageName,
                ComponentName = currentComponent,
                IsVisible = currentVisible,
                IsFocused = currentFocused,
            };
        }

        foreach (var rawLine in dump.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();

            var displayMatch = Regex.Match(line, @"^Display\s+#?(\d+)", RegexOptions.IgnoreCase);
            if (displayMatch.Success)
            {
                Commit();
                currentTaskId = null;
                currentPackage = string.Empty;
                currentComponent = string.Empty;
                currentVisible = false;
                currentFocused = false;
                currentDisplay = int.Parse(displayMatch.Groups[1].Value);
                continue;
            }

            if (currentDisplay != displayId) continue;

            var taskMatch = Regex.Match(line, @"(?:Task|TaskRecord)\{[^#}]*#(?<id>\d+)(?<rest>[^}]*)\}", RegexOptions.IgnoreCase);
            if (taskMatch.Success)
            {
                Commit();
                currentTaskId = int.Parse(taskMatch.Groups["id"].Value);
                var rest = taskMatch.Groups["rest"].Value;
                currentPackage = Regex.Match(rest, @"\bA=(?<pkg>[A-Za-z0-9_.$]+)", RegexOptions.IgnoreCase).Groups["pkg"].Value;
                currentComponent = string.Empty;
                currentVisible = Regex.IsMatch(rest, @"\bvisible=true\b", RegexOptions.IgnoreCase);
                currentFocused = Regex.IsMatch(rest, @"\b(isFocused|focused)=true\b", RegexOptions.IgnoreCase);
                continue;
            }

            if (!currentTaskId.HasValue) continue;

            var componentMatch = Regex.Match(line,
                @"(?:topActivity=ComponentInfo\{|mActivityComponent=|cmp=)(?<component>[A-Za-z0-9_.$]+/[A-Za-z0-9_.$]+)",
                RegexOptions.IgnoreCase);
            if (componentMatch.Success)
            {
                currentComponent = componentMatch.Groups["component"].Value;
                if (string.IsNullOrWhiteSpace(currentPackage))
                    currentPackage = currentComponent.Split('/')[0];
            }

            if (Regex.IsMatch(line, @"\b(isVisible|mVisibleRequested|visible)=true\b", RegexOptions.IgnoreCase))
                currentVisible = true;
            if (Regex.IsMatch(line, @"\b(isFocused|focused)=true\b", RegexOptions.IgnoreCase))
                currentFocused = true;
        }

        Commit();

        // Some Samsung/Android 16 builds format `dumpsys activity activities` differently.
        // If the detailed parser found nothing, use the stable TASK lines from `dumpsys activity top`,
        // which include both task id and displayId on modern Android.
        if (result.Count == 0)
        {
            var topDump = await ShellAsync(serial, "dumpsys activity top", cancellationToken);
            foreach (var rawLine in topDump.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = rawLine.Trim();
                var match = Regex.Match(line,
                    @"^TASK\s+\d+:(?<pkg>[^\s]+)\s+id=(?<id>\d+).*?displayId=(?<display>\d+)",
                    RegexOptions.IgnoreCase);
                if (!match.Success) continue;
                if (!int.TryParse(match.Groups["display"].Value, out var lineDisplay) || lineDisplay != displayId) continue;
                if (!int.TryParse(match.Groups["id"].Value, out var taskId)) continue;

                var packageName = match.Groups["pkg"].Value;
                if (packageName.Contains("launcher", StringComparison.OrdinalIgnoreCase)
                    || packageName.Equals("com.android.systemui", StringComparison.OrdinalIgnoreCase)) continue;

                result[taskId] = new RunningTaskInfo
                {
                    TaskId = taskId,
                    DisplayId = displayId,
                    PackageName = packageName,
                    ComponentName = string.Empty,
                    IsVisible = false,
                    IsFocused = false,
                };
            }
        }

        return result.Values
            .OrderByDescending(t => t.IsFocused)
            .ThenByDescending(t => t.IsVisible)
            .ThenBy(t => t.PackageName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }


    public async Task<string> RemoveTaskAsync(string serial, int taskId, CancellationToken cancellationToken = default)
        => await ShellAsync(serial, $"am stack remove {taskId}", cancellationToken);

    public async Task<IReadOnlyList<RunningTaskInfo>> CloseDisplayTasksAsync(
        string serial,
        int displayId,
        CancellationToken cancellationToken = default)
    {
        var tasks = await GetRunningTasksAsync(serial, displayId, cancellationToken);
        foreach (var task in tasks)
        {
            await RemoveTaskAsync(serial, task.TaskId, cancellationToken);
            await Task.Delay(80, cancellationToken);
        }
        return tasks;
    }

    public async Task<string> FocusTaskAsync(string serial, int taskId, CancellationToken cancellationToken = default)
        => await ShellAsync(serial, $"am task focus {taskId}", cancellationToken);

    public async Task<int?> GetTaskDisplayIdAsync(string serial, int taskId, CancellationToken cancellationToken = default)
    {
        var dump = await ShellAsync(serial, "dumpsys activity top", cancellationToken);
        foreach (var rawLine in dump.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            var match = Regex.Match(line,
                @"^TASK\s+\d+:(?<pkg>[^\s]+)\s+id=(?<id>\d+).*?displayId=(?<display>\d+)",
                RegexOptions.IgnoreCase);
            if (!match.Success) continue;
            if (!int.TryParse(match.Groups["id"].Value, out var id) || id != taskId) continue;
            if (int.TryParse(match.Groups["display"].Value, out var displayId)) return displayId;
        }

        // Fallback for vendor dumps where `activity top` omits background tasks.
        var activities = await ShellAsync(serial, "dumpsys activity activities", cancellationToken);
        var currentDisplay = -1;
        foreach (var rawLine in activities.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            var displayMatch = Regex.Match(line, @"^Display\s+#?(?<display>\d+)", RegexOptions.IgnoreCase);
            if (displayMatch.Success && int.TryParse(displayMatch.Groups["display"].Value, out var display))
            {
                currentDisplay = display;
                continue;
            }

            var taskMatch = Regex.Match(line, @"(?:Task|TaskRecord)\{[^#}]*#(?<id>\d+)", RegexOptions.IgnoreCase);
            if (taskMatch.Success && int.TryParse(taskMatch.Groups["id"].Value, out var id) && id == taskId)
                return currentDisplay >= 0 ? currentDisplay : null;
        }
        return null;
    }

    public async Task<string> ResolveLauncherActivityAsync(string serial, string packageName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packageName)) return string.Empty;
        var output = await ShellAsync(serial,
            $"cmd package resolve-activity --brief -a android.intent.action.MAIN -c android.intent.category.LAUNCHER {packageName}",
            cancellationToken);

        return output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .LastOrDefault(line => line.Contains('/') && !line.StartsWith("No activity", StringComparison.OrdinalIgnoreCase))
            ?? string.Empty;
    }

    public async Task<TaskHandoffResult> HandoffTaskToMainDisplayAsync(
        string serial,
        RunningTaskInfo task,
        CancellationToken cancellationToken = default)
    {
        var details = new List<string>();

        // Android 15/16 WMShell exposes a desktop-mode command that reparents the task itself
        // to the next display. With the usual DexBridge topology (display 0 + one virtual DeX
        // display), moving from DeX wraps back to display 0 and preserves the task state.
        var moveOutput = (await ShellAsync(serial,
            $"dumpsys activity service SystemUIService WMShell desktopmode moveToNextDisplay {task.TaskId}",
            cancellationToken)).Trim();
        if (!string.IsNullOrWhiteSpace(moveOutput)) details.Add(moveOutput);
        await Task.Delay(350, cancellationToken);

        var movedDisplay = await GetTaskDisplayIdAsync(serial, task.TaskId, cancellationToken);
        if (movedDisplay == 0)
        {
            // Normalize the moved task to the phone's native fullscreen mode. --task keeps the
            // existing task when the platform accepts it, while --windowingMode 1 requests
            // WINDOWING_MODE_FULLSCREEN.
            var component = task.ComponentName;
            if (string.IsNullOrWhiteSpace(component))
                component = await ResolveLauncherActivityAsync(serial, task.PackageName, cancellationToken);

            if (!string.IsNullOrWhiteSpace(component))
            {
                var normalize = (await ShellAsync(serial,
                    $"am start --display 0 --windowingMode 1 --task {task.TaskId} -n {component}",
                    cancellationToken)).Trim();
                if (!string.IsNullOrWhiteSpace(normalize)) details.Add(normalize);
            }

            return new TaskHandoffResult
            {
                Task = task,
                Success = true,
                PreservedTask = true,
                Method = "moveToNextDisplay",
                Details = string.Join(Environment.NewLine, details),
            };
        }

        // Samsung builds may hide or alter the WMShell command. Fall back to relaunching the
        // same activity on display 0 in fullscreen. This may not preserve every navigation
        // detail, but prevents the app from remaining in DeX/freeform state on the phone.
        var launchComponent = task.ComponentName;
        if (string.IsNullOrWhiteSpace(launchComponent))
            launchComponent = await ResolveLauncherActivityAsync(serial, task.PackageName, cancellationToken);

        if (string.IsNullOrWhiteSpace(launchComponent))
        {
            return new TaskHandoffResult
            {
                Task = task,
                Success = false,
                PreservedTask = false,
                Method = "no-launcher-activity",
                Details = string.Join(Environment.NewLine, details),
            };
        }

        var launchOutput = (await ShellAsync(serial,
            $"am start --display 0 --windowingMode 1 --activity-reorder-to-front -n {launchComponent}",
            cancellationToken)).Trim();
        if (!string.IsNullOrWhiteSpace(launchOutput)) details.Add(launchOutput);
        await Task.Delay(350, cancellationToken);

        var phoneTasks = await GetRunningTasksAsync(serial, 0, cancellationToken);
        var phoneHasPackage = phoneTasks.Any(t => t.PackageName.Equals(task.PackageName, StringComparison.OrdinalIgnoreCase));

        if (phoneHasPackage)
        {
            // If the old DeX task still exists after the phone activity was created, remove only
            // the old DeX task. Never remove it if Android actually moved the same task to display 0.
            var oldDisplay = await GetTaskDisplayIdAsync(serial, task.TaskId, cancellationToken);
            if (oldDisplay.HasValue && oldDisplay.Value != 0)
            {
                var remove = (await RemoveTaskAsync(serial, task.TaskId, cancellationToken)).Trim();
                if (!string.IsNullOrWhiteSpace(remove)) details.Add(remove);
            }

            return new TaskHandoffResult
            {
                Task = task,
                Success = true,
                PreservedTask = false,
                Method = "relaunch-on-display-0",
                Details = string.Join(Environment.NewLine, details),
            };
        }

        return new TaskHandoffResult
        {
            Task = task,
            Success = false,
            PreservedTask = false,
            Method = "handoff-failed",
            Details = string.Join(Environment.NewLine, details),
        };
    }


    public async Task<bool> EnsurePackageOnMainDisplayAsync(
        string serial,
        RunningTaskInfo originalTask,
        CancellationToken cancellationToken = default)
    {
        var phoneTasks = await GetRunningTasksAsync(serial, 0, cancellationToken);
        if (phoneTasks.Any(t => t.PackageName.Equals(originalTask.PackageName, StringComparison.OrdinalIgnoreCase)))
            return true;

        var component = originalTask.ComponentName;
        if (string.IsNullOrWhiteSpace(component))
            component = await ResolveLauncherActivityAsync(serial, originalTask.PackageName, cancellationToken);
        if (string.IsNullOrWhiteSpace(component)) return false;

        await ShellAsync(serial,
            $"am start --display 0 --windowingMode 1 --activity-reorder-to-front -n {component}",
            cancellationToken);
        await Task.Delay(350, cancellationToken);

        phoneTasks = await GetRunningTasksAsync(serial, 0, cancellationToken);
        return phoneTasks.Any(t => t.PackageName.Equals(originalTask.PackageName, StringComparison.OrdinalIgnoreCase));
    }

    public async Task ResetVirtualDisplayOverridesAsync(string serial, int displayId, CancellationToken cancellationToken = default)
    {
        await ShellAsync(serial, $"wm density reset -d {displayId}", cancellationToken);
        await ShellAsync(serial, $"wm set-display-windowing-mode -d {displayId} 0", cancellationToken);
    }

    public async Task<IReadOnlyList<PhoneFileEntry>> ListDirectoryAsync(
        string serial,
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeRemotePath(remotePath);
        var output = await ShellAsync(serial, $"ls -1p {ShellQuote(normalized)}", cancellationToken);
        if (output.Contains("No such file", StringComparison.OrdinalIgnoreCase)
            || output.Contains("Permission denied", StringComparison.OrdinalIgnoreCase))
            throw new IOException(output.Trim());

        var entries = new List<PhoneFileEntry>();
        foreach (var rawLine in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line)) continue;
            var isDirectory = line.EndsWith('/');
            var name = isDirectory ? line.TrimEnd('/') : line;
            if (name is "." or "..") continue;
            entries.Add(new PhoneFileEntry
            {
                Name = name,
                FullPath = CombineRemotePath(normalized, name),
                IsDirectory = isDirectory,
            });
        }

        return entries.OrderByDescending(e => e.IsDirectory)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<(bool Success, string Details)> PullAsync(
        string serial,
        string remotePath,
        string localPath,
        CancellationToken cancellationToken = default)
    {
        var args = $"-s {Quote(serial)} pull {Quote(remotePath)} {Quote(localPath)}";
        var (exitCode, output, error) = await ProcessRunner.RunAsync(AdbPath, args, WorkingDirectory, cancellationToken);
        var details = (output + Environment.NewLine + error).Trim();
        return (exitCode == 0, details);
    }

    public async Task<(bool Success, string Details)> PushAsync(
        string serial,
        string localPath,
        string remoteDirectory,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeRemotePath(remoteDirectory);
        var args = $"-s {Quote(serial)} push {Quote(localPath)} {Quote(normalized + "/")}";
        var (exitCode, output, error) = await ProcessRunner.RunAsync(AdbPath, args, WorkingDirectory, cancellationToken);
        var details = (output + Environment.NewLine + error).Trim();
        return (exitCode == 0, details);
    }

    public static string ParentRemotePath(string remotePath)
    {
        var normalized = NormalizeRemotePath(remotePath);
        if (normalized == "/" || normalized == "/sdcard") return "/sdcard";
        var index = normalized.LastIndexOf('/');
        if (index <= 0) return "/sdcard";
        var parent = normalized[..index];
        return string.IsNullOrWhiteSpace(parent) ? "/sdcard" : parent;
    }

    private static string NormalizeRemotePath(string path)
    {
        var value = string.IsNullOrWhiteSpace(path) ? "/sdcard/Download" : path.Trim();
        value = value.Replace('\\', '/');
        while (value.Contains("//", StringComparison.Ordinal)) value = value.Replace("//", "/", StringComparison.Ordinal);
        if (!value.StartsWith('/')) value = "/" + value;
        return value.Length > 1 ? value.TrimEnd('/') : value;
    }

    private static string CombineRemotePath(string directory, string name)
        => NormalizeRemotePath(directory) + "/" + name.Trim('/');

    private static string ShellQuote(string value)
        => "'" + value.Replace("'", "'\\''") + "'";

    public async Task<bool?> GetMainDisplayPowerStateAsync(string serial, CancellationToken cancellationToken = default)
    {
        var dump = await ShellAsync(serial, "dumpsys display", cancellationToken);
        var lines = dump.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        // Android 14-16 commonly prints a Logical Displays block like:
        //   Display 0:
        //     mDisplayId=0
        //     ... DisplayInfo{..., state ON, committedState ON, ...}
        // State therefore does not necessarily live on the same line as displayId.
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            var startsDisplayZero = Regex.IsMatch(line, @"^Display\s+0\s*:", RegexOptions.IgnoreCase);
            var hasDisplayIdZero = Regex.IsMatch(line, @"\bmDisplayId\s*=\s*0\b|\bdisplayId\s*=?\s*0\b", RegexOptions.IgnoreCase);
            if (!startsDisplayZero && !hasDisplayIdZero) continue;

            for (var j = i; j < Math.Min(lines.Length, i + 40); j++)
            {
                var candidate = lines[j].Trim();
                if (j > i && Regex.IsMatch(candidate, @"^Display\s+\d+\s*:", RegexOptions.IgnoreCase)) break;

                var stateMatch = Regex.Match(candidate, @"\bstate\s+(ON|OFF)\b", RegexOptions.IgnoreCase);
                if (stateMatch.Success)
                    return stateMatch.Groups[1].Value.Equals("ON", StringComparison.OrdinalIgnoreCase);

                var committedMatch = Regex.Match(candidate, @"\bcommittedState\s+(ON|OFF)\b", RegexOptions.IgnoreCase);
                if (committedMatch.Success)
                    return committedMatch.Groups[1].Value.Equals("ON", StringComparison.OrdinalIgnoreCase);
            }
        }

        return null;
    }

    /// <summary>
    /// Controls only the physical main display (display 0), not the DeX virtual display.
    /// Android/One UI builds differ in how they restore a display after power-off, so the
    /// power-on path intentionally uses several safe fallbacks and then verifies the state.
    /// </summary>
    public async Task<(bool? IsOn, string Details)> SetMainDisplayPowerAsync(
        string serial,
        bool powerOn,
        CancellationToken cancellationToken = default)
    {
        var details = new List<string>();

        if (!powerOn)
        {
            var output = (await ShellAsync(serial, "cmd display power-off 0", cancellationToken)).Trim();
            if (!string.IsNullOrWhiteSpace(output)) details.Add(output);
            await Task.Delay(200, cancellationToken);
            var state = await GetMainDisplayPowerStateAsync(serial, cancellationToken);
            return (state, string.Join(Environment.NewLine, details));
        }

        // Newer Android branches expose power-reset rather than power-on.
        var reset = (await ShellAsync(serial, "cmd display power-reset 0", cancellationToken)).Trim();
        if (!string.IsNullOrWhiteSpace(reset)) details.Add(reset);
        await Task.Delay(180, cancellationToken);
        var currentState = await GetMainDisplayPowerStateAsync(serial, cancellationToken);
        if (currentState == true) return (true, string.Join(Environment.NewLine, details));

        // Older Android 15/16 branches expose power-on.
        var legacyOn = (await ShellAsync(serial, "cmd display power-on 0", cancellationToken)).Trim();
        if (!string.IsNullOrWhiteSpace(legacyOn)) details.Add(legacyOn);
        await Task.Delay(180, cancellationToken);
        currentState = await GetMainDisplayPowerStateAsync(serial, cancellationToken);
        if (currentState == true) return (true, string.Join(Environment.NewLine, details));

        // Final safe fallback: KEYCODE_WAKEUP (224). Unlike KEYCODE_POWER it does not toggle
        // an already-on display back off.
        var wake = (await ShellAsync(serial, "input keyevent 224", cancellationToken)).Trim();
        if (!string.IsNullOrWhiteSpace(wake)) details.Add(wake);
        await Task.Delay(250, cancellationToken);
        currentState = await GetMainDisplayPowerStateAsync(serial, cancellationToken);

        return (currentState, string.Join(Environment.NewLine, details));
    }

    public void SetMainDisplayPowerBestEffort(string serial, bool powerOn)
    {
        try
        {
            var command = powerOn ? "input keyevent 224" : "cmd display power-off 0";
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = AdbPath,
                    Arguments = $"-s {Quote(serial)} shell {command}",
                    WorkingDirectory = WorkingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            process.Start();
            process.WaitForExit(1500);
        }
        catch
        {
            // Best-effort shutdown cleanup only.
        }
    }

    public async Task CleanupOverlayDisplayAsync(string serial, CancellationToken cancellationToken = default)
    {
        await ShellAsync(serial, "settings delete global overlay_display_devices", cancellationToken);
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";
}
