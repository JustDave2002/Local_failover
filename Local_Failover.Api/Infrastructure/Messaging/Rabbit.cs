using RabbitMQ.Client;
// using RabbitMQ.Client.IModel;

namespace Infrastructure.Messaging;

public sealed class RabbitConnection : IDisposable
{
    public IConnection Connection { get; }

    public RabbitConnection(IConfiguration cfg)
    {
        var factory = new ConnectionFactory
        {
            HostName = cfg["Rabbit:HostName"] ?? "localhost",
            Port = int.TryParse(cfg["Rabbit:Port"], out var p) ? p : 5672,
            UserName = cfg["Rabbit:UserName"] ?? "guest",
            Password = cfg["Rabbit:Password"] ?? "guest",
            VirtualHost = cfg["Rabbit:VirtualHost"] ?? "/",
            DispatchConsumersAsync = true
        };
        Connection = factory.CreateConnection(cfg["Rabbit:ClientName"] ?? "local-failover");
    }

    public IModel CreateChannel() => Connection.CreateModel();

    public void Dispose() => Connection.Dispose();
}
