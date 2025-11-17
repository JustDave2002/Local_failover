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

  public HeartbeatHostedService(IHeartbeatProbe probe, IFenceStateProvider fence, ILogger<HeartbeatHostedService> log, IConfiguration cfg)
  { _probe = probe; _fence = fence; _log = log; _cfg = cfg; }

  protected override async Task ExecuteAsync(CancellationToken ct)
  {
    var intervalMs = int.TryParse(_cfg["Heartbeat:IntervalMs"], out var i) ? i : 3000;
    var maxFails   = int.TryParse(_cfg["Heartbeat:MaxFails"],   out var f) ? f : 2;
    var tenantId = _cfg["Tenant:Id"];
    var fails = 0;

    while (!ct.IsCancellationRequested)
    {
      var ok = await _probe.CheckAsync(ct);
      var currentMode = _fence.GetFenceMode("1");

      if (ok)
      {
          if (currentMode != FenceMode.Online)
          {
              _fence.SetFenceMode(FenceMode.Online);
              // TODO: refactor local state into a lease
              fails = 0;
          }
          else
          {
              // geen verandering nodig — niks doen
              fails = 0;
          }
      }
      else
      {
          // Verbinding gefaald
          fails++;

          if (fails >= maxFails && currentMode != FenceMode.Fenced)
          {
              _fence.SetFenceMode(FenceMode.Fenced);
          }
      }

      await Task.Delay(intervalMs, ct);
    }
  }
}
