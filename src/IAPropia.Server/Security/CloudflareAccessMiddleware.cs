using System.Text.Json;
using IAPropia.Models;
using IAPropia.Services;

namespace IAPropia.Security;

public sealed class CloudflareAccessMiddleware
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly RequestDelegate _next;
    private readonly SeguridadOptions _seg;
    private readonly CloudflareAccessValidator _validator;

    public CloudflareAccessMiddleware(
        RequestDelegate next,
        IaPropiaOptions opts,
        CloudflareAccessValidator validator)
    {
        _next = next;
        _seg = opts.Seguridad;
        _validator = validator;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (path.Equals("/api/health", StringComparison.OrdinalIgnoreCase)
            && context.Request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (!_seg.ValidarCloudflareAccess)
        {
            await _next(context);
            return;
        }

        var (ok, error) = await _validator.ValidarAsync(context, context.RequestAborted);
        if (!ok)
        {
            context.Response.StatusCode = 403;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { ok = false, error }, JsonOpts));
            return;
        }

        await _next(context);
    }
}
