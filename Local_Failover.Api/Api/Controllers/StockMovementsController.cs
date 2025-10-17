using Api;
using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Domain.Entities;

namespace Api.Controllers;

[ApiController]
[Route("floorops/stockmovements")]
public class StockMovementController : ControllerBase
{
    private readonly ErpDbContext _db;
    public StockMovementController(ErpDbContext db) { _db = db; }

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
