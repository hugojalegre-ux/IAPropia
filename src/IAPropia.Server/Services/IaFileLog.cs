namespace IAPropia.Services;

/// <summary>Log ligero local (errores + eventos de arranque). No vuelca prompts.</summary>
public sealed class IaFileLog
{
    private readonly string _logDir;
    private readonly object _lock = new();

    public IaFileLog(IWebHostEnvironment env)
    {
        var raiz = AppPaths.ResolverRaizProyecto(env);
        _logDir = Path.Combine(raiz, "data", "logs");
        Directory.CreateDirectory(_logDir);
    }

    public void LogStartup(string mensaje) => Escribir("INFO", mensaje);

    public void LogError(string mensaje) => Escribir("ERROR", mensaje);

    private void Escribir(string nivel, string mensaje)
    {
        try
        {
            var line = $"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ} [{nivel}] {mensaje}";
            var path = Path.Combine(_logDir, $"ia-propia-{DateTime.UtcNow:yyyyMMdd}.log");
            lock (_lock)
            {
                File.AppendAllText(path, line + Environment.NewLine);
            }
        }
        catch
        {
            // best-effort
        }
    }
}
