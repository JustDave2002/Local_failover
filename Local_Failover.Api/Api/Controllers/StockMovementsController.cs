using Api;
using Domain.Types;
using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Domain.Entities;
using Microsoft.Extensions.Logging;
using Ports;
using System.Text.Json;

namespace Api.Controllers;

[ApiController]
[Route("floorops/stockmovements")]
public class StockMovementController : ControllerBase
{
    private readonly ErpDbContext _db;
    private readonly IFenceStateProvider _fence;
    private readonly ILogger<StockMovementController> _log;
    private readonly IAppRoleProvider _role;
    private readonly ICommandBus _bus;
    private readonly IEventPublisher _evt;
    private readonly IConfiguration _cfg;

    public StockMovementController(ErpDbContext db, ILogger<StockMovementController> log, IFenceStateProvider fence, IAppRoleProvider role, ICommandBus bus, IEventPublisher evt, IConfiguration cfg) 
    { _db = db; _log = log; _fence = fence; _role = role; _bus = bus; _evt = evt; _cfg = cfg; }

    [HttpGet] 
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await _db.StockMovements
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(10)
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost] 
    public async Task<IActionResult> Post([FromBody] PostStockMovementRequest req, CancellationToken ct)
    {   
        var tenantId = _cfg["Tenant:Id"] ?? "T1";
        var fence = _fence.GetFenceMode(tenantId);
        var role  = _role.Role;
        var now = req.CreatedAtUtc ?? DateTime.UtcNow;

        // Standard state - failover disabled
        if (role == AppRole.Disabled)
        {
            var move = new StockMovement
            {
                Id           = Guid.NewGuid(),
                Product      = req.Product,
                Qty          = req.Qty,
                Location     = req.Location,
                CreatedAtUtc = now
            };

            _db.StockMovements.Add(move);
            await _db.SaveChangesAsync(ct);

            return Ok(new PostStockMovementResponse(move.Id, move.Product, move.Qty, move.Location, move.CreatedAtUtc));
        }

        // Cloud state
        if (role == AppRole.Cloud)
        {
            var id = req.Id ?? Guid.NewGuid();

            if (fence == FenceMode.Fenced) 
            {
                // floorops RO in fenced mode
                return StatusCode(StatusCodes.Status423Locked, new { ok = false, error = "floorops readonly in fenced mode (cloud)" });
            }

            // Idempotency: als record al bestaat, gewoon ok teruggeven
            var existing = await _db.StockMovements.FindAsync(new object[] { id }, ct);
            if (existing is not null)
            {
                _log.LogInformation("[CLOUD] stockmovement {Id} already exists, treating as idempotent success", id);
                return Ok(new PostStockMovementResponse(existing.Id, existing.Product, existing.Qty, existing.Location, existing.CreatedAtUtc));
            }

            // Nieuwe insert
            var move = new StockMovement
            {
                Id           = id,
                Product      = req.Product,
                Qty          = req.Qty,
                Location     = req.Location,
                CreatedAtUtc = now
            };


            _db.StockMovements.Add(move);
            await _db.SaveChangesAsync(ct);

            var eid = Guid.NewGuid().ToString();
            
            await _evt.PublishAsync(tenantId, "stockmovement", "created", move, eid);

            return Ok(new PostStockMovementResponse(move.Id, move.Product, move.Qty, move.Location, move.CreatedAtUtc));
        }

        // Local state
        if (role == AppRole.Local)
        {
            if (fence == FenceMode.Online)
            {
                // via Rabbit command → Cloud
                var moveId = Guid.NewGuid();

                var env = new CommandEnvelope(
                    TenantId: tenantId,
                    Entity: EntityNames.StockMovement,
                    Action: "post",
                    Payload: new
                    {
                        Id           = moveId,
                        Product      = req.Product,
                        Qty          = req.Qty,
                        Location     = req.Location,
                        CreatedAtUtc = now
                    },
                    CorrelationId: Guid.NewGuid().ToString()
                );

                var ack = await _bus.SendWithAckAsync(env, TimeSpan.FromSeconds(3), ct);

                if (ack.Ok) return Ok(new { ok = true, via = "bus", id = moveId, status = ack.Status });

                return StatusCode(ack.Status, new { ok = false, via = "bus", error = ack.Message });

            }

            if (fence == FenceMode.Fenced)
            {
                // lokale write + outbox voor later flush
                var moveId = Guid.NewGuid();

                var move = new StockMovement
                {
                    Id           = moveId,
                    Product      = req.Product,
                    Qty          = req.Qty,
                    Location     = req.Location,
                    CreatedAtUtc = now
                };

                _db.StockMovements.Add(move);

                var outbox = new OutboxMessage
                {
                    Id          = Guid.NewGuid(),
                    TenantId    = tenantId,
                    Entity      = EntityNames.StockMovement,
                    Action      = "post",
                    PayloadJson = JsonSerializer.Serialize(move),
                    CreatedUtc  = now
                };
                _db.Outbox.Add(outbox);

                await _db.SaveChangesAsync(ct);

                return Ok(new { ok = true, queued = true, id = moveId });
            }
        }

        // zou niet moeten gebeuren
        return StatusCode(StatusCodes.Status500InternalServerError, new { error = "unhandled stockmovement branch" });
    }
}
