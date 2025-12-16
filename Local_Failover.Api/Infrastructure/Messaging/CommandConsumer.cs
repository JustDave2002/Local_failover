using System.Text;
using System.Text.Json;
using Domain.Types;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Ports;

namespace Infrastructure.Messaging;

public sealed class CommandConsumer : BackgroundService
{
    private readonly RabbitConnection _conn;
    private readonly ILogger<CommandConsumer> _log;
    private readonly IConfiguration _cfg;
    private readonly IFenceStateProvider _fence;
    private readonly IAppRoleProvider _role;
    private readonly ISyncGateway _gateway;

    private const string ExchangeCmd = "cmd";
    private IModel? _ch;
    private string _queueName = "";

    public CommandConsumer(
        RabbitConnection conn,
        ILogger<CommandConsumer> log,
        IConfiguration cfg,
        IFenceStateProvider fence,
        IAppRoleProvider role,
        ISyncGateway gateway)
    {
        _conn = conn;
        _log = log;
        _cfg = cfg;
        _fence = fence;
        _role = role;
        _gateway = gateway;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _ch = _conn.CreateChannel();
        _ch.ExchangeDeclare(ExchangeCmd, ExchangeType.Topic, durable: true, autoDelete: false);

        var tenantId = _cfg["Tenant:Id"] ?? "T1";
        var target = _role.Role == AppRole.Cloud ? "cloud" : "local";

        _queueName = $"q.cmd.to.{target}.{tenantId}";
        _ch.QueueDeclare(_queueName, durable: true, exclusive: false, autoDelete: false);
        _ch.QueueBind(_queueName, ExchangeCmd, $"cmd.to.{target}.{tenantId}.#");

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
        var corrId = props?.CorrelationId ?? Guid.NewGuid().ToString();

        try
        {
            var tenantId = _cfg["Tenant:Id"] ?? "T1";
            var fence = _fence.GetFenceMode(tenantId);

            var json = Encoding.UTF8.GetString(ea.Body.ToArray());
            var env = JsonSerializer.Deserialize<CommandEnvelopeDto>(json);

            if (env is null || string.IsNullOrWhiteSpace(env.Entity) || string.IsNullOrWhiteSpace(env.Action))
            {
                await SendAck(replyTo, corrId, new AckResultDto(false, 400, "bad envelope"));
                _ch!.BasicNack(ea.DeliveryTag, false, requeue: false);
                return;
            }

            

            _log.LogInformation("[CMD] Received {Entity}.{Action} tenant={Tenant} appliedLocally={AppliedLocally}",
                env.Entity, env.Action, env.TenantId ?? tenantId, env.AppliedLocally);

            // Maak SyncRequest voor gateway
            var req = new SyncRequest(
                TenantId: string.IsNullOrWhiteSpace(env.TenantId) ? tenantId : env.TenantId!,
                Domain: env.Domain ?? "",
                Entity: env.Entity,
                Action: env.Action,
                Payload: env.Payload,
                AppliedLocally: env.AppliedLocally
            );

            var result = await _gateway.ReceiveAsync(req, CancellationToken.None);

            // ACK terug naar producer
            await SendAck(replyTo, corrId, new AckResultDto(result.Ok, result.Status, result.Message));

            // Rabbit ack/nack policy
            if (result.Ok)
            {
                _ch!.BasicAck(ea.DeliveryTag, false);
                _log.LogInformation("[CMD] ✅ Applied {Entity}.{Action} status={Status}", env.Entity, env.Action, result.Status);
                return;
            }

            // Niet-ok: 5xx => requeue, anders drop (423/400 etc)
            var requeue = result.Status >= 500;
            _ch!.BasicNack(ea.DeliveryTag, false, requeue: requeue);
            _log.LogWarning("[CMD] ❌ Failed {Entity}.{Action} status={Status} requeue={Requeue} msg={Msg}",
                env.Entity, env.Action, result.Status, requeue, result.Message);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[CMD] ❌ Error handling command");
            try { await SendAck(replyTo, corrId, new AckResultDto(false, 500, ex.Message)); } catch { }
            _ch!.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
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

    private sealed class CommandEnvelopeDto
    {
        public string? TenantId { get; set; }
        public string? Domain { get; set; }          // optioneel
        public string Entity { get; set; } = "";
        public string Action { get; set; } = "";
        public JsonElement Payload { get; set; } = default;

        // Cruciaal voor loop/duplicate voorkomen bij outbox flush:
        // true = local had dit al opgeslagen (fenced write), cloud hoeft niet te resyncen
        public bool AppliedLocally { get; set; } = false;
    }

    private record AckResultDto(bool Ok, int Status, string? Message);
}
