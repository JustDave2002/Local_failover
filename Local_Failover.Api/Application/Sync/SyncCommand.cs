namespace Application.Sync;

public sealed record SyncCommand(
    string TenantId,
    string Entity,
    string Action,
    string PayloadJson,
    Guid? OpId = null
);
