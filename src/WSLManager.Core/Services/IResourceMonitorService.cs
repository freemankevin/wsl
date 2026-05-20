namespace WSLManager.Core.Services;

using WSLManager.Core.Models;

public interface IResourceMonitorService
{
    bool IsMonitoring { get; }
    void StartMonitoring(TimeSpan? interval = null);
    void StopMonitoring();
    Task<List<WslResourceInfo>> GetResourceUsageAsync(CancellationToken ct = default);
    Task<WslResourceInfo?> GetDistributionResourceAsync(string distributionName, CancellationToken ct = default);
}
