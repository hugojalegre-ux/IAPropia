using System.Text.Json;
using IAPropia.Models;
using IAPropia.Services;

namespace IAPropia.Security;

public sealed class ApiKeyMiddleware
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly RequestDelegate _next;
    private readonly SeguridadOptions _seg;
    private readonly IaFileLog? _log;

    public ApiKeyMiddleware(RequestDelegate next, IaPropiaOptions opts, IaFileLog log)
    {
        _next = next;
        _seg = opts.Seguridad;
        _log = log;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (IsRutaPublica(path, context.Request.Method))
        {
            await _next(context);
            return;
        }

        if (!_seg.RequiereApiKey)
        {
            await _next(context);
            return;
        }

        var expected = _seg.ApiKey?.Trim();
        if (string.IsNullOrEmpty(expected))
        {
            await EscribirJsonAsync(context, 503, new { ok = false, error = "RequiereApiKey activo pero falta ApiKey en appsettings.secrets.json." });
            return;
        }

        var header = _seg.HeaderApiKey?.Trim();
        if (string.IsNullOrEmpty(header))
            header = "X-IAPropia-Key";

        context.Request.Headers.TryGetValue(header, out var provided);
        if (!ApiKeyHelper.Coincide(expected, provided.ToString()))
        {
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "?";
            _log?.LogStartup($"auth api-key rechazada ip={TruncarIp(ip)} path={path}");
            await EscribirJsonAsync(context, 401, new
            {
                ok = false,
                error = $"Falta o es inválida la API key (header {header}). Ver docs/SEGURIDAD.md."
            });
            return;
        }

        await _next(context);
    }

    private static bool IsRutaPublica(string path, string method) =>
        method.Equals("GET", StringComparison.OrdinalIgnoreCase)
        && path.Equals("/api/health", StringComparison.OrdinalIgnoreCase);

    private static string TruncarIp(string ip) =>
        ip.Length <= 20 ? ip : ip[..8] + "…";

    private static async Task EscribirJsonAsync(HttpContext ctx, int code, object body)
    {
        ctx.Response.StatusCode = code;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOpts));
    }
}
