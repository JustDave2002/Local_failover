using Domain.Types;
using Ports;

namespace Application.Sync;

public sealed class CommandDispatcher : ICommandDispatcher
{
    private readonly IAppRoleProvider _role;
    private readonly IFenceStateProvider _fence;
    private readonly IEnumerable<ISyncCommandHandler> _handlers;

    public CommandDispatcher(
        IAppRoleProvider role,
        IFenceStateProvider fence,
        IEnumerable<ISyncCommandHandler> handlers)
    {
        _role = role;
        _fence = fence;
        _handlers = handlers;
    }

    public async Task<DispatchResult> DispatchAsync(SyncCommand cmd, CancellationToken ct)
    {
        // Stap 2: alleen “local invoke” (nog geen bus/outbox)
        // We willen nu eerst de handler-pipeline neerzetten.

        var handler = _handlers.FirstOrDefault(h => h.CanHandle(cmd.Entity, cmd.Action));
        if (handler is null)
            return new DispatchResult(false, 400, $"No handler for {cmd.Entity}.{cmd.Action}");

        return await handler.HandleAsync(cmd, ct);
    }
}
