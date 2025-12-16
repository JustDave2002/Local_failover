using System.Text.Json;
using Domain.Types;
using Ports;

namespace Infrastructure.Messaging;

public sealed class SyncGateway : ISyncGateway
{
    private readonly IAppRoleProvider _role;
    private readonly IFenceStateProvider _fence;
    private readonly ICommandBus _bus;
    private readonly IOutboxWriter _outbox;
    private readonly IReadOnlyDictionary<(string entity, string action), ISyncApplyHandler> _handlers;

    public SyncGateway(
        IAppRoleProvider role,
        IFenceStateProvider fence,
        ICommandBus bus,
        IOutboxWriter outbox,
        IEnumerable<ISyncApplyHandler> handlers)
    {
        _role = role;
        _fence = fence;
        _bus = bus;
        _outbox = outbox;

        _handlers = handlers.ToDictionary(
            h =>
            {
                // CanHandle kan meerdere keys toelaten, momenteel in PoC is het 1 key per handler.
                // TODO: een factory/registratie tabel.
                var key = GetSingleKey(h);
                return key;
            },
            h => h
        );
    }

    public async Task<SyncResult> DispatchAsync(SyncRequest req, CancellationToken ct)
    {
        var role = _role.Role;

        if (role == AppRole.Disabled)
        {
            await ApplyAsync(req, ct);
            return new SyncResult(true, 200, "local");
        }

        var fence = _fence.GetFenceMode(req.TenantId);

        // 1) CLOUD: authoritative writes (mits niet fenced op floorops)
        if (role == AppRole.Cloud)
        {
            if (req.Domain == "floorops" && fence == FenceMode.Fenced)
                return new SyncResult(false, 423, "local", "floorops readonly in fenced mode (cloud)");

            await ApplyAsync(req, ct);

            // cloud → local resync (persistent)
            await EnqueueOutboxAsync(req, direction: "toLocal", ct);

            return new SyncResult(true, 200, "local");
        }

        // 2) LOCAL Online: proxy naar cloud (commands)
        if (role == AppRole.Local && fence == FenceMode.Online)
        {
            var cmd = new CommandEnvelope(
                TenantId: req.TenantId,
                Target: "cloud",
                Entity: req.Entity,
                Action: req.Action,
                Payload: req.Payload,
                CorrelationId: Guid.NewGuid().ToString(),
                AppliedLocally: false
            );

            var ack = await _bus.SendWithAckAsync(cmd, TimeSpan.FromSeconds(7), ct);

            if (ack.Ok) return new SyncResult(true, 200, "remote", Data: new { ack.Status });
            return new SyncResult(false, ack.Status, "remote", ack.Message);
        }

        // 3) LOCAL Fenced: floorops write lokaal + enqueue toCloud
        if (role == AppRole.Local && fence == FenceMode.Fenced)
        {
            await ApplyAsync(req, ct);
            await EnqueueOutboxAsync(req with { AppliedLocally = true }, direction: "toCloud", ct);
            return new SyncResult(true, 200, "queued");
        }

        return new SyncResult(false, 500, "local", "unhandled routing branch");
    }

    public async Task<SyncResult> ReceiveAsync(SyncRequest req, CancellationToken ct)
    {
        var role = _role.Role;
        var fence = _fence.GetFenceMode(req.TenantId);

        // geen sync-traffic tijdens fenced (naast heartbeat)
        if (fence == FenceMode.Fenced)
            return new SyncResult(false, 423, "refused", "fenced");

        if (role == AppRole.Cloud)
        {
            await ApplyAsync(req, ct);

            // Als dit NIET vanuit outbox kwam (AppliedLocally=false), dan is het een proxy call → resync terug
            if (!req.AppliedLocally)
                await EnqueueOutboxAsync(req, direction: "toLocal", ct);

            return new SyncResult(true, 200, "applied");
        }

        if (role == AppRole.Local)
        {
            await ApplyAsync(req, ct);
            return new SyncResult(true, 200, "applied");
        }

        return new SyncResult(false, 500, "system", "unhandled routing branch");
    }

    private async Task ApplyAsync(SyncRequest req, CancellationToken ct)
    {
        if (!_handlers.TryGetValue((req.Entity, req.Action), out var handler))
            throw new InvalidOperationException($"No apply handler for {req.Entity}.{req.Action}");

        var res = await handler.ApplyAsync(req, ct);
        if (!res.Ok)
            throw new InvalidOperationException(res.Message ?? $"Apply failed {req.Entity}.{req.Action}");
    }

    private async Task EnqueueOutboxAsync(SyncRequest req, string direction, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(req.Payload);
        await _outbox.EnqueueAsync(new OutboxItem(
            OpId: Guid.NewGuid(),
            TenantId: req.TenantId,
            Direction: direction,
            Entity: req.Entity,
            Action: req.Action,
            PayloadJson: json,
            CreatedUtc: DateTime.UtcNow
        ), ct);
    }

    private static (string entity, string action) GetSingleKey(ISyncApplyHandler h)
    {
        // PoC: keys zijn bekend, dus hardcode mapping per handler type.
        // Simpeler (en netter): voeg Entity/Action properties toe aan interface,
        var t = h.GetType().Name.ToLowerInvariant();

        if (t.Contains("salesorder") && t.Contains("post"))
            return (EntityNames.SalesOrder, "post");

        if (t.Contains("stockmovement") && t.Contains("post"))
            return (EntityNames.StockMovement, "post");

        throw new InvalidOperationException($"Handler key mapping missing for {h.GetType().Name}");
    }
}
