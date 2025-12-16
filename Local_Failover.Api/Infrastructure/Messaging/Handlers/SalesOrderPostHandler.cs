using System.Text.Json;
using Api;
using Domain.Entities;
using Domain.Types;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Ports;

namespace Infrastructure.Messaging.Handlers;

public sealed class SalesOrderPostHandler : ISyncApplyHandler
{
    private readonly ErpDbContext _db;
    public SalesOrderPostHandler(ErpDbContext db) => _db = db;

    public bool CanHandle(string entity, string action)
        => entity == EntityNames.SalesOrder && action == "post";

    public async Task<SyncApplyResult> ApplyAsync(SyncRequest req, CancellationToken ct)
    {
    
        var so = Coerce<SalesOrder>(req.Payload);
        if (so is null) return new(false, 400, "bad payload");

        // idempotency
        var exists = await _db.SalesOrders.AnyAsync(x => x.Id == so.Id, ct);
        if (!exists) _db.SalesOrders.Add(so);

        await _db.SaveChangesAsync(ct);
        return new(true, 200, Data: new { so.Id });
    }

    private static T? Coerce<T>(object payload)
    {
        if (payload is T t) return t;

        if (payload is JsonElement je)
            return JsonSerializer.Deserialize<T>(je.GetRawText());

        // fallback
        return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(payload));
    }
}
