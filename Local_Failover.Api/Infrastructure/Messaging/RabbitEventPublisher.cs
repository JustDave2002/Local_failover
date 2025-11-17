using System.Text;
using System.Text.Json;
using Ports;
using RabbitMQ.Client;

namespace Infrastructure.Messaging;

public sealed class RabbitEventPublisher : IEventPublisher
{
    private readonly RabbitConnection _conn;
    private readonly IModel _ch;
    private readonly string _exchange = "evt";

    public RabbitEventPublisher(RabbitConnection conn)
    {
        _conn = conn;
        _ch = _conn.CreateChannel();
        _ch.ExchangeDeclare(_exchange, ExchangeType.Topic, durable: true, autoDelete: false);
    }

    public Task PublishAsync(string tenantId, string entity, string @event, object payload, string eventId)
    {
        var rk = $"evt.{tenantId}.{entity}.{@event}"; // evt.T1.stockmovement.created
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { eventId, payload }));
        var props = _ch.CreateBasicProperties();
        props.ContentType = "application/json";
        props.DeliveryMode = 2;

        _ch.BasicPublish(_exchange, rk, props, body);
        return Task.CompletedTask;
    }
}
