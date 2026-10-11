namespace DexBridge.Models;

public sealed record DeviceInfo(string Serial, string State, string Model, string AndroidVersion)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Model)
        ? $"{Serial} ({State})"
        : $"{Model} · Android {AndroidVersion} · {Serial}";
}
