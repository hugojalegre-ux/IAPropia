using IAPropia.Models;
using IAPropia.Services;

var contentRoot = AppContext.BaseDirectory;
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = contentRoot
});

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
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

var timeoutMax = Math.Clamp(iaOpts.Limites.TimeoutMaximo, 60, 3600);
var timeoutWeb = Math.Clamp(iaOpts.Limites.TimeoutBusquedaWebSegundos, 10, 120);

builder.Services.AddHttpClient("ollama", c =>
{
    c.Timeout = TimeSpan.FromSeconds(timeoutMax);
});

builder.Services.AddHttpClient("llm-web", c =>
{
    c.Timeout = TimeSpan.FromSeconds(timeoutWeb);
});

var timeoutNormal = Math.Clamp(iaOpts.Limites.TimeoutSegundos, 30, timeoutMax);
builder.Services.AddHttpClient("llm", c =>
{
    c.Timeout = TimeSpan.FromSeconds(timeoutNormal);
});

builder.Services.AddRequestTimeouts();

builder.Services.AddCors(o =>
    o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

var fileLog = app.Services.GetRequiredService<IaFileLog>();
fileLog.LogStartup($"IA propia arrancó · proveedor={iaOpts.ProveedorPorDefecto}");

app.UseCors();
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
    Mensaje = "Servidor IA propia en ejecución."
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
        CarpetaDatos = dataDir,
        CarpetaConocimiento = conocimientoDir,
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
}).WithRequestTimeout(TimeSpan.FromSeconds(timeoutMax));

app.MapPost("/api/chat/stream", async (ChatRequest req, LlmOrchestrator llm, HttpContext ctx, CancellationToken ct) =>
{
    await llm.ChatStreamAsync(req, ctx.Response, ct);
}).WithRequestTimeout(TimeSpan.FromSeconds(timeoutMax));

app.MapFallbackToFile("index.html");

var urls = builder.Configuration["ASPNETCORE_URLS"];
if (string.IsNullOrWhiteSpace(urls))
{
    var port = iaOpts.PuertoPreferido > 0 ? iaOpts.PuertoPreferido : 5077;
    app.Urls.Add($"http://127.0.0.1:{port}");
}

app.Logger.LogInformation("IA propia v{Version} — raíz {Raiz} — conocimiento {Conocimiento}",
    versionInfo.Version, raiz, conocimientoDir);
app.Run();
