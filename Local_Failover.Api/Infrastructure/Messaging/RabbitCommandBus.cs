using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Ports;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Infrastructure.Messaging;

public sealed class RabbitCommandBus : ICommandBus, IDisposable
{
    private readonly RabbitConnection _conn;
    private readonly IModel _ch;
    private readonly string _exchangeCmd = "cmd";
    private readonly ConcurrentDictionary<string, TaskCompletionSource<AckResult>> _pending = new();

    public RabbitCommandBus(RabbitConnection conn)
    {
        _conn = conn;
        _ch = _conn.CreateChannel();

        // declare topic exchange for commands
        _ch.ExchangeDeclare(_exchangeCmd, ExchangeType.Topic, durable: true, autoDelete: false);

        // Direct Reply-to consumer
        var consumer = new AsyncEventingBasicConsumer(_ch);
        consumer.Received += async (_, ea) =>
        {
            try
            {
                var corr = ea.BasicProperties?.CorrelationId ?? "";
                if (_pending.TryRemove(corr, out var tcs))
                {
                    var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var ack = JsonSerializer.Deserialize<AckResult>(body) ?? new AckResult(false, 500, "bad ack");
                    tcs.TrySetResult(ack);
                }
            }
            catch { /* swallow */ }
            await Task.CompletedTask;
        };
        _ch.BasicConsume(queue: "amq.rabbitmq.reply-to", autoAck: true, consumer: consumer);
    }

    public Task<AckResult> SendWithAckAsync(CommandEnvelope cmd, TimeSpan timeout, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<AckResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[cmd.CorrelationId] = tcs;

        var props = _ch.CreateBasicProperties();
        props.ContentType = "application/json";
        props.DeliveryMode = 2; // persistent
        props.CorrelationId = cmd.CorrelationId;
        props.ReplyTo = "amq.rabbitmq.reply-to";

        var rk = $"cmd.to.cloud.{cmd.TenantId}.{cmd.Entity}.{cmd.Action}";
        var json = JsonSerializer.Serialize(cmd);
        var body = Encoding.UTF8.GetBytes(json);

        _ch.BasicPublish(exchange: _exchangeCmd, routingKey: rk, basicProperties: props, body: body);

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(timeout, ct);
                if (_pending.TryRemove(cmd.CorrelationId, out var pending))
                    pending.TrySetResult(new AckResult(false, 504, "ack timeout"));
            }
            catch { /* ignore */ }
        }, ct);

        return tcs.Task;
    }

    public void Dispose()
    {
        try { _ch?.Dispose(); } catch { }
    }
}
