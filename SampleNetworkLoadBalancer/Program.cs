// See https://aka.ms/new-console-template for more information

using System.Diagnostics;

string projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
string logFilePath = Path.Combine(projectRoot, "NetworkLoadBalancerLog.txt");
TimeSpan interval = TimeSpan.FromMinutes(30);
string urlToCheck = "https://www.mobile.bg/";

Console.WriteLine($"Monitoring {urlToCheck} every 30 minutes.");

using var httpClient = new HttpClient();

while (true)
{
    string status;
    long responseSize = 0;
    long responseTimeMs = 0;

    try
    {
        var stopwatch = Stopwatch.StartNew();
        var response = await httpClient.GetAsync(urlToCheck);
        stopwatch.Stop();
        responseTimeMs = stopwatch.ElapsedMilliseconds;

        status = response.IsSuccessStatusCode
            ? $"Success ({(int)response.StatusCode})"
            : $"Failed ({(int)response.StatusCode})";

        if (response.Content.Headers.ContentLength.HasValue)
        {
            responseSize = response.Content.Headers.ContentLength.Value;
        }
        else
        {
            var contentBytes = await response.Content.ReadAsByteArrayAsync();
            responseSize = contentBytes.Length;
        }
    }
    catch (Exception ex)
    {
        status = $"Error: {ex.Message}";
    }

    string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | Link: {urlToCheck} | Status: {status} | " +
                      $"Response Time: {responseTimeMs} ms | Response Size: {responseSize} bytes";
    Console.WriteLine(logEntry);

    await File.AppendAllTextAsync(logFilePath, logEntry + Environment.NewLine);

    if (responseSize > 1000000) 
    {
        SendMailDummy();
    }

    await Task.Delay(interval);
}

static void SendMailDummy()
{
    Console.WriteLine("ALERT: Large response detected! Sending notification email...");
}