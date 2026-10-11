namespace DexBridge.Models;

public sealed class TaskHandoffResult
{
    public required RunningTaskInfo Task { get; init; }
    public bool Success { get; init; }
    public bool PreservedTask { get; init; }
    public string Method { get; init; } = string.Empty;
    public string Details { get; init; } = string.Empty;
}
