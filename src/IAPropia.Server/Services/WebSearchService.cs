using System.Text.Json;
using IAPropia.Models;

namespace IAPropia.Services;

/// <summary>Búsqueda web ligera (DuckDuckGo JSON) — internet sin API de chat de pago.</summary>
public sealed class WebSearchService
{
    private readonly HttpClient _http;
    private readonly InternetBusquedaOptions _opts;

    public WebSearchService(IHttpClientFactory factory, IaPropiaOptions iaOpts)
    {
        _http = factory.CreateClient("llm-web");
        _opts = iaOpts.InternetBusqueda;
    }

    public async Task<(string Bloque, int Fragmentos, List<string> Avisos)> BuscarAsync(
        string consulta,
        CancellationToken ct)
    {
        var avisos = new List<string>();
        if (!_opts.Habilitada)
        {
            avisos.Add("Búsqueda web deshabilitada en configuración.");
            return ("", 0, avisos);
        }

        if (string.IsNullOrWhiteSpace(consulta))
            return ("", 0, avisos);

        var q = Uri.EscapeDataString(consulta.Trim());
        var url = $"https://api.duckduckgo.com/?q={q}&format=json&no_redirect=1&no_html=1";
        try
        {
            var raw = await _http.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            var fragmentos = new List<string>();

            if (root.TryGetProperty("AbstractText", out var abs))
            {
                var t = (abs.GetString() ?? "").Trim();
                if (t.Length > 0) fragmentos.Add(t);
            }

            if (root.TryGetProperty("RelatedTopics", out var topics))
            {
                foreach (var item in topics.EnumerateArray())
                {
                    if (fragmentos.Count >= _opts.MaxFragmentos) break;
                    if (item.TryGetProperty("Text", out var txt))
                    {
                        var t = (txt.GetString() ?? "").Trim();
                        if (t.Length > 10) fragmentos.Add(t);
                    }
                }
            }

            if (fragmentos.Count == 0)
            {
                avisos.Add("Búsqueda web sin resultados útiles para esta consulta.");
                return ("", 0, avisos);
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Resultados de búsqueda web (DuckDuckGo, solo lectura):");
            var i = 1;
            foreach (var f in fragmentos.Take(_opts.MaxFragmentos))
            {
                sb.AppendLine($"{i++}. {f}");
            }

            avisos.Add($"Internet: {fragmentos.Count} fragmento(s) agregados al contexto.");
            return (sb.ToString().Trim(), fragmentos.Count, avisos);
        }
        catch (Exception ex)
        {
            avisos.Add("Búsqueda web falló: " + ex.Message);
            return ("", 0, avisos);
        }
    }
}
