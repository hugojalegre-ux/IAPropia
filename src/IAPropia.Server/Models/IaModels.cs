namespace IAPropia.Models;

public sealed class IaPropiaOptions
{
    public const string Section = "IAPropia";

    public string Titulo { get; set; } = "IA propia";
    public int PuertoPreferido { get; set; } = 5077;
    public string ProveedorPorDefecto { get; set; } = "ollama";
    public OllamaOptions Ollama { get; set; } = new();
    public OpenAiCompatibleOptions OpenAiCompatible { get; set; } = new();
    public RagOptions Rag { get; set; } = new();
    public LimitesOptions Limites { get; set; } = new();
    public bool ModoSoloLocal { get; set; } = true;
    public InternetBusquedaOptions InternetBusqueda { get; set; } = new();
    public ChatsOptions Chats { get; set; } = new();
    public SeguridadOptions Seguridad { get; set; } = new();
}

public sealed class SeguridadOptions
{
  public bool EnlaceSoloLocalhost { get; set; } = true;

  public bool RequiereApiKey { get; set; }

  public string? ApiKey { get; set; }

  public string HeaderApiKey { get; set; } = "X-IAPropia-Key";

  public bool ValidarCloudflareAccess { get; set; }

  /// <summary>Ej. tudominio.cloudflareaccess.com (sin https://).</summary>
  public string? CloudflareTeamDomain { get; set; }

  public string? AudienciaAccess { get; set; }

  public bool ExponerRutasEnEstado { get; set; } = true;

  public int LimiteMensajeCaracteres { get; set; } = 32_000;

  public int MaxBodyKb { get; set; } = 512;

  public int RateLimitChatPorMinuto { get; set; } = 20;
}

public sealed class InternetBusquedaOptions
{
    public bool Habilitada { get; set; } = true;
    public int MaxFragmentos { get; set; } = 4;
}

public sealed class ChatsOptions
{
    public string CarpetaPorDefecto { get; set; } = "principal";
}

public sealed class OllamaOptions
{
    public string BaseUrl { get; set; } = "http://127.0.0.1:11434";
    public string Modelo { get; set; } = "llama3.2";
    public int? NumCtx { get; set; }
    public int? NumPredict { get; set; }
    public string? KeepAlive { get; set; } = "10m";
}

public sealed class OpenAiCompatibleOptions
{
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string Modelo { get; set; } = "gpt-4o-mini";
    public string? ApiKey { get; set; }
    public List<string> ModelosSugeridos { get; set; } = ["gpt-4o-mini", "gpt-4o", "gpt-3.5-turbo"];
}

public sealed class RagOptions
{
    public string CarpetaConocimiento { get; set; } = "conocimiento";
    public int MaxFragmentos { get; set; } = 4;
    public int MaxCaracteresPorFragmento { get; set; } = 2000;
}

public sealed class LimitesOptions
{
    public int MaxMensajesHistorial { get; set; } = 40;
    public int TimeoutSegundos { get; set; } = 300;
    public int TimeoutSegundosPesado { get; set; } = 1200;
    public int TimeoutMaximo { get; set; } = 1800;
    public int TimeoutBusquedaWebSegundos { get; set; } = 45;
}

public sealed class PersonaDef
{
    public string Id { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string SystemPrompt { get; set; } = "";
}

public sealed class ChatMessageDto
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = "";
}

public sealed class ChatRequest
{
    public string? ConversacionId { get; set; }
    public string? PersonaId { get; set; }
    public string? Proveedor { get; set; }
    public string? Modelo { get; set; }
    public string? Titulo { get; set; }
    public string? CarpetaId { get; set; }
    public List<string>? RelacionadasIds { get; set; }
    public string Mensaje { get; set; } = "";
    public bool UsarConocimientoLocal { get; set; } = true;
    public bool UsarBusquedaInternet { get; set; }
    public bool ModoPesado { get; set; }
    public bool UsarStreaming { get; set; } = true;
}

public sealed class ChatResponse
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public string ConversacionId { get; set; } = "";
    public string Respuesta { get; set; } = "";
    public string ProveedorUsado { get; set; } = "";
    public string ModeloUsado { get; set; } = "";
    public int FragmentosConocimiento { get; set; }
    public int FragmentosInternet { get; set; }
    public int FragmentosAprendizajes { get; set; }
    public List<string> Avisos { get; set; } = [];
}

public sealed class ConversacionResumen
{
    public string Id { get; set; } = "";
    public string Titulo { get; set; } = "";
    public string CarpetaId { get; set; } = "principal";
    public string PersonaId { get; set; } = "";
    public string CreadaUtc { get; set; } = "";
    public string ActualizadaUtc { get; set; } = "";
    public int CantidadMensajes { get; set; }
    public List<string> RelacionadasIds { get; set; } = [];
}

public sealed class ConversacionDoc
{
    public string Id { get; set; } = "";
    public string Titulo { get; set; } = "";
    public string CarpetaId { get; set; } = "principal";
    public string PersonaId { get; set; } = "asistente";
    public List<ChatMessageDto> Mensajes { get; set; } = [];
    public List<string> RelacionadasIds { get; set; } = [];
    public List<string> AprendizajeIds { get; set; } = [];
    public string CreadaUtc { get; set; } = "";
    public string ActualizadaUtc { get; set; } = "";
}

public sealed class CarpetaChatDef
{
    public string Id { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Descripcion { get; set; } = "";
}

public sealed class AprendizajeDoc
{
    public string Id { get; set; } = "";
    public string Titulo { get; set; } = "";
    public string Contenido { get; set; } = "";
    public string CarpetaId { get; set; } = "principal";
    public List<string> ConversacionOrigenIds { get; set; } = [];
    public List<string> ConversacionRelacionadasIds { get; set; } = [];
    public string CreadoUtc { get; set; } = "";
}

public sealed class ConversacionMetaRequest
{
    public string? Titulo { get; set; }
    public string? CarpetaId { get; set; }
    public List<string>? RelacionadasIds { get; set; }
}

public sealed class AprendizajeRequest
{
    public string? ConversacionId { get; set; }
    public string Titulo { get; set; } = "";
    public string Contenido { get; set; } = "";
    public string? CarpetaId { get; set; }
    public List<string>? RelacionadasIds { get; set; }
}

public sealed class EstadoServicioDto
{
    public string Nombre { get; set; } = "IA propia";
    public string Version { get; set; } = "1.0.0";
    public string Alcance { get; set; } =
        "Asistente personal local. Sin vínculo con SOT Accusys ni RunnerIA obligatorio.";
    public string ProveedorPorDefecto { get; set; } = "";
    public bool OllamaConfigurado { get; set; }
    public bool ModoSoloLocal { get; set; } = true;
    public bool InternetBusquedaHabilitada { get; set; } = true;
    public string CarpetaDatos { get; set; } = "";
    public string CarpetaConocimiento { get; set; } = "";
    public List<PersonaDef> Personas { get; set; } = [];
    public OllamaHealthDto? Ollama { get; set; }
    public List<CarpetaChatDef> Carpetas { get; set; } = [];
}

public sealed class CloudHealthDto
{
    public bool Ok { get; set; }
    public bool ApiKeyPresente { get; set; }
    public string BaseUrl { get; set; } = "";
    public string ModeloConfigurado { get; set; } = "";
    public List<string> ModelosSugeridos { get; set; } = [];
    public bool ConectividadVerificada { get; set; }
    public string Mensaje { get; set; } = "";
    public string RutaSecrets { get; set; } = "appsettings.secrets.json";
}

public sealed class OllamaHealthDto
{
    public bool Ok { get; set; }
    public string BaseUrl { get; set; } = "";
    public string ModeloConfigurado { get; set; } = "";
    public bool ModeloPresente { get; set; }
    public List<string> ModelosDisponibles { get; set; } = [];
    public string Mensaje { get; set; } = "";
}

public sealed class HealthDto
{
    public bool Ok { get; set; } = true;
    public string Version { get; set; } = "";
    public string Mensaje { get; set; } = "Servidor IA propia en ejecución.";
    public bool RequiereApiKey { get; set; }
    public string HeaderApiKey { get; set; } = "X-IAPropia-Key";
    public bool ValidarCloudflareAccess { get; set; }
}
