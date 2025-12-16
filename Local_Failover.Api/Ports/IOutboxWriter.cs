namespace Ports;

public interface IOutboxWriter
{
    Task EnqueueAsync(OutboxItem item, CancellationToken ct);
}

public sealed record OutboxItem(
    Guid OpId,
    string TenantId,
    string Direction,     // "toCloud" | "toLocal"
    string Entity,
    string Action,
    string PayloadJson,
    DateTime CreatedUtc
);
