using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("floorops/stockmovements")]
public class StockMovementController : ControllerBase
{
    [HttpGet] public IActionResult List() => Ok(new[] { new { id = "demo-move-1", product = "Tomatenpuree", qty = -1.0 }});
    [HttpPost] public IActionResult Post([FromBody] object body) => Ok(new { ok = true, source = "fake" });
}
