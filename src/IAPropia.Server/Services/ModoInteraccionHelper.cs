namespace IAPropia.Services;

public static class ModoInteraccionHelper
{
    public const string Ask = "ask";
    public const string Plan = "plan";
    public const string MultiPlan = "multiplan";
    public const string Agent = "agent";
    public const string Debug = "debug";

    public static string Normalizar(string? modo)
    {
        var m = (modo ?? "").Trim().ToLowerInvariant();
        return m switch
        {
            Plan => Plan,
            MultiPlan or "multi-plan" or "multi_plan" => MultiPlan,
            Agent or "agente" => Agent,
            Debug or "depurar" => Debug,
            _ => Ask
        };
    }

    public static string InstruccionesSistema(string? modo)
    {
        return Normalizar(modo) switch
        {
            Plan => """
Modo **Plan** (como Cursor Plan):
- No simules ejecutar herramientas ni modificar archivos; solo razoná y proponé.
- Respondé con: objetivo breve, supuestos, pasos numerados, riesgos y criterio de “listo”.
- Si falta información, preguntá al final (máx. 3 preguntas concretas).
- Formato preferido: secciones claras en Markdown (## Objetivo, ## Pasos, etc.).
""",
            MultiPlan => """
Modo **Multi-plan** (varias estrategias antes de actuar):
- Ofrecé **2 o 3 opciones** etiquetadas **A**, **B** y opcionalmente **C**.
- Por cada opción: nombre corto, cuándo conviene, pros/contras, esfuerzo relativo (bajo/medio/alto).
- Cerrá pidiendo cuál opción prefiere el usuario (no elijas vos).
- No ejecutes ni des por hecho que se implementó algo.
""",
            Debug => """
Modo **Debug** (diagnóstico y trazas):
- Al inicio de la respuesta incluí un bloque `## Diagnóstico` breve: qué entendiste del pedido, supuestos y posibles puntos de fallo.
- Mostrá razonamiento intermedio en `## Razonamiento` (pasos numerados, sin ocultar dudas).
- Luego la respuesta útil al usuario en `## Respuesta`.
- Sé explícito sobre incertidumbre, qué datos faltarían y cómo comprobar hipótesis (comandos o comprobaciones sugeridas, sin decir que ya los ejecutaste).
- El servidor adjuntará métricas técnicas aparte; no inventes latencias ni nombres de modelo: si no las sabés, omitilas.
""",
            Agent => """
Modo **Agente** (como Cursor Agent, sin acceso real a archivos):
- Sé proactivo: desglosá el trabajo en pasos pequeños verificables y sugerí el **siguiente paso inmediato**.
- Podés usar listas de verificación; marcá qué harías primero, segundo, etc.
- Si el usuario pide algo amplio, proponé un orden de ataque y entregables por fase.
- No inventes comandos ejecutados ni resultados de terminal; si hace falta código, mostralo como propuesta.
- Si no hay herramientas disponibles en esta app, decilo y limitate a guía y borradores.
""",
            _ => """
Modo **Preguntar** (como Cursor Ask):
- Respondé de forma directa y clara a la consulta, sin plan largo salvo que lo pidan.
- Priorizá explicaciones y respuestas breves; podés usar listas cortas si ayudan.
- No propongas implementación multietapa salvo que el usuario lo pida explícitamente.
"""
        };
    }
}
