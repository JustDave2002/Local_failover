using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Ports;

namespace Infrastructure.Data;

public sealed class DbOutboxWriter : IOutboxWriter
{
    private readonly ErpDbContext _db;
    public DbOutboxWriter(ErpDbContext db) => _db = db;

    public async Task EnqueueAsync(OutboxItem item, CancellationToken ct)
    {
        _db.Outbox.Add(new OutboxMessage
        {
            Id = item.OpId,
            TenantId = item.TenantId,
            Direction = item.Direction,
            Entity = item.Entity,
            Action = item.Action,
            PayloadJson = item.PayloadJson,
            CreatedUtc = item.CreatedUtc
        });

        await _db.SaveChangesAsync(ct);
    }
}
