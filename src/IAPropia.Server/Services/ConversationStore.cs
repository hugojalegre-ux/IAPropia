using System.Text.Json;
using System.Text.Json.Serialization;
using IAPropia.Models;

namespace IAPropia.Services;

public sealed class ConversationStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _chatsRoot;
    private readonly string _legacyDir;
    private readonly IaPropiaOptions _opts;
    private readonly CarpetaStore _carpetas;

    public ConversationStore(IWebHostEnvironment env, IaPropiaOptions opts, CarpetaStore carpetas)
    {
        _opts = opts;
        _carpetas = carpetas;
        var raiz = AppPaths.ResolverRaizProyecto(env);
        _chatsRoot = AppPaths.CarpetaChats(raiz);
        _legacyDir = AppPaths.CarpetaConversacionesLegacy(raiz);
        Directory.CreateDirectory(_chatsRoot);
        MigrarLegacySiCorresponde();
    }

    public IReadOnlyList<ConversacionResumen> ListarResumenes(string? carpetaId = null)
    {
        var list = new List<ConversacionResumen>();
        foreach (var path in EnumerarArchivos(carpetaId))
        {
            try
            {
                var doc = LeerArchivo(path);
                list.Add(ToResumen(doc));
            }
            catch { /* ignorar */ }
        }

        return list.OrderByDescending(x => x.ActualizadaUtc).ToList();
    }

    public ConversacionDoc? Obtener(string id)
    {
        var path = BuscarRuta(id);
        return path == null ? null : LeerArchivo(path);
    }

    public ConversacionDoc ObtenerOCrear(string? conversacionId, string personaId, string? carpetaId, string? titulo)
    {
        if (!string.IsNullOrWhiteSpace(conversacionId))
        {
            var existente = Obtener(conversacionId);
            if (existente != null)
                return existente;
        }

        var carpeta = AppPaths.SanitizeSlug(carpetaId ?? _opts.Chats.CarpetaPorDefecto);
        _carpetas.AsegurarDirectorio(carpeta);
        var now = DateTime.UtcNow.ToString("o");
        return new ConversacionDoc
        {
            Id = Guid.NewGuid().ToString("N"),
            Titulo = string.IsNullOrWhiteSpace(titulo) ? "Nueva conversación" : titulo.Trim(),
            CarpetaId = carpeta,
            PersonaId = personaId,
            CreadaUtc = now,
            ActualizadaUtc = now,
            Mensajes = []
        };
    }

    public ConversacionDoc? ActualizarMeta(string id, ConversacionMetaRequest meta)
    {
        var doc = Obtener(id);
        if (doc == null) return null;

        var carpetaAnterior = doc.CarpetaId;
        if (!string.IsNullOrWhiteSpace(meta.Titulo))
            doc.Titulo = meta.Titulo.Trim();
        if (!string.IsNullOrWhiteSpace(meta.CarpetaId))
            doc.CarpetaId = AppPaths.SanitizeSlug(meta.CarpetaId);
        if (meta.RelacionadasIds != null)
            doc.RelacionadasIds = meta.RelacionadasIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
        if (!string.IsNullOrWhiteSpace(meta.ModoInteraccion))
            doc.ModoInteraccion = ModoInteraccionHelper.Normalizar(meta.ModoInteraccion);

        if (!doc.CarpetaId.Equals(carpetaAnterior, StringComparison.OrdinalIgnoreCase))
        {
            var rutaVieja = Ruta(carpetaAnterior, doc.Id);
            Guardar(doc);
            if (File.Exists(rutaVieja) && rutaVieja != Ruta(doc.CarpetaId, doc.Id))
                File.Delete(rutaVieja);
        }
        else
        {
            Guardar(doc);
        }

        return doc;
    }

    public void Guardar(ConversacionDoc doc)
    {
        doc.CarpetaId = AppPaths.SanitizeSlug(
            string.IsNullOrWhiteSpace(doc.CarpetaId) ? _opts.Chats.CarpetaPorDefecto : doc.CarpetaId);
        _carpetas.AsegurarDirectorio(doc.CarpetaId);
        doc.ActualizadaUtc = DateTime.UtcNow.ToString("o");
        if (doc.Mensajes.Count > _opts.Limites.MaxMensajesHistorial)
        {
            var skip = doc.Mensajes.Count - _opts.Limites.MaxMensajesHistorial;
            doc.Mensajes = doc.Mensajes.Skip(skip).ToList();
        }

        File.WriteAllText(Ruta(doc.CarpetaId, doc.Id), JsonSerializer.Serialize(doc, JsonOpts));
    }

    public void VincularAprendizaje(string conversacionId, string aprendizajeId)
    {
        var doc = Obtener(conversacionId);
        if (doc == null) return;
        if (!doc.AprendizajeIds.Contains(aprendizajeId))
            doc.AprendizajeIds.Add(aprendizajeId);
        Guardar(doc);
    }

    public bool Eliminar(string id)
    {
        var path = BuscarRuta(id);
        if (path == null || !File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    private IEnumerable<string> EnumerarArchivos(string? carpetaId)
    {
        if (!string.IsNullOrWhiteSpace(carpetaId))
        {
            var dir = Path.Combine(_chatsRoot, AppPaths.SanitizeSlug(carpetaId));
            if (Directory.Exists(dir))
                foreach (var f in Directory.EnumerateFiles(dir, "*.json")) yield return f;
            yield break;
        }

        foreach (var dir in Directory.EnumerateDirectories(_chatsRoot))
        foreach (var f in Directory.EnumerateFiles(dir, "*.json"))
            yield return f;

        if (Directory.Exists(_legacyDir))
        foreach (var f in Directory.EnumerateFiles(_legacyDir, "*.json"))
            yield return f;
    }

    private string? BuscarRuta(string id)
    {
        var safe = SanitizeId(id);
        foreach (var path in EnumerarArchivos(null))
        {
            if (Path.GetFileNameWithoutExtension(path).Equals(safe, StringComparison.OrdinalIgnoreCase))
                return path;
        }
        return null;
    }

    private string Ruta(string carpetaId, string id) =>
        Path.Combine(_chatsRoot, AppPaths.SanitizeSlug(carpetaId), SanitizeId(id) + ".json");

    private static ConversacionDoc LeerArchivo(string path)
    {
        var doc = JsonSerializer.Deserialize<ConversacionDoc>(File.ReadAllText(path), JsonOpts)
                  ?? throw new InvalidOperationException("Conversación vacía.");
        if (string.IsNullOrWhiteSpace(doc.Id))
            doc.Id = Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrWhiteSpace(doc.CarpetaId))
            doc.CarpetaId = AppPaths.SanitizeSlug(Path.GetFileName(Path.GetDirectoryName(path) ?? "") ?? "principal");
        doc.RelacionadasIds ??= [];
        doc.AprendizajeIds ??= [];
        return doc;
    }

    private static ConversacionResumen ToResumen(ConversacionDoc doc) => new()
    {
        Id = doc.Id,
        Titulo = doc.Titulo,
        CarpetaId = doc.CarpetaId,
        PersonaId = doc.PersonaId,
        CreadaUtc = doc.CreadaUtc,
        ActualizadaUtc = doc.ActualizadaUtc,
        CantidadMensajes = doc.Mensajes.Count,
        RelacionadasIds = doc.RelacionadasIds
    };

    private void MigrarLegacySiCorresponde()
    {
        if (!Directory.Exists(_legacyDir)) return;
        var dest = Path.Combine(_chatsRoot, AppPaths.SanitizeSlug(_opts.Chats.CarpetaPorDefecto));
        Directory.CreateDirectory(dest);
        foreach (var f in Directory.EnumerateFiles(_legacyDir, "*.json"))
        {
            var name = Path.GetFileName(f);
            var target = Path.Combine(dest, name);
            if (!File.Exists(target))
                File.Move(f, target);
        }
    }

    private static string SanitizeId(string id)
    {
        var s = (id ?? "").Trim();
        foreach (var c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '-');
        return s;
    }
}
