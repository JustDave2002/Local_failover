using Domain.Types;
using Ports;

namespace Infrastructure.Fence;
public sealed class FenceStateProvider : IFenceStateProvider
{
    private readonly ILogger<FenceStateProvider> _log;
    private FenceMode _state = FenceMode.Online;

    public FenceStateProvider(ILogger<FenceStateProvider> log)
    {
        _log = log;
    }

    public FenceMode GetFenceMode(string tenantId)
    {
        // NOTE: tenantId wordt genegeerd (single-tenant PoC)
        // TODO: Bij multi-tenant API: per-tenant state opslaan (cache of DB)
        return _state;
    }

    public void SetFenceMode(FenceMode newState)
    {
        // TODO: moet aangeroepen vanuit Heartbeat/lease-logica
        _state = newState;
        _log.LogWarning("[FENCE] Mode changed → {Mode}", newState);
    }
}
