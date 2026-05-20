using WSLManager.Core.Models;

namespace WSLManager.Core.Services;

public interface IWslService
{
    Task<IReadOnlyList<WslDistribution>> GetDistributionsAsync(CancellationToken cancellationToken = default);
    Task<bool> StartDistributionAsync(string name, CancellationToken cancellationToken = default);
    Task<bool> StopDistributionAsync(string name, CancellationToken cancellationToken = default);
    Task<bool> TerminateDistributionAsync(string name, CancellationToken cancellationToken = default);
    Task<bool> SetDefaultDistributionAsync(string name, CancellationToken cancellationToken = default);
    Task<bool> ExportDistributionAsync(string name, string filePath, CancellationToken cancellationToken = default);
    Task<bool> ImportDistributionAsync(string name, string installLocation, string filePath, CancellationToken cancellationToken = default);
    Task<bool> UnregisterDistributionAsync(string name, CancellationToken cancellationToken = default);
    Task<bool> ShutdownAsync(CancellationToken cancellationToken = default);
    Task<bool> IsWslInstalledAsync(CancellationToken cancellationToken = default);
    Task<string> GetWslVersionAsync(CancellationToken cancellationToken = default);
    Task<bool> StartWslServiceAsync(CancellationToken cancellationToken = default);
    Task<bool> StopWslServiceAsync(CancellationToken cancellationToken = default);
    Task<string?> GetWslServiceStatusAsync(CancellationToken cancellationToken = default);
    Task<string?> GetLxssManagerStatusAsync(CancellationToken cancellationToken = default);
    Task<bool> StartLxssManagerAsync(CancellationToken cancellationToken = default);
    Task<bool> StopLxssManagerAsync(CancellationToken cancellationToken = default);
    Task<string> GetDistributionListRawAsync(CancellationToken cancellationToken = default);
    Task<(double CpuPercent, double MemoryMB, string Diagnostics)> GetDistributionResourceAsync(string name, CancellationToken cancellationToken = default);
    Task<(int ExitCode, string StdOut, string StdErr)> ExecuteAsync(string arguments, CancellationToken cancellationToken = default);
}
