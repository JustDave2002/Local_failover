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
    private readonly IConfiguration _cfg;
    private readonly ISyncGateway _gateway;

    public SalesOrdersController(ErpDbContext db, IFenceStateProvider fence, IAppRoleProvider role, ICommandBus bus, IConfiguration cfg, ISyncGateway gateway) 
    { _db = db; _fence = fence; _role = role; _bus = bus; _cfg = cfg; _gateway = gateway; }
    
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
        var now = req.CreatedAtUtc ?? DateTime.UtcNow;

        var payload = new Domain.Entities.SalesOrder
        {
            Id = req.Id ?? Guid.NewGuid(),
            Customer = req.Customer,
            Total = req.Total,
            CreatedAtUtc = now
        };

        var syncReq = new SyncRequest(
            TenantId: tenantId,
            Domain: "backoffice",
            Entity: EntityNames.SalesOrder,
            Action: "post",
            Payload: payload,
            AppliedLocally: false // controller-call, nog niet applied
        );

        var res = await _gateway.DispatchAsync(syncReq, ct);

        if (!res.Ok) return StatusCode(res.Status, new { ok = false, mode = res.Mode, error = res.Message });

        return Ok(new { ok = true, mode = res.Mode, data = res.Data });
    }    
}
