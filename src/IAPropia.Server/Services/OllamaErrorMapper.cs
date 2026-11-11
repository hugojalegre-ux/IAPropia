using System.Net;

namespace IAPropia.Services;

public static class OllamaErrorMapper
{
    public static string DesdeExcepcion(Exception ex, string modelo, int? statusCode, string? cuerpo)
    {
        if (ex is OperationCanceledException)
            return "Ollama tardó demasiado en responder. Probá un modelo más liviano o aumentá Limites.TimeoutSegundos en appsettings.json.";

        if (ex is HttpRequestException)
            return "No se pudo conectar con Ollama. ¿Está corriendo? En otra terminal: ollama serve";

        if (statusCode == (int)HttpStatusCode.NotFound)
            return $"El modelo «{modelo}» no está disponible. Ejecutá: ollama pull {modelo}";

        if (statusCode is 400 or 422)
        {
            var hint = ExtraerErrorJson(cuerpo);
            if (hint.Contains("model", StringComparison.OrdinalIgnoreCase))
                return $"Modelo «{modelo}» rechazado por Ollama. Verificá con: ollama list · luego: ollama pull {modelo}";
            return string.IsNullOrWhiteSpace(hint)
                ? "Ollama rechazó la petición (400). Revisá el modelo seleccionado."
                : hint;
        }

        if (statusCode >= 500)
            return $"Ollama falló internamente (HTTP {statusCode}). Reiniciá ollama serve e intentá de nuevo.";

        if (!string.IsNullOrWhiteSpace(cuerpo) && cuerpo.Length < 300)
            return $"Ollama: {cuerpo.Trim()}";

        return ex.Message;
    }

    private static string ExtraerErrorJson(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        if (raw.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return "Modelo no encontrado en Ollama.";
        return raw.Trim();
    }
}
