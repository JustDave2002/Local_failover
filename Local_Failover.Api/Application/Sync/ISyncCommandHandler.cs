namespace Application.Sync;

public interface ISyncCommandHandler
{
    bool CanHandle(string entity, string action);
    Task<DispatchResult> HandleAsync(SyncCommand cmd, CancellationToken ct);
}
