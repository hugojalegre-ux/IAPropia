using IAPropia.Models;

namespace IAPropia.Services;

/// <summary>Búsqueda simple por palabras en archivos .md/.txt (sin embeddings).</summary>
public sealed class KnowledgeRag
{
    private readonly string _carpeta;
    private readonly RagOptions _opts;

    public KnowledgeRag(IWebHostEnvironment env, IaPropiaOptions iaOpts)
    {
        _opts = iaOpts.Rag;
        var raiz = AppPaths.ResolverRaizProyecto(env);
        _carpeta = AppPaths.CarpetaConocimiento(raiz, _opts.CarpetaConocimiento);
        Directory.CreateDirectory(_carpeta);
    }

    public string CarpetaAbsoluta => _carpeta;

    public (string BloqueContexto, int Fragmentos) BuscarFragmentos(string consulta)
    {
        if (string.IsNullOrWhiteSpace(consulta))
            return ("", 0);

        var tokens = Tokenizar(consulta);
        if (tokens.Count == 0)
            return ("", 0);

        var hits = new List<(int Score, string Fuente, string Texto)>();

        foreach (var ext in new[] { "*.md", "*.txt", "*.json" })
        {
            foreach (var file in Directory.EnumerateFiles(_carpeta, ext, SearchOption.AllDirectories))
            {
                string text;
                try
                {
                    text = File.ReadAllText(file);
                }
                catch
                {
                    continue;
                }

                if (text.Length > 500_000)
                    text = text[..500_000];

                var rel = Path.GetRelativePath(_carpeta, file);
                var score = ScoreTexto(text, tokens);
                if (score <= 0) continue;

                var fragmento = ExtraerMejorFragmento(text, tokens);
                hits.Add((score, rel, fragmento));
            }
        }

        var top = hits
            .OrderByDescending(h => h.Score)
            .Take(_opts.MaxFragmentos)
            .ToList();

        if (top.Count == 0)
            return ("", 0);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Conocimiento local relevante (solo lectura, carpeta conocimiento/):");
        foreach (var h in top)
        {
            sb.AppendLine($"--- {h.Fuente} ---");
            sb.AppendLine(h.Texto);
            sb.AppendLine();
        }

        return (sb.ToString().Trim(), top.Count);
    }

    private string ExtraerMejorFragmento(string text, HashSet<string> tokens)
    {
        var maxLen = _opts.MaxCaracteresPorFragmento;
        var lines = text.Split('\n');
        var bestScore = 0;
        var bestStart = 0;
        var window = Math.Min(12, lines.Length);

        for (var i = 0; i < lines.Length; i++)
        {
            var chunk = string.Join('\n', lines.Skip(i).Take(window));
            var score = ScoreTexto(chunk, tokens);
            if (score > bestScore)
            {
                bestScore = score;
                bestStart = i;
            }
        }

        var fragment = string.Join('\n', lines.Skip(bestStart).Take(window));
        if (fragment.Length > maxLen)
            fragment = fragment[..maxLen] + "…";
        return fragment.Trim();
    }

    private static int ScoreTexto(string text, HashSet<string> tokens)
    {
        var lower = text.ToLowerInvariant();
        return tokens.Sum(t => lower.Split(t, StringSplitOptions.None).Length - 1);
    }

    private static HashSet<string> Tokenizar(string consulta)
    {
        return consulta
            .ToLowerInvariant()
            .Split([' ', '\t', '\r', '\n', ',', '.', ';', ':', '?', '!'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3)
            .Take(24)
            .ToHashSet();
    }
}
