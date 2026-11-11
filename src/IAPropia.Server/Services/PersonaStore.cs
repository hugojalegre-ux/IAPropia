using System.Text.Json;
using System.Text.Json.Serialization;
using IAPropia.Models;

namespace IAPropia.Services;

public sealed class PersonaStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _personasDir;
    private List<PersonaDef>? _cache;

    public PersonaStore(IWebHostEnvironment env)
    {
        var raiz = ResolverRaizProyecto(env);
        _personasDir = AppPaths.CarpetaPersonas(raiz);
        Directory.CreateDirectory(_personasDir);
    }

    private static string ResolverRaizProyecto(IWebHostEnvironment env)
    {
        var fromContent = AppPaths.ResolverRaizProyecto(env);
        if (Directory.Exists(Path.Combine(fromContent, "config", "personas")))
            return fromContent;
        var sibling = Path.GetFullPath(Path.Combine(env.ContentRootPath, "..", "..", ".."));
        if (Directory.Exists(Path.Combine(sibling, "config", "personas")))
            return sibling;
        return fromContent;
    }

    public IReadOnlyList<PersonaDef> Listar()
    {
        _cache ??= CargarTodas();
        return _cache;
    }

    public PersonaDef? Obtener(string? id)
    {
        var key = (id ?? "").Trim();
        if (string.IsNullOrWhiteSpace(key)) key = "asistente";
        return Listar().FirstOrDefault(p =>
            string.Equals(p.Id, key, StringComparison.OrdinalIgnoreCase));
    }

    public PersonaDef Guardar(PersonaDef persona)
    {
        if (string.IsNullOrWhiteSpace(persona.Id))
            persona.Id = "persona-" + Guid.NewGuid().ToString("N")[..8];
        persona.Id = SanitizeId(persona.Id);
        var path = Path.Combine(_personasDir, persona.Id + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(persona, JsonOpts));
        _cache = null;
        return persona;
    }

    private List<PersonaDef> CargarTodas()
    {
        var list = new List<PersonaDef>();
        foreach (var file in Directory.EnumerateFiles(_personasDir, "*.json"))
        {
            try
            {
                var p = JsonSerializer.Deserialize<PersonaDef>(File.ReadAllText(file), JsonOpts);
                if (p != null && !string.IsNullOrWhiteSpace(p.Id))
                    list.Add(p);
            }
            catch
            {
                // ignorar JSON corrupto
            }
        }

        return list.OrderBy(p => p.Nombre).ToList();
    }

    private static string SanitizeId(string id)
    {
        var s = id.Trim().ToLowerInvariant();
        foreach (var c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '-');
        return s;
    }
}
