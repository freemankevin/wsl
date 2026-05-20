namespace WSLManager.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using WSLManager.Core.Models;

public sealed partial class DistributionViewModel : ObservableObject
{
    private readonly WslDistribution _model;

    public string Name => _model.Name;
    public string State => _model.State;
    public int Version => _model.WslVersion;
    public bool IsDefault => _model.IsDefault;
    public bool IsRunning => _model.IsRunning;
    public bool CanStart => !_model.IsRunning;
    public bool CanStop => _model.IsRunning;
    public double CpuUsagePercent => _model.CpuUsagePercent;
    public double MemoryUsageMB => _model.MemoryUsageMB;

    [ObservableProperty]
    private bool _isSelected;

    public DistributionViewModel(WslDistribution model)
    {
        _model = model;
        _model.PropertyChanged += (_, e) => OnPropertyChanged(e.PropertyName);
    }
}
