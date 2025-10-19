using Api;
using Domain.Types;
using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Domain.Entities;
using Ports;


namespace Api.Controllers;

[ApiController]
[Route("backoffice/salesorders")]
public class SalesOrdersController : ControllerBase
{
    private readonly ErpDbContext _db;
    private readonly IFenceStateProvider _fence;
    private readonly IAppRoleProvider _role;
    private readonly ICommandBus _bus;
    public SalesOrdersController(ErpDbContext db, IFenceStateProvider fence, IAppRoleProvider role, ICommandBus bus) 
    { _db = db; _fence = fence; _role = role; _bus = bus; }
    
    [HttpGet] 
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await _db.SalesOrders
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(50)
            .ToListAsync(ct);
        return Ok(items);
    }
    
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSalesOrderRequest req, CancellationToken ct)
    {
        var fence = _fence.GetFenceMode("T1");
        var role  = _role.Role;
        var now = DateTime.UtcNow;

        if (role == AppRole.Local && fence == FenceMode.Online)
        {
            // via Rabbit command → Cloud
            var env = new CommandEnvelope(
                TenantId: "T1",
                Entity: EntityNames.SalesOrder,
                Action: "post",
                Payload: new {
                    Id = Guid.NewGuid(),
                    Customer = req.Customer,
                    Total = req.Total,
                    CreatedAtUtc = now
                },
                CorrelationId: Guid.NewGuid().ToString()
            );

            var ack = await _bus.SendWithAckAsync(env, TimeSpan.FromSeconds(3), ct);
            if (ack.Ok) return Ok(new { ok = true, via = "bus", status = ack.Status });
            return StatusCode(ack.Status, new { ok = false, via = "bus", error = ack.Message });
        }

        //local writing mode
        //TODO: Rename to order
        var so = new SalesOrder
        {
            Id = Guid.NewGuid(),
            Customer = req.Customer,
            Total = req.Total,
            CreatedAtUtc = now
        };
        _db.SalesOrders.Add(so);
        await _db.SaveChangesAsync(ct);

        return Ok(new CreateSalesOrderResponse(so.Id, so.Customer, so.Total, so.CreatedAtUtc));
    }
    
}
