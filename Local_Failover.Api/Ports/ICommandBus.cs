namespace Ports;

public interface ICommandBus
{
    Task<AckResult> SendWithAckAsync(CommandEnvelope cmd, TimeSpan timeout, CancellationToken ct);
}

public record CommandEnvelope(
    string TenantId,
    string Target,   // "cloud" | "local"
    string Entity,
    string Action,
    object Payload,
    string CorrelationId,
    bool AppliedLocally
);

public record AckResult(bool Ok, int Status, string? Message);
