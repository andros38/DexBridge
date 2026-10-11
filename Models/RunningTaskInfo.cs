namespace DexBridge.Models;

public sealed class RunningTaskInfo
{
    public int TaskId { get; init; }
    public int DisplayId { get; init; }
    public string PackageName { get; init; } = string.Empty;
    public string ComponentName { get; init; } = string.Empty;
    public string AppName { get; init; } = string.Empty;
    public bool IsVisible { get; init; }
    public bool IsFocused { get; init; }

    public string Title => !string.IsNullOrWhiteSpace(AppName)
        ? AppName
        : string.IsNullOrWhiteSpace(PackageName) ? $"Task {TaskId}" : PackageName;

    public string Details
    {
        get
        {
            var state = DexBridge.Services.LocalizationService.T(IsFocused ? "focused" : IsVisible ? "visible" : "background/minimized");
            var package = string.IsNullOrWhiteSpace(PackageName) ? string.Empty : $" · {PackageName}";
            return DexBridge.Services.LocalizationService.F("task {0} · {1}{2}", TaskId, state, package);
        }
    }
}
