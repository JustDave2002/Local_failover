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

    public OutboxPublisherHostedService(IServiceProvider sp, ILogger<OutboxPublisherHostedService> log, IFenceStateProvider fence, ICommandBus bus)
    { _sp = sp; _log = log; _fence = fence; _bus = bus; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // alleen proberen te flushen als we Online zijn
                //TODO: change tenant to dynamic loading
                if (_fence.GetFenceMode("T1") == Domain.Types.FenceMode.Online)
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
                        _log.LogInformation("[OUTBOX] Sending {Entity} {Action} {Id}", m.Entity, m.Action, m.Id);
                        var env = new CommandEnvelope(
                            m.TenantId, m.Entity, m.Action,
                            JsonSerializer.Deserialize<object>(m.PayloadJson)!,
                            Guid.NewGuid().ToString()
                        );

                        var ack = await _bus.SendWithAckAsync(env, TimeSpan.FromSeconds(7), stoppingToken);
                        if (ack.Ok)
                        {
                            _log.LogInformation("[OUTBOX] ACK {Entity} {Id} @ {Time}", m.Entity, m.Id, m.AckedUtc);
                            m.SentUtc = m.SentUtc ?? DateTime.UtcNow;
                            m.AckedUtc = DateTime.UtcNow;
                            await db.SaveChangesAsync(stoppingToken);
                        }
                        else
                        {
                            _log.LogWarning("Outbox send failed {Status} {Msg}", ack.Status, ack.Message);
                            break; // laat retry loop z’n werk doen
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
