namespace Ports;
public interface IHeartbeatProbe
{
    Task<bool> CheckAsync(CancellationToken ct); // true = remote reachable/healthy
}
