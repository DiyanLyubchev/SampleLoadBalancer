using System.Net;

namespace HttpLoadBalancer;

public class LoadBalancer(List<string> servers)
{
    private readonly List<string> servers = servers;
    private int lastIndex = -1;
    private readonly object lockObject = new();

    private string GetNextServer()
    {
        lock (this.lockObject)
        {
            this.lastIndex = (this.lastIndex + 1) % this.servers.Count;
            return this.servers[this.lastIndex];
        }
    }

    public async Task HandleRequestAsync(HttpListenerContext context)
    {
        string targetServer = GetNextServer();
        string targetUrl = targetServer + context.Request.RawUrl?.TrimStart('/');

        using HttpClient client = new();

        try
        {
            HttpRequestMessage forwardRequest = new(new HttpMethod(context.Request.HttpMethod), targetUrl);

            if (context.Request.HasEntityBody)
            {
                using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                string body = await reader.ReadToEndAsync();
                forwardRequest.Content = new StringContent(body);
            }

            foreach (string headerKey in context.Request.Headers.AllKeys)
            {
                forwardRequest.Headers.TryAddWithoutValidation(headerKey, context.Request.Headers[headerKey]);
            }

            HttpResponseMessage response = await client.SendAsync(forwardRequest);

            context.Response.StatusCode = (int)response.StatusCode;

            foreach (var header in response.Headers)
            {
                context.Response.Headers[header.Key] = string.Join(",", header.Value);
            }

            string responseBody = await response.Content.ReadAsStringAsync();
            using StreamWriter writer = new(context.Response.OutputStream);
            await writer.WriteAsync(responseBody);
            context.Response.Close();

            Console.WriteLine($"Forwarded request to {targetServer}");
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            using var writer = new StreamWriter(context.Response.OutputStream);
            await writer.WriteAsync($"Error: {ex.Message}");
            context.Response.Close();
            Console.WriteLine($"Error forwarding to {targetServer}: {ex.Message}");
        }
    }
}
