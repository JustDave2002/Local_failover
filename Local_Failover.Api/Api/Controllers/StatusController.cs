using Domain.Types;
using Microsoft.AspNetCore.Mvc;
using Ports;

namespace Api.Controllers;

[ApiController]
[Route("status")]
public class StatusController : ControllerBase
{
    private readonly IFenceStateProvider _fence;
    private readonly IAppRoleProvider _roleProvider;

    public StatusController(IFenceStateProvider fence, IAppRoleProvider roleProvider)
    { _fence = fence; _roleProvider = roleProvider; }

    [HttpGet]
    public IActionResult Get()
    {
        var fence = _fence.GetFenceMode("T1");
        return Ok(new { role = _roleProvider.Role.ToString(), fence = fence.ToString(), tenantId = "T1" });
    }
}
// TODO: add counters (pendingOutbox, appliedCount) bij outbox/consumer logic