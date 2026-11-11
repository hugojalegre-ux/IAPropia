using System.Text.Json;

namespace IAPropia.Services;

public sealed class VersionInfo
{
    private readonly string _versionPath;
    private string? _cached;

    public VersionInfo(IWebHostEnvironment env)
    {
        var raiz = AppPaths.ResolverRaizProyecto(env);
        _versionPath = Path.Combine(raiz, "version.json");
        if (!File.Exists(_versionPath))
            _versionPath = Path.Combine(env.ContentRootPath, "version.json");
    }

    public string Version => LeerVersion();

    private string LeerVersion()
    {
        if (_cached != null) return _cached;
        try
        {
            if (!File.Exists(_versionPath)) return "1.0.0";
            using var doc = JsonDocument.Parse(File.ReadAllText(_versionPath));
            _cached = doc.RootElement.TryGetProperty("version", out var v)
                ? v.GetString() ?? "1.0.0"
                : "1.0.0";
            return _cached;
        }
        catch
        {
            return "1.0.0";
        }
    }
}
