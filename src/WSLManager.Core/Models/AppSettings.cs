namespace WSLManager.Core.Models;

/// <summary>
/// Application user settings persisted to disk.
/// </summary>
public sealed class AppSettings
{
    public double WindowLeft { get; set; } = double.NaN;
    public double WindowTop { get; set; } = double.NaN;
    public double WindowWidth { get; set; } = 1200;
    public double WindowHeight { get; set; } = 800;
    public bool IsMaximized { get; set; } = false;

    public bool AutoRefresh { get; set; } = true;
    public int RefreshIntervalSeconds { get; set; } = 2;

    public string? LastExportDirectory { get; set; }
    public string? LastImportDirectory { get; set; }
}
