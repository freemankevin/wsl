using System.Collections.Concurrent;
using System.Diagnostics;
using WSLManager.Core.Models;

namespace WSLManager.Core.Services;

public sealed class ResourceMonitorService : IResourceMonitorService, IDisposable
{
    private Timer? _timer;
    private readonly object _lock = new();
    private TimeSpan _interval = TimeSpan.FromSeconds(2);
    private readonly ConcurrentQueue<WslResourceInfo> _history = new();
    private const int MaxHistory = 120; // ~4 minutes at 2s interval

    private readonly Dictionary<int, (DateTime Time, TimeSpan Cpu)> _prevProcessCpu = new();

    public event EventHandler<WslResourceInfo>? ResourceInfoUpdated;

    public bool IsMonitoring => _timer != null;

    public void StartMonitoring(TimeSpan? interval = null)
    {
        lock (_lock)
        {
            StopMonitoring();
            _interval = interval ?? TimeSpan.FromSeconds(2);
            _timer = new Timer(OnTimerTick, null, TimeSpan.Zero, _interval);
        }
    }

    public void StopMonitoring()
    {
        lock (_lock)
        {
            _timer?.Dispose();
            _timer = null;
        }
    }

    private void OnTimerTick(object? state)
    {
        try
        {
            var info = CollectResourceInfo();
            _history.Enqueue(info);
            while (_history.Count > MaxHistory)
                _history.TryDequeue(out _);
            ResourceInfoUpdated?.Invoke(this, info);
        }
        catch
        {
            // Silently ignore monitoring errors to avoid spamming the UI.
        }
    }

    private WslResourceInfo CollectResourceInfo()
    {
        var processes = Process.GetProcesses();
        double totalCpuPercent = 0;
        long totalMemoryBytes = 0;
        int wslProcessCount = 0;

        var now = DateTime.UtcNow;
        var currentProcessCpu = new Dictionary<int, (DateTime, TimeSpan)>();

        foreach (var proc in processes)
        {
            try
            {
                var name = proc.ProcessName.ToLowerInvariant();
                bool isWslRelated = name.Contains("wsl") || name.Contains("vmcompute") || name.Contains("vmmem");
                if (!isWslRelated) continue;

                totalMemoryBytes += proc.WorkingSet64;
                wslProcessCount++;

                try
                {
                    var cpuTime = proc.TotalProcessorTime;
                    currentProcessCpu[proc.Id] = (now, cpuTime);

                    if (_prevProcessCpu.TryGetValue(proc.Id, out var prev))
                    {
                        var cpuDelta = cpuTime - prev.Cpu;
                        var timeDelta = (now - prev.Time).TotalSeconds;
                        if (timeDelta > 0.1)
                        {
                            // TotalProcessorTime spans all cores; normalize to system-wide percentage
                            var procCpuPercent = cpuDelta.TotalSeconds / (Environment.ProcessorCount * timeDelta) * 100;
                            totalCpuPercent += Math.Max(0, procCpuPercent);
                        }
                    }
                }
                catch { /* ignore processes we cannot query */ }
            }
            catch
            {
                // Ignore processes we cannot access.
            }
            finally
            {
                proc.Dispose();
            }
        }

        // Update snapshot for next iteration
        _prevProcessCpu.Clear();
        foreach (var kvp in currentProcessCpu)
        {
            _prevProcessCpu[kvp.Key] = kvp.Value;
        }

        double memoryMB = totalMemoryBytes / (1024.0 * 1024.0);
        var totalPhysicalMB = GetTotalPhysicalMemoryMB();

        return new WslResourceInfo
        {
            CpuUsagePercent = Math.Round(Math.Min(totalCpuPercent, 100.0), 2),
            MemoryUsageMB = Math.Round(memoryMB, 2),
            MemoryTotalMB = totalPhysicalMB,
            DiskReadMBPerSec = 0,
            DiskWriteMBPerSec = 0,
            Timestamp = DateTime.Now
        };
    }

    private static double GetTotalPhysicalMemoryMB()
    {
        try
        {
            var gcMemory = GC.GetGCMemoryInfo();
            return Math.Round(gcMemory.TotalAvailableMemoryBytes / (1024.0 * 1024.0), 2);
        }
        catch
        {
            return 8192;
        }
    }

    public void Dispose()
    {
        StopMonitoring();
    }

    public IReadOnlyList<WslResourceInfo> GetHistory() => _history.ToList();

    public Task<List<WslResourceInfo>> GetResourceUsageAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_history.ToList());
    }

    public Task<WslResourceInfo?> GetDistributionResourceAsync(string distributionName, CancellationToken ct = default)
    {
        // Currently returns the most recent global resource snapshot.
        // Per-distribution resource tracking can be added later.
        var latest = _history.LastOrDefault();
        return Task.FromResult(latest);
    }
}
