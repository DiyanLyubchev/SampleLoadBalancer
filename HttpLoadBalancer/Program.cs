// See https://aka.ms/new-console-template for more information

using HttpLoadBalancer;
using System.Net;

List<string> backendServers =
[
    "https://localhost:7205/server",
    "https://localhost:7066/server"
];

LoadBalancer lb = new(backendServers);

HttpListener listener = new();
listener.Prefixes.Add("http://localhost:8080/");
listener.Start();
Console.WriteLine("Load balancer started on http://localhost:8080/");

while (true)
{
    var context = await listener.GetContextAsync();
    _ = Task.Run(() => lb.HandleRequestAsync(context));
}


