# Seguridad — IA propia

Asistente **personal** en tu PC. Estas capas aplican según cómo lo uses: solo local, LAN u HTTPS con túnel.

## Capas

| Capa | Qué hace |
|------|----------|
| **127.0.0.1** | `Seguridad.EnlaceSoloLocalhost` evita escuchar en toda la red |
| **API key** | Header `X-IAPropia-Key` (nombre configurable) en casi todas las rutas `/api/*` |
| **Cloudflare Access** | Login en el borde + JWT `Cf-Access-Jwt-Assertion` validado en el servidor |
| **Rate limit** | Tope de chats por minuto por IP en `/api/chat` y `/api/chat/stream` |
| **Cabeceras HTTP** | CSP, `X-Frame-Options`, `nosniff`, etc. |

`GET /api/health` es público (versión y flags de seguridad, sin datos de chats).

## Configuración

### Base — [`appsettings.json`](../src/IAPropia.Server/appsettings.json)

Bloque `IAPropia.Seguridad` (defaults seguros para uso local en casa).

### Secretos — `appsettings.secrets.json`

```json
{
  "IAPropia": {
    "Seguridad": {
      "ApiKey": "GENERAR-SECRETO-LARGO",
      "CloudflareTeamDomain": "tu-equipo.cloudflareaccess.com",
      "AudienciaAccess": "uuid-de-la-application-en-zero-trust"
    }
  }
}
```

Plantilla: [`appsettings.secrets.json.example`](../src/IAPropia.Server/appsettings.secrets.json.example).

### Perfiles (copiar y renombrar, no van a Git)

| Archivo | Uso |
|---------|-----|
| `appsettings.Tunel.json` | Desde [`appsettings.Tunel.json.example`](../src/IAPropia.Server/appsettings.Tunel.json.example) |
| `appsettings.Lan.json` | Desde [`appsettings.Lan.json.example`](../src/IAPropia.Server/appsettings.Lan.json.example) |

En `dist/IAPropia-win-x64/` colocá los mismos JSON junto a `appsettings.json` del portable.

## Escenarios

### Solo local (PC tuya)

- `RequiereApiKey`: opcional (`false` cómodo).
- `ValidarCloudflareAccess`: `false`.
- `EnlaceSoloLocalhost`: `true` (recomendado).

### LAN (otros equipos en la red)

1. Copiá `appsettings.Lan.json.example` → `appsettings.Lan.json`.
2. API key obligatoria en secrets.
3. Firewall Windows: **no** abrir 5077 a Internet; solo red privada si realmente lo necesitás.
4. Ollama `11434`: solo loopback (script opcional `Endurecer-Ollama-Firewall.ps1`).

### Túnel HTTPS (celular / fuera de casa)

1. **[TUNEL-CLOUDFLARE.md](TUNEL-CLOUDFLARE.md)** — Access obligatorio en Cloudflare.
2. `appsettings.Tunel.json` + secrets con `ApiKey`, `CloudflareTeamDomain`, `AudienciaAccess`.
3. En la UI: **Guardar clave** en el navegador (localStorage) para que `fetch` envíe el header.
4. `Iniciar-Tunel-IAPropia.ps1` avisa si faltan flags de seguridad.

## UI y API key

En **Ajustes del modelo** → pegar clave → **Guardar clave en este navegador**.

La clave **no** va en el HTML ni en Git; vive en `appsettings.secrets.json` del servidor y en `localStorage` del cliente.

## Contenido no confiable (RAG / internet)

Notas en `conocimiento/` y resultados DuckDuckGo pueden incluir texto manipulado. El system prompt indica al modelo que **no obedezca instrucciones** embebidas en esos bloques; igual conviene no pegar archivos de origen desconocido.

## Verificación

```powershell
cd C:\Repositorio\RunnerIA\IAPropia
.\Verificar-Seguridad-IAPropia.ps1
```

## Referencias

- Cloudflare Access JWT: https://developers.cloudflare.com/cloudflare-one/identity/authorization-cookie/validating-json/
- Ollama local: no exponer puerto 11434 a la LAN sin necesidad
