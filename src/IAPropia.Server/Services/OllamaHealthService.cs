using System.Text.Json;
using IAPropia.Models;

namespace IAPropia.Services;

public sealed class OllamaHealthService
{
    private readonly HttpClient _http;
    private readonly IaPropiaOptions _opts;

    public OllamaHealthService(IHttpClientFactory factory, IaPropiaOptions opts)
    {
        _http = factory.CreateClient("llm-web");
        _opts = opts;
    }

    public async Task<OllamaHealthDto> ComprobarAsync(CancellationToken ct = default)
    {
        var baseUrl = _opts.Ollama.BaseUrl.Trim().TrimEnd('/');
        var modeloCfg = _opts.Ollama.Modelo.Trim();
        var dto = new OllamaHealthDto
        {
            BaseUrl = baseUrl,
            ModeloConfigurado = modeloCfg
        };

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            dto.Ok = false;
            dto.Mensaje = "Falta URL de Ollama en appsettings.json.";
            return dto;
        }

        try
        {
            using var resp = await _http.GetAsync(baseUrl + "/api/tags", ct);
            var raw = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                dto.Ok = false;
                dto.Mensaje = $"Ollama respondió HTTP {(int)resp.StatusCode}. ¿Está corriendo? Probá: ollama serve";
                return dto;
            }

            dto.ModelosDisponibles = ParsearModelos(raw);
            dto.ModeloPresente = ModeloDisponible(modeloCfg, dto.ModelosDisponibles);
            dto.Ok = dto.ModeloPresente;
            dto.Mensaje = dto.Ok
                ? $"Ollama OK · modelo «{modeloCfg}» listo."
                : $"Ollama responde, pero falta el modelo «{modeloCfg}». Ejecutá: ollama pull {modeloCfg}";
            return dto;
        }
        catch (OperationCanceledException)
        {
            dto.Ok = false;
            dto.Mensaje = "Tiempo de espera agotado al consultar Ollama.";
            return dto;
        }
        catch (HttpRequestException)
        {
            dto.Ok = false;
            dto.Mensaje = "No se pudo conectar con Ollama. ¿Está corriendo? Probá: ollama serve";
            return dto;
        }
        catch (Exception ex)
        {
            dto.Ok = false;
            dto.Mensaje = "Error al consultar Ollama: " + ex.Message;
            return dto;
        }
    }

    public static bool ModeloDisponible(string modeloConfigurado, IReadOnlyList<string> disponibles)
    {
        if (string.IsNullOrWhiteSpace(modeloConfigurado) || disponibles.Count == 0)
            return false;
        var key = modeloConfigurado.Trim().ToLowerInvariant();
        foreach (var m in disponibles)
        {
            var name = m.ToLowerInvariant();
            if (name == key || name.StartsWith(key + ":", StringComparison.Ordinal)
                || name.StartsWith(key + "@", StringComparison.Ordinal))
                return true;
            // tag sin : (ej. llama3.2:latest vs llama3.2)
            var shortName = name.Split(':', '@')[0];
            if (shortName == key) return true;
        }

        return false;
    }

    private static List<string> ParsearModelos(string raw)
    {
        var list = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty("models", out var models)) return list;
            foreach (var item in models.EnumerateArray())
            {
                if (item.TryGetProperty("name", out var n))
                {
                    var s = n.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) list.Add(s);
                }
            }
        }
        catch
        {
            // ignorar JSON inválido
        }

        return list.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
