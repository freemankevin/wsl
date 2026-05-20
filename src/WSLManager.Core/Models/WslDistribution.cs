using CommunityToolkit.Mvvm.ComponentModel;

namespace WSLManager.Core.Models;

public partial class WslDistribution : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _state = string.Empty;

    [ObservableProperty]
    private int _wslVersion;

    [ObservableProperty]
    private bool _isDefault;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private double _cpuUsagePercent;

    [ObservableProperty]
    private double _memoryUsageMB;

    public bool IsRunning => State.Equals("Running", StringComparison.OrdinalIgnoreCase);
    public bool IsStopped => State.Equals("Stopped", StringComparison.OrdinalIgnoreCase);
}
