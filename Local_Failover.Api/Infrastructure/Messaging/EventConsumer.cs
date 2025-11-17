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

public sealed class EventConsumer : BackgroundService
{
    private readonly RabbitConnection _conn;
    private readonly ILogger<EventConsumer> _log;
    private readonly ErpDbContext _db;
    private readonly IConfiguration _cfg;
    private IModel? _ch;
    private readonly string _exchange = "evt";
    private string _queue = "";

    public EventConsumer(RabbitConnection conn, ILogger<EventConsumer> log, ErpDbContext db, IConfiguration cfg)
    { _conn = conn; _log = log; _db = db; _cfg = cfg; }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _ch = _conn.CreateChannel();
        _ch.ExchangeDeclare(_exchange, ExchangeType.Topic, durable: true, autoDelete: false);

        var tenant = _cfg["Tenant:Id"] ?? "T1";
        _queue = $"q.evt.local.{tenant}";
        _ch.QueueDeclare(_queue, durable: true, exclusive: false, autoDelete: false);
        _ch.QueueBind(_queue, _exchange, $"evt.{tenant}.#"); // alles van deze tenant

        var consumer = new AsyncEventingBasicConsumer(_ch);
        consumer.Received += OnReceivedAsync;
        _ch.BasicQos(0, 10, false);
        _ch.BasicConsume(_queue, autoAck: false, consumer: consumer);

        _log.LogInformation("[EVT] Consuming {queue}", _queue);
        return Task.CompletedTask;
    }

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        try
        {
            var rk = ea.RoutingKey; // evt.T1.entity.event
            var parts = rk.Split('.');
            var entity = parts.Length >= 3 ? parts[2] : "";
            var evName = parts.Length >= 4 ? parts[3] : "";

            var json = Encoding.UTF8.GetString(ea.Body.ToArray());
            var env = JsonSerializer.Deserialize<EventEnvelope>(json);
            if (env is null || string.IsNullOrWhiteSpace(env.eventId))
            { _ch!.BasicAck(ea.DeliveryTag, false); return; }

            var eid = Guid.Parse(env.eventId);

            _log.LogInformation("[EVT] Received {Entity}.{Event} {Id}", entity, evName, env.eventId);

            // idempotent check
            if (await _db.AppliedEvents.AnyAsync(x => x.Id == eid))
            { 
                _ch!.BasicAck(ea.DeliveryTag, false); 
                _log.LogDebug("[EVT] Skipped duplicate event {Id}", env.eventId);
                return; }

            // apply
            if (entity == "stockmovement" && evName == "created")
            {
                var sm = JsonSerializer.Deserialize<StockMovement>(env.payload.GetRawText());
                if (sm != null)
                {
                    await UpsertStockMovementAsync(sm);
                }
            }
            else if (entity == "salesorder" && evName == "created")
            {
                var so = JsonSerializer.Deserialize<SalesOrder>(env.payload.GetRawText());
                if (so != null)
                {
                    await UpsertSalesOrderAsync(so);
                }
            }

            _db.AppliedEvents.Add(new AppliedEvent { Id = eid, SeenAtUtc = DateTime.UtcNow });
            await _db.SaveChangesAsync();

            _log.LogInformation("[EVT] Applied {Entity}.{Event} {Id}", entity, evName, env.eventId);

            _ch!.BasicAck(ea.DeliveryTag, false);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[EVT] Error applying event");
            _ch!.BasicNack(ea.DeliveryTag, false, requeue: true);
        }
    }

    private async Task UpsertSalesOrderAsync(SalesOrder so)
    {
        var existing = await _db.SalesOrders.FirstOrDefaultAsync(x => x.Id == so.Id);
        if (existing == null) _db.SalesOrders.Add(so);
        else { existing.Customer = so.Customer; existing.Total = so.Total; existing.CreatedAtUtc = so.CreatedAtUtc; }
    }

    private async Task UpsertStockMovementAsync(StockMovement sm)
    {
        var existing = await _db.StockMovements.FirstOrDefaultAsync(x => x.Id == sm.Id);
        if (existing == null) _db.StockMovements.Add(sm);
        else {_log.LogDebug("[EVT] Skipped duplicate event {Id}", existing.Id); existing.Product = sm.Product; existing.Qty = sm.Qty; existing.Location = sm.Location; existing.CreatedAtUtc = sm.CreatedAtUtc; }
    }

    private sealed class EventEnvelope
    {
        public string eventId { get; set; } = "";
        public JsonElement payload { get; set; } = default;
    }
}
