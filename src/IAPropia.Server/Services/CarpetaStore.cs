using System.Text.Json;
using System.Text.Json.Serialization;
using IAPropia.Models;

namespace IAPropia.Services;

public sealed class CarpetaStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _archivo;
    private readonly string _chatsRoot;

    public CarpetaStore(IWebHostEnvironment env)
    {
        var raiz = AppPaths.ResolverRaizProyecto(env);
        _archivo = AppPaths.ArchivoCatalogoCarpetas(raiz);
        _chatsRoot = AppPaths.CarpetaChats(raiz);
        Directory.CreateDirectory(_chatsRoot);
        AsegurarCatalogo();
    }

    public IReadOnlyList<CarpetaChatDef> Listar()
    {
        var doc = Leer();
        return doc.Carpetas.OrderBy(c => c.Nombre).ToList();
    }

    public CarpetaChatDef Crear(string? id, string nombre, string? descripcion)
    {
        var doc = Leer();
        var slug = string.IsNullOrWhiteSpace(id)
            ? AppPaths.SanitizeSlug(nombre)
            : AppPaths.SanitizeSlug(id);
        if (doc.Carpetas.Any(c => c.Id.Equals(slug, StringComparison.OrdinalIgnoreCase)))
            return doc.Carpetas.First(c => c.Id.Equals(slug, StringComparison.OrdinalIgnoreCase));

        var carpeta = new CarpetaChatDef
        {
            Id = slug,
            Nombre = string.IsNullOrWhiteSpace(nombre) ? slug : nombre.Trim(),
            Descripcion = (descripcion ?? "").Trim()
        };
        doc.Carpetas.Add(carpeta);
        Guardar(doc);
        Directory.CreateDirectory(Path.Combine(_chatsRoot, slug));
        return carpeta;
    }

    public void AsegurarDirectorio(string carpetaId)
    {
        Directory.CreateDirectory(Path.Combine(_chatsRoot, AppPaths.SanitizeSlug(carpetaId)));
    }

    private void AsegurarCatalogo()
    {
        if (File.Exists(_archivo)) return;
        Guardar(new CatalogoCarpetasDoc
        {
            Carpetas =
            [
                new CarpetaChatDef { Id = "principal", Nombre = "Principal", Descripcion = "Chats generales" }
            ]
        });
        Directory.CreateDirectory(Path.Combine(_chatsRoot, "principal"));
    }

    private CatalogoCarpetasDoc Leer()
    {
        AsegurarCatalogo();
        return JsonSerializer.Deserialize<CatalogoCarpetasDoc>(File.ReadAllText(_archivo), JsonOpts)
               ?? new CatalogoCarpetasDoc();
    }

    private void Guardar(CatalogoCarpetasDoc doc) =>
        File.WriteAllText(_archivo, JsonSerializer.Serialize(doc, JsonOpts));

    private sealed class CatalogoCarpetasDoc
    {
        public List<CarpetaChatDef> Carpetas { get; set; } = [];
    }
}
