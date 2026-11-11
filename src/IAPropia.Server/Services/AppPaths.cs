namespace IAPropia.Services;

public static class AppPaths
{
    public static string ResolverRaizProyecto(IWebHostEnvironment env)
    {
        var dir = env.ContentRootPath;
        for (var i = 0; i < 4 && !string.IsNullOrEmpty(dir); i++)
        {
            if (Directory.Exists(Path.Combine(dir, "config", "personas")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName ?? "";
        }

        return Directory.GetParent(env.ContentRootPath)?.FullName
               ?? env.ContentRootPath;
    }

    public static string CarpetaDatos(string raiz) => Path.Combine(raiz, "data");

    public static string CarpetaChats(string raiz) => Path.Combine(raiz, "data", "chats");

    public static string CarpetaConversacion(string raiz, string carpetaId) =>
        Path.Combine(CarpetaChats(raiz), SanitizeSlug(carpetaId));

    public static string CarpetaConversacionesLegacy(string raiz) =>
        Path.Combine(raiz, "data", "conversaciones");

    public static string CarpetaAprendizajes(string raiz) => Path.Combine(raiz, "data", "aprendizajes");

    public static string CarpetaPersonas(string raiz) => Path.Combine(raiz, "config", "personas");

    public static string CarpetaConocimiento(string raiz, string rel) =>
        Path.IsPathRooted(rel) ? rel : Path.Combine(raiz, rel);

    public static string ArchivoCatalogoCarpetas(string raiz) =>
        Path.Combine(raiz, "data", "catalogo-carpetas.json");

    public static string SanitizeSlug(string id)
    {
        var s = (id ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(s)) return "principal";
        foreach (var c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '-');
        return s;
    }
}
