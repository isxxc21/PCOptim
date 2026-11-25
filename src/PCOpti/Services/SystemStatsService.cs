using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PCOpti.Services;

public sealed record SystemStats(double CpuPercent, double RamPercent, double RamUsedGb, double RamTotalGb,
    double DiskPercent, double? GpuPercent);

/// <summary>
/// Reads live usage from Windows performance counters (the same source as Task Manager).
/// All counter work happens on a background thread; the UI only receives finished numbers.
/// </summary>
public sealed class SystemStatsService : IDisposable
{
    private PerformanceCounter? _cpu;
    private PerformanceCounter? _disk;
    private List<PerformanceCounter> _gpu = new();
    private DateTime _gpuRefreshed = DateTime.MinValue;
    private CancellationTokenSource? _cts;

    public event EventHandler<SystemStats>? Updated;

    public void Start()
    {
        if (_cts is not null) return;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        Task.Run(async () =>
        {
            Init();
            while (!token.IsCancellationRequested)
            {
                try { Updated?.Invoke(this, Sample()); } catch { /* counters can disappear (e.g. driver reset) */ }
                try { await Task.Delay(1500, token); } catch (TaskCanceledException) { break; }
            }
        }, token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
    }

    private void Init()
    {
        // "% Processor Utility" matches Task Manager on Windows 10/11; fall back for older systems.
        _cpu = TryCounter("Processor Information", "% Processor Utility", "_Total")
               ?? TryCounter("Processor", "% Processor Time", "_Total");
        _disk = TryCounter("PhysicalDisk", "% Idle Time", "_Total");
        _cpu?.NextValue();
        _disk?.NextValue();
    }

    private static PerformanceCounter? TryCounter(string category, string counter, string instance)
    {
        try
        {
            var c = new PerformanceCounter(category, counter, instance, readOnly: true);
            c.NextValue();
            return c;
        }
        catch { return null; }
    }

    private SystemStats Sample()
    {
        var cpu = Math.Clamp(_cpu?.NextValue() ?? 0, 0, 100);
        var disk = _disk is null ? 0 : Math.Clamp(100 - _disk.NextValue(), 0, 100);
        var (ramPct, used, total) = ReadMemory();
        return new SystemStats(cpu, ramPct, used, total, disk, ReadGpu());
    }

    /// <summary>GPU usage = highest per-adapter sum of 3D engine utilisation (how Task Manager shows "3D").</summary>
    private double? ReadGpu()
    {
        try
        {
            // GPU engine instances appear and disappear with processes, so refresh the list regularly.
            if (DateTime.UtcNow - _gpuRefreshed > TimeSpan.FromSeconds(10))
            {
                foreach (var c in _gpu) c.Dispose();
                _gpu = new();
                if (PerformanceCounterCategory.Exists("GPU Engine"))
                {
                    var cat = new PerformanceCounterCategory("GPU Engine");
                    foreach (var name in cat.GetInstanceNames().Where(n => n.EndsWith("engtype_3D", StringComparison.OrdinalIgnoreCase)))
                    {
                        var c = new PerformanceCounter("GPU Engine", "Utilization Percentage", name, true);
                        c.NextValue();
                        _gpu.Add(c);
                    }
                }
                _gpuRefreshed = DateTime.UtcNow;
                return null; // first read after refresh is always 0
            }
            if (_gpu.Count == 0) return null;

            double total = 0;
            foreach (var c in _gpu)
            {
                try { total += c.NextValue(); } catch { /* instance ended */ }
            }
            return Math.Clamp(total, 0, 100);
        }
        catch { return null; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength, dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);

    private static (double Pct, double UsedGb, double TotalGb) ReadMemory()
    {
        var m = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!GlobalMemoryStatusEx(ref m) || m.ullTotalPhys == 0) return (0, 0, 0);
        const double gb = 1024d * 1024 * 1024;
        var used = (m.ullTotalPhys - m.ullAvailPhys) / gb;
        return (used / (m.ullTotalPhys / gb) * 100, used, m.ullTotalPhys / gb);
    }

    public void Dispose()
    {
        Stop();
        _cpu?.Dispose();
        _disk?.Dispose();
        foreach (var c in _gpu) c.Dispose();
    }
}
