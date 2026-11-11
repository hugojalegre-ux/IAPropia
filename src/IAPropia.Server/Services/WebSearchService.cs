using System.Text.Json;
using System.Text.RegularExpressions;
using IAPropia.Models;

namespace IAPropia.Services;

/// <summary>Búsqueda web (DuckDuckGo JSON + respaldo HTML lite) sin API de pago.</summary>
public sealed class WebSearchService
{
    private static readonly Regex HtmlSnippetRegex = new(
        @"class=""result-snippet"">\s*([^<]+)\s*</",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex HtmlTitleRegex = new(
        @"class=""result-link""[^>]*>([^<]+)</a>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly HttpClient _http;
    private readonly InternetBusquedaOptions _opts;
    private readonly IaFileLog _log;

    public WebSearchService(IHttpClientFactory factory, IaPropiaOptions iaOpts, IaFileLog log)
    {
        _http = factory.CreateClient("llm-web");
        _opts = iaOpts.InternetBusqueda;
        _log = log;
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

        var consultaWeb = RefinarConsulta(consulta);
        var tokens = Tokenizar(consultaWeb);

        List<string> fragmentos;
        try
        {
            fragmentos = await BuscarJsonInstantAsync(consultaWeb, ct);
            if (fragmentos.Count == 0 && _opts.UsarRespaldoHtml)
            {
                var htmlHits = await BuscarHtmlLiteAsync(consultaWeb, ct);
                fragmentos.AddRange(htmlHits);
                if (htmlHits.Count > 0)
                    avisos.Add("Internet: respaldo HTML lite (resultados de búsqueda).");
            }
        }
        catch (Exception ex)
        {
            _log.LogError($"web search: {ex.Message}");
            avisos.Add("Búsqueda web falló: " + ex.Message);
            return ("", 0, avisos);
        }

        fragmentos = fragmentos
            .Select(f => TruncarFragmento(f))
            .Where(f => f.Length > 15)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (tokens.Count > 0)
        {
            var conPuntaje = fragmentos
                .Select(f => (Texto: f, Score: PuntajeRelevancia(f, tokens)))
                .OrderByDescending(x => x.Score)
                .ToList();

            var mejor = conPuntaje.FirstOrDefault().Score;
            if (mejor <= 0)
            {
                avisos.Add(
                    "Búsqueda web sin resultados relevantes para tu pregunta. El modelo no debe inventar datos de internet.");
                return ("", 0, avisos);
            }

            fragmentos = conPuntaje
                .Where(x => x.Score >= Math.Max(1, mejor - 1))
                .Select(x => x.Texto)
                .Take(_opts.MaxFragmentos)
                .ToList();
        }
        else
        {
            fragmentos = fragmentos.Take(_opts.MaxFragmentos).ToList();
        }

        if (fragmentos.Count == 0)
        {
            avisos.Add("Búsqueda web sin resultados útiles. Respondé sin inventar hechos externos.");
            return ("", 0, avisos);
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Resultados web (DuckDuckGo — datos NO verificados; pueden estar desactualizados o ser irrelevantes):");
        sb.AppendLine("Si no responden la pregunta del usuario, ignorálos y decilo en la respuesta.");
        var i = 1;
        foreach (var f in fragmentos)
            sb.AppendLine($"{i++}. {f}");

        avisos.Add($"Internet: {fragmentos.Count} fragmento(s) relevantes.");
        return (sb.ToString().Trim(), fragmentos.Count, avisos);
    }

    private async Task<List<string>> BuscarJsonInstantAsync(string consulta, CancellationToken ct)
    {
        var list = new List<string>();
        var q = Uri.EscapeDataString(consulta);
        var url = $"https://api.duckduckgo.com/?q={q}&format=json&no_redirect=1&no_html=1&skip_disambig=1";

        var raw = await _http.GetStringAsync(url, ct);
        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;

        if (root.TryGetProperty("Answer", out var ans))
        {
            var t = (ans.GetString() ?? "").Trim();
            if (t.Length > 2) list.Add(t);
        }

        if (root.TryGetProperty("AbstractText", out var abs))
        {
            var t = (abs.GetString() ?? "").Trim();
            if (t.Length > 0)
            {
                var heading = root.TryGetProperty("Heading", out var h) ? h.GetString() : null;
                if (!string.IsNullOrWhiteSpace(heading))
                    list.Add($"{heading.Trim()}: {t}");
                else
                    list.Add(t);
            }
        }

        if (root.TryGetProperty("Definition", out var def))
        {
            var t = (def.GetString() ?? "").Trim();
            if (t.Length > 0) list.Add(t);
        }

        AgregarRelatedTopics(root, list);
        return list;
    }

    private void AgregarRelatedTopics(JsonElement root, List<string> list)
    {
        if (!root.TryGetProperty("RelatedTopics", out var topics))
            return;

        foreach (var item in topics.EnumerateArray())
        {
            if (list.Count >= _opts.MaxFragmentos * 2) break;

            if (item.TryGetProperty("Text", out var txt))
            {
                var t = (txt.GetString() ?? "").Trim();
                if (t.Length > 10) list.Add(t);
                continue;
            }

            if (item.TryGetProperty("Topics", out var nested))
            {
                foreach (var sub in nested.EnumerateArray())
                {
                    if (list.Count >= _opts.MaxFragmentos * 2) break;
                    if (sub.TryGetProperty("Text", out var st))
                    {
                        var t = (st.GetString() ?? "").Trim();
                        if (t.Length > 10) list.Add(t);
                    }
                }
            }
        }
    }

    private async Task<List<string>> BuscarHtmlLiteAsync(string consulta, CancellationToken ct)
    {
        var q = Uri.EscapeDataString(consulta);
        var url = $"https://lite.duckduckgo.com/lite/?q={q}";
        var html = await _http.GetStringAsync(url, ct);

        var snippets = HtmlSnippetRegex.Matches(html).Select(m => DecodificarHtml(m.Groups[1].Value.Trim())).ToList();
        var titles = HtmlTitleRegex.Matches(html).Select(m => DecodificarHtml(m.Groups[1].Value.Trim())).ToList();

        var merged = new List<string>();
        for (var i = 0; i < Math.Min(snippets.Count, titles.Count) && merged.Count < _opts.MaxFragmentos * 2; i++)
        {
            var snip = snippets[i];
            if (snip.Length < 12) continue;
            var title = titles[i];
            merged.Add(string.IsNullOrWhiteSpace(title) ? snip : $"{title} — {snip}");
        }

        foreach (var snip in snippets.Skip(titles.Count))
        {
            if (merged.Count >= _opts.MaxFragmentos * 2) break;
            if (snip.Length >= 12) merged.Add(snip);
        }

        return merged;
    }

    private string RefinarConsulta(string consulta)
    {
        var max = Math.Clamp(_opts.MaxCaracteresConsulta, 40, 300);
        var t = consulta.Trim().Replace('\n', ' ').Replace('\r', ' ');
        while (t.Contains("  ", StringComparison.Ordinal)) t = t.Replace("  ", " ");

        if (t.Length <= max) return t;

        var corte = t.LastIndexOf('?', Math.Min(t.Length - 1, max));
        if (corte >= 40) return t[..(corte + 1)];

        corte = t.LastIndexOf('.', Math.Min(t.Length - 1, max));
        if (corte >= 40) return t[..(corte + 1)];

        return t[..max];
    }

    private string TruncarFragmento(string f)
    {
        var max = Math.Clamp(_opts.MaxCaracteresFragmento, 80, 1200);
        f = f.Trim();
        return f.Length <= max ? f : f[..max] + "…";
    }

    private static int PuntajeRelevancia(string texto, HashSet<string> tokens)
    {
        if (tokens.Count == 0) return 1;
        var lower = texto.ToLowerInvariant();
        return tokens.Sum(tok => lower.Contains(tok, StringComparison.Ordinal) ? 1 : 0);
    }

    private static HashSet<string> Tokenizar(string consulta) =>
        consulta
            .ToLowerInvariant()
            .Split([' ', '\t', ',', '.', ';', ':', '?', '!', '(', ')', '"', '¿', '¡'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3 && !EsStopword(w))
            .Take(12)
            .ToHashSet();

    private static bool EsStopword(string w) =>
        w is "para" or "como" or "qué" or "que" or "con" or "por" or "los" or "las" or "una" or "uno" or "del" or "the" or "and";

    private static string DecodificarHtml(string s) =>
        System.Net.WebUtility.HtmlDecode(s);
}
