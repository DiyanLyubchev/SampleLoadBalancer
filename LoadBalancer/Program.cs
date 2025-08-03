// See https://aka.ms/new-console-template for more information
using System.Collections.Generic;

List<Server> servers =
[
    new ("Server 1"),
    new ("Server 2"),
    new ("Server 3")
];

LoadBalancer lb = new (servers);

for (int i = 1; i <= 10; i++)
{
    string request = $"Request {i}";
    lb.RouteRequest(request);
    Thread.Sleep(500); 
}

Console.ReadLine();

class Server(string name)
{
    public string Name { get; private set; } = name;

    public void HandleRequest(string request)
    {
        Console.WriteLine($"{Name} handled {request}");
    }
}

class LoadBalancer(List<Server> servers)
{
    private readonly List<Server> servers = servers ?? throw new ArgumentNullException(nameof(servers));
    private int lastServerIndex = -1;
    private readonly object lockObject = new();

    public void RouteRequest(string request)
    {
        Server server = GetNextServer();
        server.HandleRequest(request);
    }

    private Server GetNextServer()
    {
        lock (this.lockObject)
        {
            this.lastServerIndex = (this.lastServerIndex + 1) % this.servers.Count;
            return this.servers[this.lastServerIndex];
        }
    }
}