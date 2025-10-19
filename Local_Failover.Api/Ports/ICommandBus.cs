namespace Ports;

public interface ICommandBus
{
    Task<AckResult> SendWithAckAsync(CommandEnvelope cmd, TimeSpan timeout, CancellationToken ct);
}

public record CommandEnvelope(
    string TenantId,
    string Entity,
    string Action,
    object Payload,
    string CorrelationId
);

public record AckResult(bool Ok, int Status, string? Message);
