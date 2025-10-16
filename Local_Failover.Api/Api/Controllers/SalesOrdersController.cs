using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("backoffice/salesorders")]
public class SalesOrdersController : ControllerBase
{
    [HttpGet] public IActionResult List() => Ok(new[] { new { id = "demo-order-1", customer = "ACME", total = 100.0 }});
    [HttpPost] public IActionResult Create([FromBody] object body) => Ok(new { ok = true, source = "fake" });
}
