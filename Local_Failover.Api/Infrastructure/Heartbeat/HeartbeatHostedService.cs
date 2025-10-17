using Domain.Types;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ports;

namespace Infrastructure.Heartbeat;
public sealed class HeartbeatHostedService : BackgroundService
{
  private readonly IHeartbeatProbe _probe;
  private readonly IFenceStateProvider _fence;
  private readonly ILogger<HeartbeatHostedService> _log;
  private readonly IConfiguration _cfg;

  public HeartbeatHostedService(IHeartbeatProbe probe, IFenceStateProvider fence,
                                ILogger<HeartbeatHostedService> log, IConfiguration cfg)
  { _probe = probe; _fence = fence; _log = log; _cfg = cfg; }

  protected override async Task ExecuteAsync(CancellationToken ct)
  {
    var intervalMs = int.TryParse(_cfg["Heartbeat:IntervalMs"], out var i) ? i : 3000;
    var maxFails   = int.TryParse(_cfg["Heartbeat:MaxFails"],   out var f) ? f : 2;
    var fails = 0;

    while (!ct.IsCancellationRequested)
    {
      var ok = await _probe.CheckAsync(ct);
      if (ok) { fails = 0; _fence.SetFenceMode(FenceMode.Online); }
      else if (++fails >= maxFails) { _fence.SetFenceMode(FenceMode.Fenced); }

      await Task.Delay(intervalMs, ct);
    }
  }
}
