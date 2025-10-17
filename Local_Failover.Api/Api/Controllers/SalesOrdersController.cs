using Api;
using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Domain.Entities;


namespace Api.Controllers;

[ApiController]
[Route("backoffice/salesorders")]
public class SalesOrdersController : ControllerBase
{
    private readonly ErpDbContext _db;
    public SalesOrdersController(ErpDbContext db) {_db = db; }
    
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
        var now = DateTime.UtcNow;
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
