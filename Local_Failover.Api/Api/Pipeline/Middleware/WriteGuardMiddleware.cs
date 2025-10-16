using System.Text.RegularExpressions;
using Domain.Policies;
using Domain.Types;
using Ports;

namespace Api.Pipeline.Middleware;

public sealed class WriteGuardMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IDomainPolicy _policy;
    private readonly IFenceStateProvider _fence;
    private readonly IAppRoleProvider _roleProvider; // haalt AppRole op uit DI
    private static readonly Regex EntityFromPath =
        new("/(backoffice|floorops)/(?<entity>\\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private const string TenantId = "T1"; // PoC simplificatie
    

    public WriteGuardMiddleware(RequestDelegate next, IDomainPolicy policy, IFenceStateProvider fence, IAppRoleProvider roleProvider)
    {
        _next = next; _policy = policy; _fence = fence; _roleProvider = roleProvider;
    }

    public async Task Invoke(HttpContext ctx)
    {
        // bypass admin/status/swagger/health en CORS preflight
        var path = ctx.Request.Path.Value?.ToLowerInvariant() ?? "";
        if (path.StartsWith("/admin/")
        || path.StartsWith("/status")
        || path.StartsWith("/swagger")
        || path.StartsWith("/health")
        || HttpMethods.IsOptions(ctx.Request.Method)) // CORS preflight
        {
            await _next(ctx);
            return;
        }
        // alleen writes guard’en (reads altijd doorlaten)
        var isWrite =
            HttpMethods.IsPost(ctx.Request.Method) ||
            HttpMethods.IsPut(ctx.Request.Method) ||
            HttpMethods.IsPatch(ctx.Request.Method) ||
            HttpMethods.IsDelete(ctx.Request.Method);

        if (!isWrite)
        {
            await _next(ctx);
            return;
        }

        // route parsing: /backoffice/{entity} of /floorops/{entity}
        // TODO: later entity uit route-data halen ipv regex
        var m = EntityFromPath.Match(path);
        var rawEntity = m.Success ? m.Groups["entity"].Value : "";

         // normaliseer (plural→singular)
        if (!EntityNames.TryNormalize(rawEntity, out var entity))
        {
            //TODO: andere status code
            ctx.Response.StatusCode = 423;
            await ctx.Response.WriteAsJsonAsync(new
            {
                error = "readonly",
                reason = $"unknown-entity path={path}"
            });
            return;
        }


        
        

        var fence = _fence.GetFenceMode(TenantId);
        var role = _roleProvider.Role;

        var canWrite = _policy.CanWrite(role, fence, entity);

        if (canWrite)
        {
            await _next(ctx);
            return;
        }

        // blokkeer write bij RO
        ctx.Response.StatusCode = 423; // Locked
        await ctx.Response.WriteAsJsonAsync(new
        {
            error = "readonly",
            reason = $"role={role}, fence={fence}, entity={entity}"
        });
    }
}

public static class MiddlewareExtensions
{
    public static IApplicationBuilder UseWriteGuard(this IApplicationBuilder app)
        => app.UseMiddleware<WriteGuardMiddleware>();
}