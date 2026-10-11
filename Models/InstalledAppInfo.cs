namespace DexBridge.Models;

public sealed class InstalledAppInfo
{
    public string Name { get; init; } = string.Empty;
    public string PackageName { get; init; } = string.Empty;
    public bool IsSystem { get; init; }
    public bool IsFavorite { get; set; }
    public bool IsRecent { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? PackageName : Name;
    public string DisplayText => $"{(IsFavorite ? "★ " : IsRecent ? "↺ " : "")}{DisplayName} — {PackageName}";
}
