using Domain.Types;

namespace Ports;

public interface ISyncGateway
{
    Task<SyncResult> DispatchAsync(SyncRequest req, CancellationToken ct);
    Task<SyncResult> ReceiveAsync(SyncRequest req, CancellationToken ct);
}

public sealed record SyncRequest(
    string TenantId,
    string Domain,     
    string Entity,     
    string Action,     
    object Payload,
    bool AppliedLocally     
);

public sealed record SyncResult(
    bool Ok,
    int Status,
    string Mode,       // "local" | "remote" | "queued"
    string? Message = null,
    object? Data = null
);
