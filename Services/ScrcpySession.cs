using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace DexBridge.Services;

public sealed class ScrcpySession : IDisposable
{
    private readonly string _scrcpyPath;
    private readonly string _workingDirectory;
    private Process? _process;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetWindowText(IntPtr hWnd, string lpString);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_MAXIMIZE = 3;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public event Action<string>? LogReceived;
    public event Action<int>? DisplayCreated;
    public event Action<int>? FpsUpdated;
    public event Action<int>? Exited;

    public bool IsRunning => _process is { HasExited: false };
    public int? DisplayId { get; private set; }
    public int? LastFps { get; private set; }

    public ScrcpySession(string scrcpyPath)
    {
        _scrcpyPath = scrcpyPath;
        _workingDirectory = Path.GetDirectoryName(scrcpyPath) ?? Environment.CurrentDirectory;
    }

    public void Start(string arguments)
    {
        if (IsRunning) throw new InvalidOperationException("scrcpy is already running.");

        DisplayId = null;
        LastFps = null;
        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _scrcpyPath,
                Arguments = arguments,
                WorkingDirectory = _workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
            EnableRaisingEvents = true,
        };

        _process.OutputDataReceived += (_, e) => HandleLine(e.Data);
        _process.ErrorDataReceived += (_, e) => HandleLine(e.Data);
        _process.Exited += (_, _) => Exited?.Invoke(_process?.ExitCode ?? -1);

        _process.Start();
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }

    private void HandleLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        LogReceived?.Invoke(line);

        var displayMatch = Regex.Match(line, @"New display:\s*\S+\s*\(id=(\d+)\)", RegexOptions.IgnoreCase);
        if (displayMatch.Success && int.TryParse(displayMatch.Groups[1].Value, out var id))
        {
            DisplayId = id;
            DisplayCreated?.Invoke(id);
        }

        var fpsMatch = Regex.Match(line, @"\b(?<fps>\d+)\s+fps\b", RegexOptions.IgnoreCase);
        if (fpsMatch.Success && int.TryParse(fpsMatch.Groups["fps"].Value, out var fps))
        {
            LastFps = fps;
            FpsUpdated?.Invoke(fps);
        }
    }

    private IntPtr TryGetMainWindowHandle()
    {
        try
        {
            if (_process is null || _process.HasExited) return IntPtr.Zero;
            _process.Refresh();
            return _process.MainWindowHandle;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    public bool UpdateWindowTitle(string title)
    {
        var hwnd = TryGetMainWindowHandle();
        return hwnd != IntPtr.Zero && SetWindowText(hwnd, title);
    }


    public async Task<bool> MaximizeWindowAsync(int timeoutMs = 3500)
    {
        var started = Environment.TickCount64;
        while (Environment.TickCount64 - started < timeoutMs)
        {
            var hwnd = TryGetMainWindowHandle();
            if (hwnd != IntPtr.Zero) return ShowWindow(hwnd, SW_MAXIMIZE);
            await Task.Delay(80);
        }
        return false;
    }

    public bool TryGetWindowBounds(out (int Left, int Top, int Right, int Bottom) bounds)
    {
        bounds = default;
        var hwnd = TryGetMainWindowHandle();
        if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var rect)) return false;
        bounds = (rect.Left, rect.Top, rect.Right, rect.Bottom);
        return true;
    }

    public async Task StopGracefullyAsync(int timeoutMs = 4500)
    {
        if (_process is null) return;
        try
        {
            if (!_process.HasExited)
            {
                var closeRequested = false;
                try { closeRequested = _process.CloseMainWindow(); } catch { }

                if (closeRequested)
                {
                    using var cts = new CancellationTokenSource(timeoutMs);
                    try { await _process.WaitForExitAsync(cts.Token); } catch (OperationCanceledException) { }
                }

                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                    _process.WaitForExit(2000);
                }
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
        finally
        {
            _process.Dispose();
            _process = null;
            DisplayId = null;
            LastFps = null;
        }
    }

    public void Stop()
    {
        try { StopGracefullyAsync().GetAwaiter().GetResult(); } catch { }
    }

    public void Dispose() => Stop();
}
