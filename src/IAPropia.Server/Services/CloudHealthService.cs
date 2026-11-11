using System.Net.Http.Headers;
using IAPropia.Models;

namespace IAPropia.Services;

/// <summary>Preflight proveedor OpenAI-compatible (cloud / API con internet).</summary>
public sealed class CloudHealthService
{
    private readonly HttpClient _http;
    private readonly IaPropiaOptions _opts;

    public CloudHealthService(IHttpClientFactory factory, IaPropiaOptions opts)
    {
        _http = factory.CreateClient("llm");
        _opts = opts;
    }

    public async Task<CloudHealthDto> ComprobarAsync(CancellationToken ct = default)
    {
        var open = _opts.OpenAiCompatible;
        var dto = new CloudHealthDto
        {
            BaseUrl = open.BaseUrl.Trim().TrimEnd('/'),
            ModeloConfigurado = open.Modelo.Trim(),
            ModelosSugeridos = open.ModelosSugeridos?.Where(m => !string.IsNullOrWhiteSpace(m)).ToList() ?? [],
            ApiKeyPresente = !string.IsNullOrWhiteSpace(open.ApiKey)
        };

        if (!dto.ApiKeyPresente)
        {
            dto.Ok = false;
            dto.Mensaje =
                "Cloud: falta API key. Copiá appsettings.secrets.json.example → appsettings.secrets.json " +
                "en la carpeta del servidor (dist o src) y completá IAPropia.OpenAiCompatible.ApiKey.";
            return dto;
        }

        if (dto.ModelosSugeridos.Count == 0)
            dto.ModelosSugeridos = [dto.ModeloConfigurado];

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, dto.BaseUrl + "/models");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", open.ApiKey!.Trim());
            using var resp = await _http.SendAsync(req, ct);
            dto.ConectividadVerificada = resp.IsSuccessStatusCode;
            if (resp.IsSuccessStatusCode)
            {
                dto.Ok = true;
                dto.Mensaje = $"Cloud OK · {dto.BaseUrl} · modelo sugerido «{dto.ModeloConfigurado}».";
                return dto;
            }

            var body = await resp.Content.ReadAsStringAsync(ct);
            dto.Ok = false;
            dto.Mensaje = resp.StatusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized =>
                    "Cloud: API key rechazada (401). Revisá appsettings.secrets.json.",
                System.Net.HttpStatusCode.Forbidden =>
                    "Cloud: acceso denegado (403). Revisá cuenta o límites del proveedor.",
                _ => $"Cloud: HTTP {(int)resp.StatusCode}. {Truncar(body, 200)}"
            };
            return dto;
        }
        catch (OperationCanceledException)
        {
            dto.Ok = false;
            dto.Mensaje = "Cloud: tiempo de espera agotado al contactar el proveedor.";
            return dto;
        }
        catch (HttpRequestException)
        {
            dto.ApiKeyPresente = true;
            dto.Ok = true;
            dto.ConectividadVerificada = false;
            dto.Mensaje =
                "Cloud: API key configurada, pero no hay conexión ahora (sin internet o URL incorrecta). " +
                "Podés intentar enviar igual.";
            return dto;
        }
        catch (Exception ex)
        {
            dto.Ok = false;
            dto.Mensaje = "Cloud: " + ex.Message;
            return dto;
        }
    }

    private static string Truncar(string s, int max) =>
        s.Length <= max ? s : s[..max] + "...";
}
