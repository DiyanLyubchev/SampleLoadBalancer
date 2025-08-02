// See https://aka.ms/new-console-template for more information
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using System.Management; 

string projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
string logFilePath = Path.Combine(projectRoot, "loadbalancer_log.txt");
TimeSpan interval = TimeSpan.FromMinutes(30);

Console.WriteLine("Simple Load Balancer started. Monitoring every 30 minutes.");

while (true)
{
    string processCpuUsage = GetApplicationCpuUsage();
    double processMemoryUsage = GetApplicationMemoryUsage();
    double systemMemoryUsage = GetSystemMemoryUsage();

    string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | " +
                      $"Application Process CPU: {processCpuUsage} | Application Process Memory: {processMemoryUsage:F2} MB | " +
                      $"System Memory Used: {systemMemoryUsage:F2} MB | System CPU Usage: {GetSystemCpuUsage()}%";

    Console.WriteLine(logEntry);

    await File.AppendAllTextAsync(logFilePath, logEntry + Environment.NewLine);

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
        double totalMB = GetTotalMemoryInMB_Windows();
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

static double GetTotalMemoryInMB_Windows()
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
    var cpuInfo = File.ReadAllLines("/proc/stat").FirstOrDefault();
    if (cpuInfo != null)
    {
        var data = cpuInfo.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        double idleCpuTime = double.Parse(data[4]);
        double totalCpuTime = 0;
        for (int i = 1; i < data.Length; i++)
        {
            totalCpuTime += double.Parse(data[i]);
        }
        double cpuUsage = ((totalCpuTime - idleCpuTime) / totalCpuTime) * 100;
        return cpuUsage;
    }
    return 0;
}
