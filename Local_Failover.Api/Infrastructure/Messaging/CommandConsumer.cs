using System.Text;
using System.Text.Json;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Ports;
using Api.Controllers; 
using Domain.Types;

namespace Infrastructure.Messaging;

public sealed class CommandConsumer : BackgroundService
{
    private readonly RabbitConnection _conn;
    private readonly ILogger<CommandConsumer> _log;
    private readonly IServiceProvider _sp;
    private readonly IConfiguration _cfg;
    private readonly string _exchangeCmd = "cmd";
    private IModel? _ch;
    private string _queueName = "";
    private readonly IFenceStateProvider _fence;

    // dynamische routing-tabel
    private readonly Dictionary<(string entity, string action), Func<JsonElement, IServiceScope, Task<AckResultDto>>> _handlers;

    public CommandConsumer(
        RabbitConnection conn,
        ILogger<CommandConsumer> log,
        IServiceProvider sp,
        IConfiguration cfg,
        IFenceStateProvider fence)
    {
        _conn = conn;
        _log = log;
        _sp  = sp;
        _cfg = cfg;
        _fence = fence;

        _handlers = new()
        {
            // elke entry roept de juiste controller aan
            [("salesorder", "post")] = async (payload, scope) =>
            {
                _log.LogInformation("[CMD] Processing salesorder");
                var ctrl = scope.ServiceProvider.GetRequiredService<SalesOrdersController>();
                var req = JsonSerializer.Deserialize<CreateSalesOrderRequest>(payload.GetRawText());
                await ctrl.Create(req!, CancellationToken.None);
                return new AckResultDto(true, 200, "salesorder handled via controller");
            },

            [("stockmovement", "post")] = async (payload, scope) =>
            {
                var ctrl = scope.ServiceProvider.GetRequiredService<StockMovementController>();
                var req = JsonSerializer.Deserialize<PostStockMovementRequest>(payload.GetRawText());
                await ctrl.Post(req!, CancellationToken.None);
                return new AckResultDto(true, 200, "stockmovement handled via controller");
            }
        };
    }

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

        _log.LogInformation("[CMD] Consuming queue {Queue}", _queueName);
        return Task.CompletedTask;
    }

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        var props = ea.BasicProperties;
        var replyTo = props?.ReplyTo;
        var corrId  = props?.CorrelationId ?? Guid.NewGuid().ToString();
        //TODO: dynamic tenant Id
        var fence = _fence.GetFenceMode("t1");

        try
        {
            var json = Encoding.UTF8.GetString(ea.Body.ToArray());
            var env  = JsonSerializer.Deserialize<CommandEnvelopeDto>(json);

            if (env is null)
            {
                await SendAck(replyTo, corrId, new AckResultDto(false, 400, "bad envelope"));
                _ch!.BasicNack(ea.DeliveryTag, false, requeue: false);
                return;
            }

            if (fence == FenceMode.Fenced) 
            {
                await SendAck(replyTo, corrId, new AckResultDto(false, 423, "Refused"));
            }

            _log.LogInformation("[CMD] Received {Entity}.{Action} for tenant={Tenant}", env.Entity, env.Action, env.TenantId);

            if (_handlers.TryGetValue((env.Entity, env.Action), out var handler))
            {
                using var scope = _sp.CreateScope();
                var ack = await handler(env.Payload, scope);
                await SendAck(replyTo, corrId, ack);
                _ch!.BasicAck(ea.DeliveryTag, false);
                _log.LogInformation("[CMD] ✅ Applied {Entity}.{Action} via controller", env.Entity, env.Action);
                return;
            }

            await SendAck(replyTo, corrId, new AckResultDto(false, 400, "unknown command"));
            _ch!.BasicNack(ea.DeliveryTag, false, requeue: false);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[CMD] ❌ Error handling command");
            try { await SendAck(replyTo, corrId, new AckResultDto(false, 500, ex.Message)); } catch { }
            _ch!.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
        }
    }

    private Task SendAck(string? replyTo, string correlationId, AckResultDto ack)
    {
        if (string.IsNullOrWhiteSpace(replyTo)) return Task.CompletedTask;

        var pk = _ch!.CreateBasicProperties();
        pk.ContentType  = "application/json";
        pk.CorrelationId = correlationId;

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(ack));
        _ch.BasicPublish(exchange: "", routingKey: replyTo, basicProperties: pk, body: body);
        return Task.CompletedTask;
    }

    // nested helper DTOs

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