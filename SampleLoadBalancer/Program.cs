// See https://aka.ms/new-console-template for more information
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Management; 


string projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
string logFilePath = Path.Combine(projectRoot, "LoadBalancerLog.txt");
TimeSpan interval = TimeSpan.FromMinutes(30);

Console.WriteLine("Monitoring every 30 minutes.");

while (true)
{
    string processCpuUsage = GetApplicationCpuUsage();
    double processMemoryUsage = GetApplicationMemoryUsage();
    double systemMemoryUsage = GetSystemMemoryUsage();
    double systemCpuUsage = GetSystemCpuUsage();

    string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | " +
                      $"Application Process CPU: {processCpuUsage} | Application Process Memory: {processMemoryUsage:F2} MB | " +
                      $"System CPU: {systemCpuUsage:F2}% | System Memory Used: {systemMemoryUsage:F2} MB";

    Console.WriteLine(logEntry);

    await File.AppendAllTextAsync(logFilePath, logEntry + Environment.NewLine);

    if (systemCpuUsage >= 85)
    {
        SendMail();
    }

    await Task.Delay(interval);
}

static string GetApplicationCpuUsage()
{
    using var proc = Process.GetCurrentProcess();
    var startCpuUsage = proc.TotalProcessorTime;
    var startTime = DateTime.UtcNow;

    Thread.Sleep(500);

    var endCpuUsage = proc.TotalProcessorTime;
    var endTime = DateTime.UtcNow;

    var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
    var totalMsPassed = (endTime - startTime).TotalMilliseconds;

    int cpuCount = Environment.ProcessorCount;

    double cpuUsageTotal = (cpuUsedMs / (totalMsPassed * cpuCount)) * 100;

    return $"{cpuUsageTotal:F2}%";
}

static double GetApplicationMemoryUsage()
{
    using var proc = Process.GetCurrentProcess();
    return proc.WorkingSet64 / (1024.0 * 1024.0);
}

static double GetSystemMemoryUsage()
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        PerformanceCounter pc = new("Memory", "Available KBytes");
        double availableKB = pc.NextValue();
        double totalMB = GetTotalMemoryInMBWindows();
        return (totalMB * 1024) - availableKB;
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
        var memInfo = File.ReadAllLines("/proc/meminfo");
        double total = 0, free = 0;
        foreach (var line in memInfo)
        {
            if (line.StartsWith("MemTotal:"))
                total = double.Parse(line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1]) / 1024.0;
            if (line.StartsWith("MemAvailable:"))
                free = double.Parse(line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1]) / 1024.0;
        }
        return total - free;
    }
    else
    {
        throw new PlatformNotSupportedException("System memory info is only implemented for Windows and Linux.");
    }
}

static double GetTotalMemoryInMBWindows()
{
    PerformanceCounter pc = new("Memory", "Committed Bytes");
    return pc.NextValue() / (1024.0 * 1024.0);
}

static double GetSystemCpuUsage()
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        double cpuUsage = 0;
        var searcher = new ManagementObjectSearcher("select * from Win32_Processor");
        foreach (var obj in searcher.Get())
        {
            cpuUsage += Convert.ToDouble(obj["LoadPercentage"]);
        }
        return cpuUsage / Environment.ProcessorCount;
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
        return GetSystemCpuUsageLinux();
    }
    else
    {
        throw new PlatformNotSupportedException("System CPU info is only implemented for Windows and Linux.");
    }
}

static double GetSystemCpuUsageLinux()
{
    var cpuLine1 = File.ReadLines("/proc/stat").First(line => line.StartsWith("cpu "));
    var values1 = cpuLine1.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(double.Parse).ToArray();
    double idle1 = values1[3], total1 = values1.Sum();

    Thread.Sleep(500);

    var cpuLine2 = File.ReadLines("/proc/stat").First(line => line.StartsWith("cpu "));
    var values2 = cpuLine2.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(double.Parse).ToArray();
    double idle2 = values2[3], total2 = values2.Sum();

    double idleDelta = idle2 - idle1;
    double totalDelta = total2 - total1;

    return 100.0 * (1.0 - idleDelta / totalDelta);

}

static void SendMail()
{
    Console.WriteLine("ALERT: System CPU usage exceeded threshold. Sending notification email...");
}
