using Application.Sync;
using Microsoft.AspNetCore.Mvc;

namespace Infrastructure.Messaging.Handlers;

internal static class ControllerResultMapper
{
    public static DispatchResult Map(IActionResult result)
    {
        // Ok(...) of OkObjectResult
        if (result is OkResult) return new DispatchResult(true, 200);
        if (result is OkObjectResult okObj) return new DispatchResult(true, 200, Body: okObj.Value);

        // StatusCode(x, ...)
        if (result is ObjectResult obj)
        {
            var status = obj.StatusCode ?? 200;
            var ok = status is >= 200 and < 300;
            return new DispatchResult(ok, status, Body: obj.Value);
        }

        // fallback
        return new DispatchResult(false, 500, "Unhandled controller result type");
    }
}
