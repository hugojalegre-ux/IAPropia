using System.Text.RegularExpressions;

namespace IAPropia.Services;

/// <summary>Deriva consultas cortas para buscadores a partir del mensaje del usuario.</summary>
internal static class WebSearchQueryHelper
{
    private static readonly Regex CodeFence = new(@"```[\s\S]*?```", RegexOptions.Compiled);
    private static readonly Regex Multiline = new(@"\s+", RegexOptions.Compiled);

    public static IReadOnlyList<string> GenerarConsultas(string mensaje, int maxCaracteres, int maxVariantes = 2)
    {
        var limpio = LimpiarMensaje(mensaje);
        if (string.IsNullOrWhiteSpace(limpio))
            return Array.Empty<string>();

        var variantes = new List<string>();
        var pregunta = ExtraerPregunta(limpio);
        if (!string.IsNullOrWhiteSpace(pregunta))
            variantes.Add(Recortar(pregunta, maxCaracteres));

        variantes.Add(Recortar(limpio, maxCaracteres));

        var claves = ExtraerFraseClave(limpio, maxCaracteres);
        if (!string.IsNullOrWhiteSpace(claves))
            variantes.Add(claves);

        return variantes
            .Select(v => v.Trim())
            .Where(v => v.Length >= 8)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(maxVariantes, 1, 4))
            .ToList();
    }

    private static string LimpiarMensaje(string mensaje)
    {
        var t = mensaje.Trim();
        t = CodeFence.Replace(t, " ");
        t = t.Replace('\r', ' ').Replace('\n', ' ');
        t = Multiline.Replace(t, " ");
        return t.Trim();
    }

    private static string ExtraerPregunta(string texto)
    {
        var idx = texto.LastIndexOf('?');
        if (idx < 0) return "";

        var start = texto.LastIndexOf('.', idx);
        if (start < 0) start = texto.LastIndexOf('!', idx);
        if (start < 0) start = 0;
        else start++;

        var chunk = texto[start..(idx + 1)].Trim();
        return chunk.Length >= 12 ? chunk : texto.Contains('?') ? texto : "";
    }

    private static string ExtraerFraseClave(string texto, int max)
    {
        var words = texto
            .ToLowerInvariant()
            .Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3 && !EsStopword(w))
            .Take(8)
            .ToList();

        if (words.Count < 2) return "";
        return Recortar(string.Join(' ', words), max);
    }

    private static string Recortar(string t, int max)
    {
        max = Math.Clamp(max, 40, 300);
        if (t.Length <= max) return t;
        var corte = t.LastIndexOf(' ', max - 1);
        return corte >= 30 ? t[..corte] : t[..max];
    }

    private static bool EsStopword(string w) =>
        w is "para" or "como" or "qué" or "que" or "con" or "por" or "los" or "las" or "una" or "uno" or "del"
            or "the" or "and" or "esto" or "esta" or "ese" or "esa" or "pero" or "solo" or "más" or "mas" or "hace"
            or "hacer" or "need" or "quiero" or "podes" or "podés" or "puede" or "debe" or "debería" or "deberia";
}
