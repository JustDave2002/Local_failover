using System.Text;
using System.Text.Json;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Infrastructure.Messaging;

public sealed class CommandConsumer : BackgroundService
{
    private readonly RabbitConnection _conn;
    private readonly ILogger<CommandConsumer> _log;
    private readonly ErpDbContext _db;
    private readonly IConfiguration _cfg;
    private IModel? _ch;
    private readonly string _exchangeCmd = "cmd";
    private string _queueName = "";
    private readonly Dictionary<(string entity, string action), Func<JsonElement, Task<AckResultDto>>> _handlers;

    public CommandConsumer(RabbitConnection conn, ILogger<CommandConsumer> log, ErpDbContext db, IConfiguration cfg)
    { _conn = conn; _log = log; _db = db; _cfg = cfg; _handlers = new()
    {
        [("stockmovement", "post")] = HandleStockMovementPostAsync,
        [("salesorder",    "post")] = HandleSalesOrderPostAsync
    }; }

    

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _ch = _conn.CreateChannel();
        _ch.ExchangeDeclare(_exchangeCmd, ExchangeType.Topic, durable: true, autoDelete: false);

        var tenantId = _cfg["Tenant:Id"] ?? "T1";
        _queueName = $"q.cmd.to.cloud.{tenantId}";
        _ch.QueueDeclare(_queueName, durable: true, exclusive: false, autoDelete: false);
        _ch.QueueBind(_queueName, _exchangeCmd, $"cmd.to.cloud.{tenantId}.#.post");

        var consumer = new AsyncEventingBasicConsumer(_ch);
        consumer.Received += OnReceivedAsync;
        _ch.BasicQos(0, 1, false);
        _ch.BasicConsume(_queueName, autoAck: false, consumer: consumer);

        _log.LogInformation("[CMD] Consuming {queue}", _queueName);
        return Task.CompletedTask;
    }

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        var props = ea.BasicProperties;
        var replyTo = props?.ReplyTo;
        var corrId = props?.CorrelationId ?? Guid.NewGuid().ToString();

        try
        {
            var json = Encoding.UTF8.GetString(ea.Body.ToArray());
            var env = JsonSerializer.Deserialize<CommandEnvelopeDto>(json);
            if (env is null)
            {
                await SendAck(replyTo, corrId, new AckResultDto(false, 400, "bad envelope"));
                _ch!.BasicNack(ea.DeliveryTag, false, requeue: false);
                return;
            }

            if (_handlers.TryGetValue((env.Entity, env.Action), out var handler))
            {
                var ack = await handler(env.Payload);
                await SendAck(replyTo, corrId, ack);
                _ch!.BasicAck(ea.DeliveryTag, false);
                return;
            }
            await SendAck(replyTo, corrId, new AckResultDto(false, 400, "unknown command"));
            _ch!.BasicNack(ea.DeliveryTag, false, requeue: false);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[CMD] Error handling command");
            try { await SendAck(replyTo, corrId, new AckResultDto(false, 500, ex.Message)); } catch { }
            _ch!.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
        }
    }

    private Task SendAck(string? replyTo, string correlationId, AckResultDto ack)
    {
        if (string.IsNullOrWhiteSpace(replyTo)) return Task.CompletedTask;

        var pk = _ch!.CreateBasicProperties();
        pk.ContentType = "application/json";
        pk.CorrelationId = correlationId;

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(ack));
        _ch.BasicPublish(exchange: "", routingKey: replyTo, basicProperties: pk, body: body);
        return Task.CompletedTask;
    }

    // handlers:
    private async Task<AckResultDto> HandleStockMovementPostAsync(JsonElement payload)
    {
        var sm = JsonSerializer.Deserialize<StockMovement>(payload.GetRawText());
        if (sm is null) return new AckResultDto(false, 400, "bad payload");
        sm.CreatedAtUtc = DateTime.UtcNow;
        _db.StockMovements.Add(sm);
        await _db.SaveChangesAsync();
        return new AckResultDto(true, 200, "ok");
    }

    private async Task<AckResultDto> HandleSalesOrderPostAsync(JsonElement payload)
    {
        var so = JsonSerializer.Deserialize<SalesOrder>(payload.GetRawText());
        if (so is null) return new AckResultDto(false, 400, "bad payload");
        so.CreatedAtUtc = DateTime.UtcNow;
        _db.SalesOrders.Add(so);
        await _db.SaveChangesAsync();
        return new AckResultDto(true, 200, "ok");
    }

    private sealed class CommandEnvelopeDto
{
    public string TenantId { get; set; } = "";
    public string Entity { get; set; } = "";
    public string Action { get; set; } = "";
    public JsonElement Payload { get; set; } = default;
    public string CorrelationId { get; set; } = "";
}
    private record AckResultDto(bool Ok, int Status, string? Message);
}
