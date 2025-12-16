using System.Threading;
using System.Threading.Tasks;

namespace Ports;

public interface ISyncApplyHandler
{
    bool CanHandle(string entity, string action);
    Task<SyncApplyResult> ApplyAsync(SyncRequest req, CancellationToken ct);
}

public sealed record SyncApplyResult(
    bool Ok,
    int Status,
    string? Message = null,
    object? Data = null
);
