using System.Text.Json;
using Domain.Entities;
using Domain.Types;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Ports;

namespace Infrastructure.Messaging.Handlers;

public sealed class StockMovementPostHandler : ISyncApplyHandler
{
    private readonly ErpDbContext _db;
    public StockMovementPostHandler(ErpDbContext db) => _db = db;

    public bool CanHandle(string entity, string action)
        => entity == EntityNames.StockMovement && action == "post";

    public async Task<SyncApplyResult> ApplyAsync(SyncRequest req, CancellationToken ct)
    {
        var sm = Coerce<StockMovement>(req.Payload);
        if (sm is null) return new(false, 400, "bad payload");

        // idempotency
        var exists = await _db.StockMovements.AnyAsync(x => x.Id == sm.Id, ct);
        if (!exists) _db.StockMovements.Add(sm);

        await _db.SaveChangesAsync(ct);
        return new(true, 200, Data: new { sm.Id });
    }

    private static T? Coerce<T>(object payload)
    {
        if (payload is T t) return t;

        if (payload is JsonElement je)
            return JsonSerializer.Deserialize<T>(je.GetRawText());

        return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(payload));
    }
}
