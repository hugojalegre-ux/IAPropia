using System.Text.Json;
using System.Text.Json.Serialization;
using IAPropia.Models;

namespace IAPropia.Services;

public sealed class AprendizajeStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _dir;

    public AprendizajeStore(IWebHostEnvironment env)
    {
        var raiz = AppPaths.ResolverRaizProyecto(env);
        _dir = AppPaths.CarpetaAprendizajes(raiz);
        Directory.CreateDirectory(_dir);
    }

    public IReadOnlyList<AprendizajeDoc> Listar(string? carpetaId = null)
    {
        return Directory.EnumerateFiles(_dir, "*.json")
            .Select(path =>
            {
                try { return JsonSerializer.Deserialize<AprendizajeDoc>(File.ReadAllText(path), JsonOpts); }
                catch { return null; }
            })
            .Where(x => x != null)
            .Cast<AprendizajeDoc>()
            .Where(x => string.IsNullOrWhiteSpace(carpetaId)
                || x.CarpetaId.Equals(AppPaths.SanitizeSlug(carpetaId), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.CreadoUtc)
            .ToList();
    }

    public AprendizajeDoc? Obtener(string id)
    {
        var path = Ruta(id);
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<AprendizajeDoc>(File.ReadAllText(path), JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    public bool Eliminar(string id)
    {
        var path = Ruta(id);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    public AprendizajeDoc Guardar(AprendizajeRequest req)
    {
        var doc = new AprendizajeDoc
        {
            Id = "apr-" + Guid.NewGuid().ToString("N")[..10],
            Titulo = req.Titulo.Trim(),
            Contenido = req.Contenido.Trim(),
            CarpetaId = AppPaths.SanitizeSlug(req.CarpetaId ?? "principal"),
            ConversacionOrigenIds = string.IsNullOrWhiteSpace(req.ConversacionId) ? [] : [req.ConversacionId!],
            ConversacionRelacionadasIds = req.RelacionadasIds?.ToList() ?? [],
            CreadoUtc = DateTime.UtcNow.ToString("o")
        };
        File.WriteAllText(Ruta(doc.Id), JsonSerializer.Serialize(doc, JsonOpts));
        return doc;
    }

    public (string Bloque, int Cantidad) BuscarParaChat(
        string consulta,
        string carpetaId,
        IEnumerable<string> relacionadasIds,
        int max = 5)
    {
        var tokens = Tokenizar(consulta);
        var pool = Listar(carpetaId).Concat(Listar(null).Where(a =>
            relacionadasIds.Any(r => a.ConversacionOrigenIds.Contains(r)
                || a.ConversacionRelacionadasIds.Contains(r)))).DistinctBy(a => a.Id);

        var ranked = pool
            .Select(a => new { Doc = a, Score = Score(a, tokens) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(max)
            .ToList();

        if (ranked.Count == 0) return ("", 0);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Aprendizajes guardados (chats relacionados):");
        foreach (var h in ranked)
        {
            sb.AppendLine($"--- {h.Doc.Titulo} ---");
            sb.AppendLine(h.Doc.Contenido);
        }
        return (sb.ToString().Trim(), ranked.Count);
    }

    private static int Score(AprendizajeDoc doc, HashSet<string> tokens)
    {
        var text = (doc.Titulo + " " + doc.Contenido).ToLowerInvariant();
        return tokens.Sum(t => text.Split(t, StringSplitOptions.None).Length - 1);
    }

    private static HashSet<string> Tokenizar(string consulta) =>
        consulta.ToLowerInvariant()
            .Split([' ', '\t', '\r', '\n', ',', '.', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3).Take(20).ToHashSet();

    private string Ruta(string id) => Path.Combine(_dir, AppPaths.SanitizeSlug(id) + ".json");
}
