using CommunityToolkit.Mvvm.ComponentModel;

namespace WSLManager.Core.Models;

public partial class WslResourceInfo : ObservableObject
{
    [ObservableProperty]
    private double _cpuUsagePercent;

    [ObservableProperty]
    private double _memoryUsageMB;

    [ObservableProperty]
    private double _memoryTotalMB;

    [ObservableProperty]
    private double _diskReadMBPerSec;

    [ObservableProperty]
    private double _diskWriteMBPerSec;

    [ObservableProperty]
    private DateTime _timestamp;

    public double MemoryUsagePercent => MemoryTotalMB > 0 ? (MemoryUsageMB / MemoryTotalMB) * 100.0 : 0;
}
