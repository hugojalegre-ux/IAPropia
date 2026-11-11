using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using IAPropia.Models;

namespace IAPropia.Services;

public sealed class LlmOrchestrator
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly IaPropiaOptions _opts;
    private readonly PersonaStore _personas;
    private readonly ConversationStore _conversaciones;
    private readonly KnowledgeRag _rag;
    private readonly IaFileLog _log;
    private readonly WebSearchService _web;
    private readonly AprendizajeStore _aprendizajes;

    public LlmOrchestrator(
        IHttpClientFactory httpClientFactory,
        IaPropiaOptions opts,
        PersonaStore personas,
        ConversationStore conversaciones,
        KnowledgeRag rag,
        IaFileLog log,
        WebSearchService web,
        AprendizajeStore aprendizajes)
    {
        _http = httpClientFactory.CreateClient("llm");
        _opts = opts;
        _personas = personas;
        _conversaciones = conversaciones;
        _rag = rag;
        _log = log;
        _web = web;
        _aprendizajes = aprendizajes;
    }

    public async Task<ChatResponse> ChatAsync(ChatRequest req, CancellationToken ct)
    {
        var avisos = new List<string>();
        var mensaje = (req.Mensaje ?? "").Trim();
        if (mensaje.Length == 0)
            return Fail("Escribí un mensaje.");

        var personaId = (req.PersonaId ?? "asistente").Trim();
        var persona = _personas.Obtener(personaId)
                      ?? _personas.Obtener("asistente")
                      ?? new PersonaDef { Id = "asistente", SystemPrompt = "Sos un asistente útil." };

        var doc = _conversaciones.ObtenerOCrear(
            req.ConversacionId,
            persona.Id,
            req.CarpetaId,
            req.Titulo);

        if (!string.IsNullOrWhiteSpace(req.Titulo))
            doc.Titulo = req.Titulo.Trim();
        else if (doc.Titulo == "Nueva conversación" && mensaje.Length > 0)
            doc.Titulo = Truncar(mensaje, 60);

        if (req.RelacionadasIds is { Count: > 0 })
            doc.RelacionadasIds = req.RelacionadasIds.Distinct().Where(x => x != doc.Id).ToList();

        var proveedor = _opts.ModoSoloLocal
            ? "ollama"
            : NormalizarProveedor(req.Proveedor ?? _opts.ProveedorPorDefecto);
        var modelo = (req.Modelo ?? "").Trim();

        var contextos = new List<string>();
        var fragCount = 0;
        var fragWeb = 0;
        var fragApr = 0;

        if (req.UsarConocimientoLocal)
        {
            var (contextoRag, n) = _rag.BuscarFragmentos(mensaje);
            fragCount = n;
            if (n > 0) contextos.Add(contextoRag);
            else avisos.Add("Sin fragmentos en conocimiento/ para esta consulta.");
        }

        if (req.UsarBusquedaInternet)
        {
            var (bloqueWeb, nWeb, avisosWeb) = await _web.BuscarAsync(mensaje, ct);
            fragWeb = nWeb;
            avisos.AddRange(avisosWeb);
            if (nWeb > 0) contextos.Add(bloqueWeb);
        }

        var (bloqueApr, nApr) = _aprendizajes.BuscarParaChat(
            mensaje,
            doc.CarpetaId,
            doc.RelacionadasIds);
        fragApr = nApr;
        if (nApr > 0)
        {
            contextos.Add(bloqueApr);
            avisos.Add($"Aprendizajes vinculados: {nApr} fragmento(s).");
        }

        if (doc.RelacionadasIds.Count > 0)
            avisos.Add($"Chats relacionados: {string.Join(", ", doc.RelacionadasIds.Take(5))}.");

        var system = ConstruirSystem(persona, contextos);
        var historial = doc.Mensajes
            .Select(m => new LlmMessage(m.Role, m.Content))
            .ToList();
        historial.Add(new LlmMessage("user", mensaje));

        doc.Mensajes.Add(new ChatMessageDto { Role = "user", Content = mensaje });

        string respuesta;
        string modeloUsado;
        try
        {
            (respuesta, modeloUsado) = proveedor switch
            {
                "openai" => await CompletarOpenAiCompatibleAsync(system, historial, modelo, ct),
                _ => await CompletarOllamaAsync(system, historial, modelo, ct)
            };
        }
        catch (Exception ex)
        {
            _log.LogError($"chat proveedor={proveedor}: {ex.Message}");
            var msg = proveedor == "ollama"
                ? (ex is InvalidOperationException ? ex.Message : OllamaErrorMapper.DesdeExcepcion(ex, modelo, null, null))
                : ex.Message;
            return Fail(msg, doc.Id, avisos);
        }

        doc.Mensajes.Add(new ChatMessageDto { Role = "assistant", Content = respuesta });
        _conversaciones.Guardar(doc);

        return new ChatResponse
        {
            Ok = true,
            ConversacionId = doc.Id,
            Respuesta = respuesta,
            ProveedorUsado = proveedor,
            ModeloUsado = modeloUsado,
            FragmentosConocimiento = fragCount,
            FragmentosInternet = fragWeb,
            FragmentosAprendizajes = fragApr,
            Avisos = avisos
        };
    }

    private async Task<(string Texto, string Modelo)> CompletarOllamaAsync(
        string system,
        List<LlmMessage> historial,
        string? modeloOverride,
        CancellationToken ct)
    {
        var model = string.IsNullOrWhiteSpace(modeloOverride) ? _opts.Ollama.Modelo : modeloOverride;
        var url = _opts.Ollama.BaseUrl.TrimEnd('/') + "/api/chat";

        var messages = new List<object> { new { role = "system", content = system } };
        foreach (var m in historial)
            messages.Add(new { role = m.Role, content = m.Content });

        var body = new { model, stream = false, messages };
        try
        {
            using var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            using var resp = await _http.PostAsync(url, content, ct);
            var raw = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                var msg = OllamaErrorMapper.DesdeExcepcion(
                    new InvalidOperationException(), model, (int)resp.StatusCode, raw);
                _log.LogError($"Ollama HTTP {(int)resp.StatusCode} model={model}");
                throw new InvalidOperationException(msg);
            }

            using var json = JsonDocument.Parse(raw);
            var text = json.RootElement.TryGetProperty("message", out var msgEl)
                         && msgEl.TryGetProperty("content", out var c)
                ? c.GetString() ?? ""
                : "";
            if (string.IsNullOrWhiteSpace(text))
            {
                var vacio = $"Ollama devolvió respuesta vacía para «{model}». ¿Está el modelo descargado? ollama pull {model}";
                _log.LogError(vacio);
                throw new InvalidOperationException(vacio);
            }

            return (text.Trim(), model);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var msg = OllamaErrorMapper.DesdeExcepcion(ex, model, null, null);
            _log.LogError(msg);
            throw new InvalidOperationException(msg, ex);
        }
    }

    private async Task<(string Texto, string Modelo)> CompletarOpenAiCompatibleAsync(
        string system,
        List<LlmMessage> historial,
        string? modeloOverride,
        CancellationToken ct)
    {
        var apiKey = _opts.OpenAiCompatible.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                "Falta API key. Copiá appsettings.secrets.json.example → appsettings.secrets.json y completá OpenAiCompatible.ApiKey.");

        var model = string.IsNullOrWhiteSpace(modeloOverride)
            ? _opts.OpenAiCompatible.Modelo
            : modeloOverride;
        var url = _opts.OpenAiCompatible.BaseUrl.TrimEnd('/') + "/chat/completions";

        var messages = new List<object> { new { role = "system", content = system } };
        foreach (var m in historial)
            messages.Add(new { role = m.Role, content = m.Content });

        var body = new { model, temperature = 0.4, messages };
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, ct);
        var raw = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"LLM HTTP {(int)resp.StatusCode}: {Truncar(raw, 400)}");

        using var json = JsonDocument.Parse(raw);
        var text = json.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";
        return (text.Trim(), model);
    }

    private static string ConstruirSystem(PersonaDef persona, IReadOnlyList<string> contextos)
    {
        var sb = new StringBuilder();
        sb.AppendLine(persona.SystemPrompt.Trim());
        sb.AppendLine();
        sb.AppendLine("Reglas:");
        sb.AppendLine("- Respondé en el idioma del usuario.");
        sb.AppendLine("- No inventes acceso a sistemas que no tenés.");
        sb.AppendLine("- Cerebro local (Ollama); internet solo como contexto si se inyectó búsqueda.");
        sb.AppendLine("- Este proyecto es independiente de SOT Accusys.");
        foreach (var ctx in contextos)
        {
            if (string.IsNullOrWhiteSpace(ctx)) continue;
            sb.AppendLine();
            sb.AppendLine(ctx);
        }

        return sb.ToString().Trim();
    }

    private static string NormalizarProveedor(string p) =>
        p.Trim().ToLowerInvariant() switch
        {
            "openai" or "api" or "cloud" => "openai",
            _ => "ollama"
        };

    private static ChatResponse Fail(string error, string? convId = null, List<string>? avisos = null) =>
        new()
        {
            Ok = false,
            Error = error,
            ConversacionId = convId ?? "",
            Avisos = avisos ?? []
        };

    private static string Truncar(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";

    private sealed record LlmMessage(string Role, string Content);
}
