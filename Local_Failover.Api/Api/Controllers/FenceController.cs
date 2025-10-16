using Domain.Types;
using Microsoft.AspNetCore.Mvc;
using Ports;

namespace Api.Controllers;

[ApiController]
[Route("admin/fence")]
public class FenceController : ControllerBase
{
    private readonly IFenceStateProvider _fence;
    public FenceController(IFenceStateProvider fence) { _fence = fence; }

    [HttpPost]
    public IActionResult Set([FromQuery] string mode)
    {
        var parsed = Enum.TryParse<FenceMode>(mode, true, out var m) ? m : FenceMode.Online;
        _fence.SetFenceMode(parsed);
        return Ok(new { setTo = parsed.ToString() });
    }
}
