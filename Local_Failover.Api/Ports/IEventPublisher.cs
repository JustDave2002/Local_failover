namespace Ports;

public interface IEventPublisher
{
    Task PublishAsync(string tenantId, string entity, string @event, object payload, string eventId);
}
