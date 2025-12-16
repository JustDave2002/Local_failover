using System.Text.Json;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ports;

namespace Infrastructure.Messaging;

public sealed class OutboxPublisherHostedService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<OutboxPublisherHostedService> _log;
    private readonly IFenceStateProvider _fence;
    private readonly ICommandBus _bus;
    private readonly IConfiguration _cfg;

    public OutboxPublisherHostedService(
        IServiceProvider sp,
        ILogger<OutboxPublisherHostedService> log,
        IFenceStateProvider fence,
        ICommandBus bus,
        IConfiguration cfg)
    {
        _sp = sp; _log = log; _fence = fence; _bus = bus; _cfg = cfg;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var tenantId = _cfg["Tenant:Id"] ?? "T1";

                // flush only when Online
                if (_fence.GetFenceMode(tenantId) == Domain.Types.FenceMode.Online)
                {
                    using var scope = _sp.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

                    var batch = await db.Outbox
                        .Where(x => x.AckedUtc == null)
                        .OrderBy(x => x.CreatedUtc)
                        .Take(50)
                        .ToListAsync(stoppingToken);

                    foreach (var m in batch)
                    {
                        var target = (m.Direction ?? "toCloud").Equals("toLocal", StringComparison.OrdinalIgnoreCase)
                            ? "local"
                            : "cloud";

                        _log.LogInformation("[OUTBOX] Sending {Dir} {Entity} {Action} {Id}", m.Direction, m.Entity, m.Action, m.Id);

                        var env = new CommandEnvelope(
                            TenantId: m.TenantId,
                            Target: target,
                            Entity: m.Entity,
                            Action: m.Action,
                            Payload: JsonSerializer.Deserialize<object>(m.PayloadJson)!,
                            CorrelationId: Guid.NewGuid().ToString(),
                            AppliedLocally: true
                        );

                        var ack = await _bus.SendWithAckAsync(env, TimeSpan.FromSeconds(7), stoppingToken);

                        if (ack.Ok)
                        {
                            m.SentUtc ??= DateTime.UtcNow;
                            m.AckedUtc = DateTime.UtcNow;
                            await db.SaveChangesAsync(stoppingToken);

                            _log.LogInformation("[OUTBOX] ACK {Entity} {Id}", m.Entity, m.Id);
                        }
                        else
                        {
                            _log.LogWarning("[OUTBOX] send failed {Status} {Msg}", ack.Status, ack.Message);
                            break; // retry loop will handle
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Outbox flush error");
            }

            await Task.Delay(2000, stoppingToken);
        }
    }
}
