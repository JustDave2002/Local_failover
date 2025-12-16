namespace Application.Sync;

public interface ICommandDispatcher
{
    Task<DispatchResult> DispatchAsync(SyncCommand cmd, CancellationToken ct);
}
