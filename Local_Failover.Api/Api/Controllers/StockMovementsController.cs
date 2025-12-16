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
    private readonly IConfiguration _cfg;
    private readonly ISyncGateway _gateway;

    public StockMovementController(ErpDbContext db, ILogger<StockMovementController> log, IFenceStateProvider fence, IAppRoleProvider role, ICommandBus bus, IConfiguration cfg, ISyncGateway gateway) 
    { _db = db; _log = log; _fence = fence; _role = role; _bus = bus; _cfg = cfg; _gateway = gateway; }

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
        var now = req.CreatedAtUtc ?? DateTime.UtcNow;

        var payload = new Domain.Entities.StockMovement
        {
            Id = req.Id ?? Guid.NewGuid(),
            Product = req.Product,
            Qty = req.Qty,
            Location = req.Location,
            CreatedAtUtc = now
        };

        var syncReq = new SyncRequest(
            TenantId: tenantId,
            Domain: "floorops",
            Entity: EntityNames.StockMovement,
            Action: "post",
            Payload: payload,
            AppliedLocally: false
        );

        var res = await _gateway.DispatchAsync(syncReq, ct);

        if (!res.Ok) return StatusCode(res.Status, new { ok = false, mode = res.Mode, error = res.Message });

        return Ok(new { ok = true, mode = res.Mode, data = res.Data });
    }
}
