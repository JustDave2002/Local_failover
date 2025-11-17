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
    private readonly IEventPublisher _evt;
    private readonly IConfiguration _cfg;

    public SalesOrdersController(ErpDbContext db, IFenceStateProvider fence, IAppRoleProvider role, ICommandBus bus, IEventPublisher evt, IConfiguration cfg) 
    { _db = db; _fence = fence; _role = role; _bus = bus; _evt = evt; _cfg = cfg; }
    
    [HttpGet] 
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await _db.SalesOrders
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(10)
            .ToListAsync(ct);
        return Ok(items);
    }
    
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSalesOrderRequest req, CancellationToken ct)
    {
        var tenantId = _cfg["Tenant:Id"] ?? "T1";
        var role     = _role.Role;
        var fence    = _fence.GetFenceMode(tenantId);
        var now      = req.CreatedAtUtc ?? DateTime.UtcNow;

        // Standard state - failover disabled
        if (role == AppRole.Disabled)
        {
            var order = new SalesOrder
            {
                Id           = Guid.NewGuid(),
                Customer     = req.Customer,
                Total        = req.Total,
                CreatedAtUtc = now
            };

            _db.SalesOrders.Add(order);
            await _db.SaveChangesAsync(ct);

            return Ok(new CreateSalesOrderResponse(order.Id, order.Customer, order.Total, order.CreatedAtUtc));
        }

        // Cloud state
        if (role == AppRole.Cloud) 
        {
            // Vereist geen fencing regels, wegens domeinpartitionering
            var order = new SalesOrder
            {
                Id           = req.Id ?? Guid.NewGuid(),
                Customer     = req.Customer,
                Total        = req.Total,
                CreatedAtUtc = now
            };

            _db.SalesOrders.Add(order);
            await _db.SaveChangesAsync(ct);
            
            var eid = Guid.NewGuid().ToString();
            
            await _evt.PublishAsync(tenantId, "salesorder", "created", order, eid);

            return Ok(new CreateSalesOrderResponse(order.Id, order.Customer, order.Total, order.CreatedAtUtc));
        }

        // Local state
        if (role == AppRole.Local)
        {
            if (fence == FenceMode.Online)
            {
                var orderId = Guid.NewGuid();

                var env = new CommandEnvelope(
                    TenantId: tenantId,
                    Entity: EntityNames.SalesOrder,
                    Action: "post",
                    Payload: new
                    {
                        Id           = orderId,
                        Customer     = req.Customer,
                        Total        = req.Total,
                        CreatedAtUtc = now
                    },
                    CorrelationId: Guid.NewGuid().ToString()
                );

                var ack = await _bus.SendWithAckAsync(env, TimeSpan.FromSeconds(3), ct);

                if (ack.Ok)
                {    
                    return Ok(new { ok=true, via = "bus", id = orderId, status = ack.Status});
                }

                return StatusCode(ack.Status, new { ok = false, via = "bus", error = ack.Message });
            }

            if (fence == FenceMode.Fenced)
            {
                var fenceError = "backoffice is readonly in fenced mode";
                // backoffice readonly tijdens outage
                return StatusCode(StatusCodes.Status423Locked, new { ok = false, error = fenceError });
            }
        }

        // cover off path
        return StatusCode(StatusCodes.Status500InternalServerError, new { error = "unhandled stockmovement branch" });
    }
    
}
