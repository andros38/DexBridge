namespace DexBridge.Models;

public sealed class PhoneFileEntry
{
    public string Name { get; init; } = string.Empty;
    public string FullPath { get; init; } = string.Empty;
    public bool IsDirectory { get; init; }

    public string DisplayName => IsDirectory ? $"📁 {Name}" : Name;
    public string Kind => DexBridge.Services.LocalizationService.T(IsDirectory ? "Folder" : "File");
}
