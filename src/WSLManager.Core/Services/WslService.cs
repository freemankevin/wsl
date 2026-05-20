using System.Diagnostics;
using System.ServiceProcess;
using System.Text;
using WSLManager.Core.Models;

namespace WSLManager.Core.Services;

public sealed class WslService : IWslService
{
    private readonly Dictionary<string, (DateTime Time, long User, long Nice, long System, long Idle, long IoWait)> _prevCpuStats = new();

    public async Task<IReadOnlyList<WslDistribution>> GetDistributionsAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunCommandAsync("--list --verbose", Encoding.Unicode, cancellationToken);
        if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StdOut))
        {
            return ParseDistributions(result.StdOut);
        }

        return Array.Empty<WslDistribution>();
    }

    private static List<WslDistribution> ParseDistributions(string output)
    {
        var list = new List<WslDistribution>();
        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        string? defaultName = null;

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.StartsWith("NAME", StringComparison.OrdinalIgnoreCase)) continue;
            if (line.StartsWith("名称", StringComparison.OrdinalIgnoreCase)) continue;

            bool isDefault = line.StartsWith("*");
            if (isDefault) line = line.TrimStart('*', ' ');

            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) continue;

            if (!int.TryParse(parts[^1], out int version)) continue;

            var name = parts[0];
            var state = parts[^2];

            if (isDefault) defaultName = name;

            list.Add(new WslDistribution
            {
                Name = name,
                State = state,
                WslVersion = version,
                IsDefault = isDefault
            });
        }

        if (defaultName != null)
        {
            foreach (var d in list)
                if (d.Name == defaultName) d.IsDefault = true;
        }

        return list;
    }

    public async Task<bool> StartDistributionAsync(string name, CancellationToken cancellationToken = default)
    {
        // Older WSL versions don't support the '--' separator
        var result = await RunCommandAsync($"-d {name} /bin/true", cancellationToken);
        if (result.ExitCode == 0) return true;

        // Modern WSL syntax with '--' separator
        result = await RunCommandAsync($"-d {name} -- /bin/true", cancellationToken);
        return result.ExitCode == 0;
    }

    public async Task<bool> StopDistributionAsync(string name, CancellationToken cancellationToken = default)
    {
        // Try direct execution without quotes
        var result = await RunCommandAsync($"--terminate {name}", cancellationToken);
        if (result.ExitCode == 0) return true;

        // Try with quoted name
        result = await RunCommandAsync($"--terminate {Escape(name)}", cancellationToken);
        if (result.ExitCode == 0) return true;

        // Try short option
        result = await RunCommandAsync($"-t {name}", cancellationToken);
        if (result.ExitCode == 0) return true;

        // Fallback via cmd.exe (some systems have issues launching wsl.exe directly)
        result = await RunCommandAsync($"/c wsl.exe --terminate {name}", Encoding.GetEncoding(0), cancellationToken, "cmd.exe");
        return result.ExitCode == 0;
    }

    public async Task<bool> TerminateDistributionAsync(string name, CancellationToken cancellationToken = default)
    {
        var result = await RunCommandAsync($"--terminate {Escape(name)}", cancellationToken);
        return result.ExitCode == 0;
    }

    public async Task<bool> SetDefaultDistributionAsync(string name, CancellationToken cancellationToken = default)
    {
        var result = await RunCommandAsync($"--set-default {Escape(name)}", cancellationToken);
        return result.ExitCode == 0;
    }

    public async Task<bool> ExportDistributionAsync(string name, string filePath, CancellationToken cancellationToken = default)
    {
        var result = await RunCommandAsync($"--export {Escape(name)} \"{filePath}\"", cancellationToken);
        return result.ExitCode == 0;
    }

    public async Task<bool> ImportDistributionAsync(string name, string installLocation, string filePath, CancellationToken cancellationToken = default)
    {
        var result = await RunCommandAsync($"--import {Escape(name)} \"{installLocation}\" \"{filePath}\"", cancellationToken);
        return result.ExitCode == 0;
    }

    public async Task<bool> UnregisterDistributionAsync(string name, CancellationToken cancellationToken = default)
    {
        var result = await RunCommandAsync($"--unregister {Escape(name)}", cancellationToken);
        return result.ExitCode == 0;
    }

    public async Task<bool> ShutdownAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunCommandAsync("--shutdown", cancellationToken);
        return result.ExitCode == 0;
    }

    public async Task<bool> IsWslInstalledAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await RunCommandAsync("--status", cancellationToken);
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> GetWslVersionAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunCommandAsync("--version", cancellationToken);
        if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StdOut))
            return result.StdOut.Trim();

        result = await RunCommandAsync("--status", cancellationToken);
        if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.StdOut))
            return result.StdOut.Trim();

        return "未知版本";
    }

    public Task<bool> StartWslServiceAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                using var controller = new ServiceController("wslservice");
                if (controller.Status == ServiceControllerStatus.Stopped)
                {
                    controller.Start();
                    controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                }
                return true;
            }
            catch
            {
                return false;
            }
        }, cancellationToken);
    }

    public Task<bool> StopWslServiceAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                using var controller = new ServiceController("wslservice");
                if (controller.Status == ServiceControllerStatus.Running)
                {
                    controller.Stop();
                    controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                }
                return true;
            }
            catch
            {
                return false;
            }
        }, cancellationToken);
    }

    public Task<string?> GetWslServiceStatusAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                using var controller = new ServiceController("wslservice");
                return controller.Status.ToString();
            }
            catch
            {
                return null;
            }
        }, cancellationToken);
    }

    public Task<string?> GetLxssManagerStatusAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                using var controller = new ServiceController("LxssManager");
                return controller.Status.ToString();
            }
            catch
            {
                return null;
            }
        }, cancellationToken);
    }

    public Task<bool> StartLxssManagerAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                using var controller = new ServiceController("LxssManager");
                if (controller.Status == ServiceControllerStatus.Stopped)
                {
                    controller.Start();
                    controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                }
                return true;
            }
            catch
            {
                return false;
            }
        }, cancellationToken);
    }

    public Task<bool> StopLxssManagerAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                using var controller = new ServiceController("LxssManager");
                if (controller.Status == ServiceControllerStatus.Running)
                {
                    controller.Stop();
                    controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                }
                return true;
            }
            catch
            {
                return false;
            }
        }, cancellationToken);
    }

    public async Task<string> GetDistributionListRawAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunCommandAsync("--list --verbose", Encoding.Unicode, cancellationToken);
        return $"ExitCode={result.ExitCode}\nStdOut:\n{result.StdOut}\nStdErr:\n{result.StdErr}";
    }

    public Task<(int ExitCode, string StdOut, string StdErr)> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        return RunCommandAsync(arguments, Encoding.Unicode, cancellationToken);
    }

    public async Task<(double CpuPercent, double MemoryMB, string Diagnostics)> GetDistributionResourceAsync(string name, CancellationToken ct = default)
    {
        double memMB = 0;
        double cpuPercent = 0;
        var diag = new StringBuilder();

        // ---- Memory ----
        // Try without '--' separator (older WSL)
        var memResult = await RunCommandAsync($"-d {name} free -m", Encoding.UTF8, ct);
        diag.AppendLine($"[mem1] wsl -d {name} free -m => ExitCode={memResult.ExitCode}");
        if (!string.IsNullOrWhiteSpace(memResult.StdErr)) diag.AppendLine($"[mem1] stderr: {memResult.StdErr.Trim()}");
        diag.AppendLine($"[mem1] stdout preview: {Preview(memResult.StdOut)}");

        if (memResult.ExitCode == 0)
        {
            var memLine = memResult.StdOut.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(l => l.TrimStart().StartsWith("Mem:"));
            diag.AppendLine($"[mem1] memLine found: {memLine != null}");
            if (memLine != null)
            {
                var parts = memLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                diag.AppendLine($"[mem1] parts.Length={parts.Length}, parts[2]={(parts.Length >= 3 ? parts[2] : "N/A")}");
                if (parts.Length >= 3 && double.TryParse(parts[2], out var used)) memMB = used;
                else diag.AppendLine($"[mem1] parse failed for parts[2]");
            }
        }
        else
        {
            // Fallback with '--' separator
            memResult = await RunCommandAsync($"-d {name} -- free -m", Encoding.UTF8, ct);
            diag.AppendLine($"[mem2] wsl -d {name} -- free -m => ExitCode={memResult.ExitCode}");
            if (!string.IsNullOrWhiteSpace(memResult.StdErr)) diag.AppendLine($"[mem2] stderr: {memResult.StdErr.Trim()}");
            diag.AppendLine($"[mem2] stdout preview: {Preview(memResult.StdOut)}");

            if (memResult.ExitCode == 0)
            {
                var memLine = memResult.StdOut.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault(l => l.TrimStart().StartsWith("Mem:"));
                diag.AppendLine($"[mem2] memLine found: {memLine != null}");
                if (memLine != null)
                {
                    var parts = memLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    diag.AppendLine($"[mem2] parts.Length={parts.Length}, parts[2]={(parts.Length >= 3 ? parts[2] : "N/A")}");
                    if (parts.Length >= 3 && double.TryParse(parts[2], out var used)) memMB = used;
                    else diag.AppendLine($"[mem2] parse failed for parts[2]");
                }
            }
        }

        // ---- CPU ----
        var cpuResult = await RunCommandAsync($"-d {name} cat /proc/stat", Encoding.UTF8, ct);
        diag.AppendLine($"[cpu1] wsl -d {name} cat /proc/stat => ExitCode={cpuResult.ExitCode}");
        diag.AppendLine($"[cpu1] stdout preview: {Preview(cpuResult.StdOut)}");

        if (cpuResult.ExitCode == 0)
        {
            var cpuLine = cpuResult.StdOut.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(l => l.StartsWith("cpu "));
            diag.AppendLine($"[cpu1] cpuLine found: {cpuLine != null}");
            if (cpuLine != null)
            {
                var parts = cpuLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                diag.AppendLine($"[cpu1] parts.Length={parts.Length}, parseOk={parts.Length >= 6}");
                if (parts.Length >= 6 &&
                    long.TryParse(parts[1], out var user) &&
                    long.TryParse(parts[2], out var nice) &&
                    long.TryParse(parts[3], out var system) &&
                    long.TryParse(parts[4], out var idle) &&
                    long.TryParse(parts[5], out var iowait))
                {
                    var now = DateTime.UtcNow;
                    var total = user + nice + system + idle + iowait;
                    var idleTotal = idle + iowait;

                    lock (_prevCpuStats)
                    {
                        if (_prevCpuStats.TryGetValue(name, out var prev))
                        {
                            var totalDiff = total - (prev.User + prev.Nice + prev.System + prev.Idle + prev.IoWait);
                            var idleDiff = idleTotal - (prev.Idle + prev.IoWait);
                            if (totalDiff > 0)
                            {
                                cpuPercent = (1.0 - (double)idleDiff / totalDiff) * 100.0;
                                cpuPercent = Math.Max(0, Math.Min(100, cpuPercent));
                                diag.AppendLine($"[cpu1] calculated={cpuPercent:F2}% totalDiff={totalDiff}");
                            }
                            else
                            {
                                diag.AppendLine($"[cpu1] totalDiff<=0, skipping");
                            }
                        }
                        else
                        {
                            diag.AppendLine($"[cpu1] no prev data, storing baseline");
                        }
                        _prevCpuStats[name] = (now, user, nice, system, idle, iowait);
                    }
                }
                else
                {
                    diag.AppendLine($"[cpu1] parse failed");
                }
            }
        }
        else
        {
            cpuResult = await RunCommandAsync($"-d {name} -- cat /proc/stat", Encoding.UTF8, ct);
            diag.AppendLine($"[cpu2] wsl -d {name} -- cat /proc/stat => ExitCode={cpuResult.ExitCode}");
            diag.AppendLine($"[cpu2] stdout preview: {Preview(cpuResult.StdOut)}");

            if (cpuResult.ExitCode == 0)
            {
                var cpuLine = cpuResult.StdOut.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault(l => l.StartsWith("cpu "));
                diag.AppendLine($"[cpu2] cpuLine found: {cpuLine != null}");
                if (cpuLine != null)
                {
                    var parts = cpuLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    diag.AppendLine($"[cpu2] parts.Length={parts.Length}, parseOk={parts.Length >= 6}");
                    if (parts.Length >= 6 &&
                        long.TryParse(parts[1], out var user) &&
                        long.TryParse(parts[2], out var nice) &&
                        long.TryParse(parts[3], out var system) &&
                        long.TryParse(parts[4], out var idle) &&
                        long.TryParse(parts[5], out var iowait))
                    {
                        var now = DateTime.UtcNow;
                        var total = user + nice + system + idle + iowait;
                        var idleTotal = idle + iowait;

                        lock (_prevCpuStats)
                        {
                            if (_prevCpuStats.TryGetValue(name, out var prev))
                            {
                                var totalDiff = total - (prev.User + prev.Nice + prev.System + prev.Idle + prev.IoWait);
                                var idleDiff = idleTotal - (prev.Idle + prev.IoWait);
                                if (totalDiff > 0)
                                {
                                    cpuPercent = (1.0 - (double)idleDiff / totalDiff) * 100.0;
                                    cpuPercent = Math.Max(0, Math.Min(100, cpuPercent));
                                    diag.AppendLine($"[cpu2] calculated={cpuPercent:F2}% totalDiff={totalDiff}");
                                }
                                else
                                {
                                    diag.AppendLine($"[cpu2] totalDiff<=0, skipping");
                                }
                            }
                            else
                            {
                                diag.AppendLine($"[cpu2] no prev data, storing baseline");
                            }
                            _prevCpuStats[name] = (now, user, nice, system, idle, iowait);
                        }
                    }
                    else
                    {
                        diag.AppendLine($"[cpu2] parse failed");
                    }
                }
            }
        }

        return (Math.Round(cpuPercent, 2), Math.Round(memMB, 2), diag.ToString());
    }

    private static string Preview(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "(empty)";
        var preview = s.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
        if (preview.Length > 300) preview = preview.Substring(0, 300) + "...";
        return preview;
    }

    private static string Escape(string arg) => $"\"{arg.Replace("\"", "\\\"")}\"";

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunCommandAsync(string arguments, Encoding encoding, CancellationToken ct, string fileName = "wsl.exe")
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = encoding,
            StandardErrorEncoding = encoding,
        };

        using var process = Process.Start(psi);
        if (process == null) return (-1, "", $"Failed to start {fileName}");

        using (ct.Register(() => { try { process.Kill(); } catch { } }))
        {
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync(ct);
            return (process.ExitCode, await stdout, await stderr);
        }
    }

    private static Task<(int ExitCode, string StdOut, string StdErr)> RunCommandAsync(string arguments, CancellationToken ct)
        => RunCommandAsync(arguments, Encoding.Unicode, ct);
}
