using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace AAT.Observer;

public sealed record GpuSample(string Name, double UtilizationPercent, long MemoryUsedBytes, long MemoryTotalBytes);

public sealed record HostSample(
    double CpuPercent,
    long MemoryUsedBytes,
    long MemoryTotalBytes,
    GpuSample? Gpu,
    string CollectionSource);

public sealed class HostMetricsState
{
    public long Idle;
    public long Kernel;
    public long User;
    public ulong LinuxIdle;
    public ulong LinuxTotal;
    public bool HasSample;
}

public static class HostMetricsReader
{
    public static double CpuPercent(ulong idleDelta, ulong totalDelta)
    {
        if (totalDelta == 0 || idleDelta > totalDelta)
            return 0;
        return Math.Clamp((totalDelta - idleDelta) * 100d / totalDelta, 0, 100);
    }

    public static bool TryParseNvidiaSmi(string? stdout, out GpuSample? sample)
    {
        sample = null;
        if (string.IsNullOrWhiteSpace(stdout))
            return false;

        var line = stdout.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        if (line is null)
            return false;

        var parts = line.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length < 4
            || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var utilization)
            || !long.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var usedMiB)
            || !long.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var totalMiB))
        {
            return false;
        }

        sample = new GpuSample(parts[0], utilization, usedMiB * 1024L * 1024L, totalMiB * 1024L * 1024L);
        return true;
    }

    public static HostSample Read(HostMetricsState state)
    {
        double cpu;
        long memoryUsed;
        long memoryTotal;
        var source = "unavailable";
        if (OperatingSystem.IsWindows())
        {
            (cpu, memoryUsed, memoryTotal) = ReadWindows(state);
            source = "host";
        }
        else if (OperatingSystem.IsLinux())
        {
            (cpu, memoryUsed, memoryTotal) = ReadLinux(state);
            source = "host";
        }
        else
        {
            cpu = 0;
            memoryUsed = 0;
            memoryTotal = 1;
        }

        var gpu = TryReadNvidiaSmi();
        if (gpu is not null)
            source = source == "host" ? "host+nvidia-smi" : "nvidia-smi";
        if (memoryTotal <= 0)
            memoryTotal = 1;
        memoryUsed = Math.Clamp(memoryUsed, 0, memoryTotal);
        return new HostSample(cpu, memoryUsed, memoryTotal, gpu, source);
    }

    private static (double Cpu, long Used, long Total) ReadWindows(HostMetricsState state)
    {
        var memory = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        var used = 0L;
        var total = 1L;
        if (GlobalMemoryStatusEx(ref memory) && memory.TotalPhys > 0)
        {
            total = (long)Math.Min(long.MaxValue, memory.TotalPhys);
            var available = (long)Math.Min(long.MaxValue, memory.AvailPhys);
            used = Math.Max(0, total - available);
        }

        double cpu = 0;
        if (GetSystemTimes(out var idle, out var kernel, out var user) && state.HasSample)
        {
            var idleDelta = (ulong)Math.Max(0, idle - state.Idle);
            var totalDelta = (ulong)Math.Max(0, (kernel - state.Kernel) + (user - state.User));
            cpu = CpuPercent(idleDelta, totalDelta);
        }

        state.Idle = idle;
        state.Kernel = kernel;
        state.User = user;
        state.HasSample = true;
        return (cpu, used, total);
    }

    private static (double Cpu, long Used, long Total) ReadLinux(HostMetricsState state)
    {
        double cpu = 0;
        if (File.Exists("/proc/stat"))
        {
            var parts = File.ReadLines("/proc/stat").FirstOrDefault()?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts is { Length: > 5 } && parts[0] == "cpu")
            {
                var values = parts.Skip(1).Select(value => ulong.TryParse(value, out var parsed) ? parsed : 0UL).ToArray();
                var total = values.Aggregate(0UL, (sum, value) => sum + value);
                var idle = values[3] + (values.Length > 4 ? values[4] : 0);
                if (state.HasSample)
                    cpu = CpuPercent(idle - state.LinuxIdle, total - state.LinuxTotal);
                state.LinuxIdle = idle;
                state.LinuxTotal = total;
                state.HasSample = true;
            }
        }

        long memTotal = 1;
        long memAvailable = 1;
        if (File.Exists("/proc/meminfo"))
        {
            foreach (var line in File.ReadLines("/proc/meminfo"))
            {
                if (line.StartsWith("MemTotal:", StringComparison.Ordinal) && TryKb(line, out var totalKb))
                    memTotal = totalKb * 1024;
                if (line.StartsWith("MemAvailable:", StringComparison.Ordinal) && TryKb(line, out var availableKb))
                    memAvailable = availableKb * 1024;
            }
        }

        return (cpu, Math.Max(0, memTotal - memAvailable), Math.Max(1, memTotal));
    }

    private static bool TryKb(string line, out long value)
    {
        value = 0;
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 && long.TryParse(parts[1], out value);
    }

    private static GpuSample? TryReadNvidiaSmi()
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=name,utilization.gpu,memory.used,memory.total --format=csv,noheader,nounits",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            if (!process.Start())
                return null;
            if (!process.WaitForExit(2000))
            {
                process.Kill(entireProcessTree: true);
                return null;
            }

            return TryParseNvidiaSmi(process.StandardOutput.ReadToEnd(), out var sample) ? sample : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out long idle, out long kernel, out long user);
}
