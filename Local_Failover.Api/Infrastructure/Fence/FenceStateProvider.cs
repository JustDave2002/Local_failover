using Domain.Types;
using Ports;

namespace Infrastructure.Fence;
public sealed class FenceStateProvider : IFenceStateProvider
{
    private volatile FenceMode _mode = FenceMode.Online;
    public FenceMode GetFenceMode(string tenantId)
    {
        // NOTE: tenantId wordt genegeerd (single-tenant PoC)
        // TODO: Bij multi-tenant API: per-tenant state opslaan (cache of DB)
        return _mode;
    }

    public void SetFenceMode(FenceMode mode)
    {
        // TODO: moet aangeroepen vanuit Heartbeat/lease-logica
        _mode = mode;
    }
}
