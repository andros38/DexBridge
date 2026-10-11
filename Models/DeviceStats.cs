namespace DexBridge.Models;

public sealed class DeviceStats
{
    public double? CpuUsagePercent { get; init; }
    public long? RamTotalKb { get; init; }
    public long? RamAvailableKb { get; init; }
    public int? BatteryLevel { get; init; }
    public double? BatteryTemperatureC { get; init; }
    public double? CpuTemperatureC { get; init; }
    public double? GpuTemperatureC { get; init; }
    public double? SkinTemperatureC { get; init; }
    public int? ThermalStatus { get; init; }
    public int? MaxSensorThermalStatus { get; init; }
    public bool ThermalStatusConfirmed { get; init; }
    public string ThermalDiagnostics { get; init; } = string.Empty;
    public string ThermalRawDump { get; init; } = string.Empty;

    public double? RamUsedPercent => RamTotalKb is > 0 && RamAvailableKb.HasValue
        ? (RamTotalKb.Value - RamAvailableKb.Value) * 100.0 / RamTotalKb.Value
        : null;

    public double? RamUsedGb => RamTotalKb.HasValue && RamAvailableKb.HasValue
        ? (RamTotalKb.Value - RamAvailableKb.Value) / 1024.0 / 1024.0
        : null;

    public double? RamTotalGb => RamTotalKb.HasValue ? RamTotalKb.Value / 1024.0 / 1024.0 : null;
}
