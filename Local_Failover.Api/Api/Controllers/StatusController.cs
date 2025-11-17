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
    private readonly IConfiguration _cfg;

    public StatusController(IFenceStateProvider fence, IAppRoleProvider roleProvider,  IConfiguration cfg)
    { _fence = fence; _roleProvider = roleProvider; _cfg = cfg; }

    [HttpGet]
    public IActionResult Get()
    {
        var tenantId = _cfg["Tenant:Id"];
        var fence = _fence.GetFenceMode(tenantId);
        return Ok(new { role = _roleProvider.Role.ToString(), fence = fence.ToString(), tenantId = tenantId });
    }
}
// TODO: add counters (pendingOutbox, appliedCount) bij outbox/consumer logic