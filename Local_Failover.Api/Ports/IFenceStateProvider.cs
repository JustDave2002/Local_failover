using Domain.Types;

namespace Ports;

public interface IFenceStateProvider
{
    FenceMode GetFenceMode(string tenantId);
    void SetFenceMode(FenceMode mode); // voor toggle endpoint
}