using System.Threading.RateLimiting;
using IAPropia.Models;
using IAPropia.Security;
using IAPropia.Services;
using Microsoft.AspNetCore.RateLimiting;

var contentRoot = AppContext.BaseDirectory;
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = contentRoot
});

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.Lan.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.Tunel.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.secrets.json", optional: true, reloadOnChange: true);

var iaSection = builder.Configuration.GetSection(IaPropiaOptions.Section);
builder.Services.Configure<IaPropiaOptions>(iaSection);
var iaOpts = iaSection.Get<IaPropiaOptions>() ?? new IaPropiaOptions();

builder.Services.AddSingleton(iaOpts);
builder.Services.AddSingleton<CarpetaStore>();
builder.Services.AddSingleton<PersonaStore>();
builder.Services.AddSingleton<ConversationStore>();
builder.Services.AddSingleton<AprendizajeStore>();
builder.Services.AddSingleton<WebSearchService>();
builder.Services.AddSingleton<KnowledgeRag>();
builder.Services.AddSingleton<LlmOrchestrator>();
builder.Services.AddSingleton<OllamaHealthService>();
builder.Services.AddSingleton<VersionInfo>();
builder.Services.AddSingleton<IaFileLog>();
builder.Services.AddSingleton<CloudflareAccessValidator>();

var seg = iaOpts.Seguridad;
var maxBodyBytes = Math.Clamp(seg.MaxBodyKb, 64, 8192) * 1024L;
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = maxBodyBytes);

var rateChat = Math.Clamp(seg.RateLimitChatPorMinuto, 1, 120);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (ctx, _) =>
    {
        ctx.HttpContext.Response.ContentType = "application/json; charset=utf-8";
        await ctx.HttpContext.Response.WriteAsync(
            """{"ok":false,"error":"Demasiadas solicitudes de chat. Esperá un momento."}""");
    };
    options.AddPolicy("chat", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateChat,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var timeoutMax = Math.Clamp(iaOpts.Limites.TimeoutMaximo, 60, 3600);
var timeoutWeb = Math.Clamp(iaOpts.Limites.TimeoutBusquedaWebSegundos, 10, 120);

builder.Services.AddHttpClient("ollama", c =>
{
    c.Timeout = TimeSpan.FromSeconds(timeoutMax);
});

builder.Services.AddHttpClient("llm-web", c =>
{
    c.Timeout = TimeSpan.FromSeconds(timeoutWeb);
    c.MaxResponseContentBufferSize = 512 * 1024;
    c.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
        "Siltex/1.0 (asistente local; +https://github.com/hugojalegre-ux/IAPropia)");
    c.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "es-AR,es;q=0.9,en;q=0.5");
});

var timeoutNormal = Math.Clamp(iaOpts.Limites.TimeoutSegundos, 30, timeoutMax);
builder.Services.AddHttpClient("llm", c =>
{
    c.Timeout = TimeSpan.FromSeconds(timeoutNormal);
});

builder.Services.AddRequestTimeouts();

builder.Services.AddCors(o =>
{
    if (seg.RequiereApiKey)
    {
        o.AddDefaultPolicy(p => p
            .SetIsOriginAllowed(origin =>
            {
                if (string.IsNullOrEmpty(origin)) return true;
                return Uri.TryCreate(origin, UriKind.Absolute, out var u)
                       && (u.Scheme is "http" or "https")
                       && (u.Host is "127.0.0.1" or "localhost");
            })
            .AllowAnyHeader()
            .AllowAnyMethod());
    }
    else
    {
        o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
    }
});

var port = iaOpts.PuertoPreferido > 0 ? iaOpts.PuertoPreferido : 5077;
if (seg.EnlaceSoloLocalhost)
{
    var envUrls = builder.Configuration["ASPNETCORE_URLS"];
    if (!string.IsNullOrWhiteSpace(envUrls)
        && !envUrls.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase)
        && !envUrls.Contains("localhost", StringComparison.OrdinalIgnoreCase))
    {
        builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Information);
    }

    builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
}

var app = builder.Build();

var fileLog = app.Services.GetRequiredService<IaFileLog>();
fileLog.LogStartup(
    $"IA propia arrancó · proveedor={iaOpts.ProveedorPorDefecto} · apiKey={seg.RequiereApiKey} · cfAccess={seg.ValidarCloudflareAccess}");

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseCors();
app.UseRateLimiter();
app.UseMiddleware<ApiKeyMiddleware>();
app.UseMiddleware<CloudflareAccessMiddleware>();
app.UseRequestTimeouts();
app.UseDefaultFiles();
app.UseStaticFiles();

var raiz = AppPaths.ResolverRaizProyecto(app.Environment);
var dataDir = AppPaths.CarpetaDatos(raiz);
var conocimientoDir = AppPaths.CarpetaConocimiento(raiz, iaOpts.Rag.CarpetaConocimiento);
var versionInfo = app.Services.GetRequiredService<VersionInfo>();

app.MapGet("/api/health", () => Results.Ok(new HealthDto
{
    Ok = true,
    Version = versionInfo.Version,
    Mensaje = "Servidor IA propia en ejecución.",
    RequiereApiKey = seg.RequiereApiKey,
    HeaderApiKey = string.IsNullOrWhiteSpace(seg.HeaderApiKey) ? "X-IAPropia-Key" : seg.HeaderApiKey.Trim(),
    ValidarCloudflareAccess = seg.ValidarCloudflareAccess
}));

app.MapGet("/api/health/ollama", async (OllamaHealthService health, CancellationToken ct) =>
{
    var dto = await health.ComprobarAsync(ct);
    return Results.Ok(dto);
});

app.MapGet("/api/estado", async (PersonaStore personas, CarpetaStore carpetas, OllamaHealthService health, CancellationToken ct) =>
{
    var ollama = await health.ComprobarAsync(ct);
    return Results.Ok(new EstadoServicioDto
    {
        Nombre = iaOpts.Titulo,
        Version = versionInfo.Version,
        ProveedorPorDefecto = iaOpts.ModoSoloLocal ? "ollama" : iaOpts.ProveedorPorDefecto,
        ModoSoloLocal = iaOpts.ModoSoloLocal,
        InternetBusquedaHabilitada = iaOpts.InternetBusqueda.Habilitada,
        OllamaConfigurado = !string.IsNullOrWhiteSpace(iaOpts.Ollama.BaseUrl),
        CarpetaDatos = seg.ExponerRutasEnEstado ? dataDir : "",
        CarpetaConocimiento = seg.ExponerRutasEnEstado ? conocimientoDir : "",
        Personas = personas.Listar().ToList(),
        Carpetas = carpetas.Listar().ToList(),
        Ollama = ollama
    });
});

app.MapGet("/api/personas", (PersonaStore store) => Results.Ok(store.Listar()));

app.MapPost("/api/personas", async (HttpRequest request, PersonaStore store) =>
{
    try
    {
        var p = await request.ReadFromJsonAsync<PersonaDef>();
        if (p == null || string.IsNullOrWhiteSpace(p.Nombre))
            return Results.BadRequest(new { ok = false, error = "Nombre requerido." });
        if (string.IsNullOrWhiteSpace(p.SystemPrompt))
            p.SystemPrompt = "Sos un asistente útil y preciso.";
        var saved = store.Guardar(p);
        return Results.Ok(new { ok = true, persona = saved });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { ok = false, error = ex.Message });
    }
});

app.MapGet("/api/carpetas", (CarpetaStore store) => Results.Ok(store.Listar()));

app.MapPost("/api/carpetas", async (HttpRequest request, CarpetaStore store) =>
{
    try
    {
        var body = await request.ReadFromJsonAsync<CarpetaChatDef>();
        if (body == null || string.IsNullOrWhiteSpace(body.Nombre))
            return Results.BadRequest(new { ok = false, error = "Nombre de carpeta requerido." });
        var saved = store.Crear(body.Id, body.Nombre, body.Descripcion);
        return Results.Ok(new { ok = true, carpeta = saved });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { ok = false, error = ex.Message });
    }
});

app.MapGet("/api/conversaciones", (ConversationStore store, string? carpetaId) =>
    Results.Ok(store.ListarResumenes(carpetaId)));

app.MapGet("/api/conversaciones/{id}", (string id, ConversationStore store) =>
{
    var doc = store.Obtener(id);
    return doc == null
        ? Results.NotFound(new { ok = false, error = "Conversación no encontrada." })
        : Results.Ok(doc);
});

app.MapDelete("/api/conversaciones/{id}", (string id, ConversationStore store) =>
{
    var ok = store.Eliminar(id);
    return ok ? Results.Ok(new { ok = true }) : Results.NotFound(new { ok = false });
});

app.MapPatch("/api/conversaciones/{id}", async (string id, HttpRequest request, ConversationStore store) =>
{
    try
    {
        var meta = await request.ReadFromJsonAsync<ConversacionMetaRequest>();
        if (meta == null) return Results.BadRequest(new { ok = false, error = "Cuerpo vacío." });
        var doc = store.ActualizarMeta(id, meta);
        return doc == null
            ? Results.NotFound(new { ok = false, error = "Conversación no encontrada." })
            : Results.Ok(new { ok = true, conversacion = doc });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { ok = false, error = ex.Message });
    }
});

app.MapGet("/api/aprendizajes", (AprendizajeStore store, string? carpetaId) =>
    Results.Ok(store.Listar(carpetaId)));

app.MapGet("/api/aprendizajes/{id}", (string id, AprendizajeStore store) =>
{
    var doc = store.Obtener(id);
    return doc == null
        ? Results.NotFound(new { ok = false, error = "Aprendizaje no encontrado." })
        : Results.Ok(doc);
});

app.MapDelete("/api/aprendizajes/{id}", (string id, AprendizajeStore store) =>
{
    var ok = store.Eliminar(id);
    return ok ? Results.Ok(new { ok = true }) : Results.NotFound(new { ok = false });
});

app.MapPost("/api/aprendizajes", async (
    HttpRequest request,
    AprendizajeStore aprendizajes,
    ConversationStore conversaciones) =>
{
    try
    {
        var req = await request.ReadFromJsonAsync<AprendizajeRequest>();
        if (req == null || string.IsNullOrWhiteSpace(req.Titulo) || string.IsNullOrWhiteSpace(req.Contenido))
            return Results.BadRequest(new { ok = false, error = "Título y contenido requeridos." });
        var doc = aprendizajes.Guardar(req);
        if (!string.IsNullOrWhiteSpace(req.ConversacionId))
            conversaciones.VincularAprendizaje(req.ConversacionId, doc.Id);
        return Results.Ok(new { ok = true, aprendizaje = doc });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { ok = false, error = ex.Message });
    }
});

app.MapPost("/api/chat", async (ChatRequest req, LlmOrchestrator llm, CancellationToken ct) =>
{
    var res = await llm.ChatAsync(req, ct);
    return res.Ok ? Results.Ok(res) : Results.Json(res, statusCode: 400);
}).WithRequestTimeout(TimeSpan.FromSeconds(timeoutMax)).RequireRateLimiting("chat");

app.MapPost("/api/chat/stream", async (ChatRequest req, LlmOrchestrator llm, HttpContext ctx, CancellationToken ct) =>
{
    await llm.ChatStreamAsync(req, ctx.Response, ct);
}).WithRequestTimeout(TimeSpan.FromSeconds(timeoutMax)).RequireRateLimiting("chat");

app.MapFallbackToFile("index.html");

if (!seg.EnlaceSoloLocalhost)
{
    var urls = builder.Configuration["ASPNETCORE_URLS"];
    if (string.IsNullOrWhiteSpace(urls))
        app.Urls.Add($"http://127.0.0.1:{port}");
}

app.Logger.LogInformation("IA propia v{Version} — raíz {Raiz} — conocimiento {Conocimiento}",
    versionInfo.Version, raiz, conocimientoDir);
app.Run();
