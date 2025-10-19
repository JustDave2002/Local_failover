using Api;
using Domain.Types;
using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Domain.Entities;
using Ports;

namespace Api.Controllers;

[ApiController]
[Route("floorops/stockmovements")]
public class StockMovementController : ControllerBase
{
    private readonly ErpDbContext _db;
    private readonly IFenceStateProvider _fence;
    private readonly IAppRoleProvider _role;
    private readonly ICommandBus _bus;
    public StockMovementController(ErpDbContext db, IFenceStateProvider fence, IAppRoleProvider role, ICommandBus bus) 
    { _db = db; _fence = fence; _role = role; _bus = bus; }

    [HttpGet] 
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await _db.StockMovements
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(50)
            .ToListAsync(ct);
        return Ok(items);
    }

    [HttpPost] 
    public async Task<IActionResult> Post([FromBody] PostStockMovementRequest req, CancellationToken ct)
    {
        var fence = _fence.GetFenceMode("T1");
        var role  = _role.Role;

        if (role == AppRole.Local && fence == FenceMode.Online)
        {
            // via Rabbit command → Cloud
            var env = new CommandEnvelope(
                TenantId: "T1",
                Entity: EntityNames.StockMovement,
                Action: "post",
                Payload: new {
                    Id = Guid.NewGuid(),
                    Product = req.Product,
                    Qty = req.Qty,
                    Location = req.Location,
                    CreatedAtUtc = DateTime.UtcNow
                },
                CorrelationId: Guid.NewGuid().ToString()
            );

            var ack = await _bus.SendWithAckAsync(env, TimeSpan.FromSeconds(3), ct);
            if (ack.Ok) return Ok(new { ok = true, via = "bus", status = ack.Status });
            return StatusCode(ack.Status, new { ok = false, via = "bus", error = ack.Message });
        }

        //local writing mode
        var now = DateTime.UtcNow;
        var sm = new StockMovement
        {
            Id = Guid.NewGuid(),
            Product = req.Product,
            Qty = req.Qty,
            Location = req.Location,
            CreatedAtUtc = now
        };
        _db.StockMovements.Add(sm);
        await _db.SaveChangesAsync(ct);

        return Ok(new PostStockMovementResponse(sm.Id, sm.Product, sm.Qty, sm.Location, sm.CreatedAtUtc));
    }
}
