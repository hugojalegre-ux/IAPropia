# IA propia

Proyecto **autónomo**: asistente personal con UI web, historial local y conocimiento en archivos. **No depende de AutomatizacionSOT ni de SOT Accusys**.

**GitHub:** [github.com/hugojalegre-ux/IAPropia](https://github.com/hugojalegre-ux/IAPropia) (cuenta [hugojalegre-ux](https://github.com/hugojalegre-ux)).

## Git — solo IA propia

Este repositorio **contiene únicamente** la carpeta/proyecto **IAPropia**. No incluye RunnerIA, AutomatizacionSOT, SOT Accusys ni otros repos.

| Dónde trabajás | Git que usás | Remoto |
|----------------|--------------|--------|
| `…/IAPropia/` | `IAPropia/.git` | `hugojalegre-ux/IAPropia` |
| `…/RunnerIA/` (resto) | `RunnerIA/.git` | `HugoAlegre/RunnerIA` (IAPropia está en `.gitignore`) |

Commits y `git push` de IA propia: siempre desde `C:\Repositorio\RunnerIA\IAPropia`. Para otros proyectos, volvé a la cuenta que corresponda (`gh auth switch -u HugoAlegre` si empujás RunnerIA).

## Acceso desde celular / Internet (túnel seguro)

HTTPS con dominio propio (Cloudflare Tunnel + Access). **Sin administrador:** `cloudflared` portable en `tools/cloudflared/`. **Guía:** [`docs/TUNEL-CLOUDFLARE.md`](docs/TUNEL-CLOUDFLARE.md).

| Paso | Script |
|------|--------|
| Instalar `cloudflared` | `.\Preparar-Cloudflared.ps1` |
| Configuración inicial (una vez) | `.\Configurar-Tunel-Inicial.ps1` |
| Local (siempre primero) | `Iniciar-IAPropia.bat` → http://127.0.0.1:5077 |
| Túnel diario | `.\Iniciar-Tunel-IAPropia.ps1` o `Iniciar-Tunel-IAPropia.bat` |
| Verificar | `.\Verificar-Tunel-IAPropia.ps1` |

URL pública: la definís vos (ej. `https://ia.tudominio.com`) tras dominio + Access en Cloudflare.

## Seguridad (local, LAN y túnel)

Guía completa: [`docs/SEGURIDAD.md`](docs/SEGURIDAD.md).

| Escenario | Mínimo recomendado |
|-----------|-------------------|
| Solo PC | `EnlaceSoloLocalhost: true` (default) |
| LAN | `appsettings.Lan.json` + API key en secrets |
| Túnel | Cloudflare Access + `appsettings.Tunel.json` + validación JWT + API key |

```powershell
.\Verificar-Seguridad-IAPropia.ps1
```

## Arranque recomendado (único camino)

1. Doble clic en **`Iniciar-IAPropia.bat`** en la raíz del proyecto (o `.\Iniciar-IAPropia.ps1`).
2. Si falta el portable o cambió el código, **publica solo** en el primer arranque (`Publicar-Portable.ps1` automático).
3. Se abre **http://127.0.0.1:5077** — el pie izquierdo muestra el estado de **Ollama** antes de chatear.

Parámetros útiles:

```powershell
.\Iniciar-IAPropia.ps1 -SinAbrirNavegador
.\Iniciar-IAPropia.ps1 -Puerto 5080
.\Iniciar-IAPropia.ps1 -SinPublicar   # avanzado: no republish aunque falte dist
```

Manual de publicación:

```powershell
.\Publicar-Portable.ps1        # dist\IAPropia-win-x64\
.\Publicar-Portable.ps1 -Zip   # + ZIP en dist\
```

## Ollama (proveedor por defecto)

### Instalación portable sin administrador (recomendada)

Ya instalado en esta máquina vía:

```powershell
winget install Ollama.Ollama.Portable --accept-package-agreements --location "$env:LOCALAPPDATA\OllamaPortable"
```

Ruta típica: `%LOCALAPPDATA%\OllamaPortable\ollama.exe`. No hace falta ejecutar `OllamaSetup.exe` de Descargas si usás el portable.

### Preparación automática

`Iniciar-IAPropia.bat` llama a `Preparar-Ollama.ps1`: levanta `ollama serve` si no hay puerto **11434** y, con `-AutoPull`, descarga el modelo de `appsettings.json` si falta.

```powershell
.\Preparar-Ollama.ps1 -AutoPull
.\Iniciar-IAPropia.ps1 -SinOllama    # solo IA propia, sin tocar Ollama
```

### Manual

1. Instalá [Ollama](https://ollama.com) o el portable de arriba.
2. En terminal: `ollama serve` (si no arranca solo).
3. Descargá el modelo de `appsettings.json` (por defecto `llama3.2`):

   ```powershell
   ollama pull llama3.2
   ```

4. En la UI: el selector **Modelo Ollama** se llena con `GET /api/tags`. Si el pie está en naranja, seguí el mensaje (serve / pull).

La UI **bloquea Enviar** si Ollama no está OK (salvo «Enviar sin preflight»).

## Modelos pesados (timeout, streaming, contexto)

Para modelos grandes, RAG + aprendizajes o respuestas largas:

| Concepto | Valor por defecto (`Limites` en `appsettings.json`) |
|----------|-----------------------------------------------------|
| Chat normal | `TimeoutSegundos` **300** (5 min) |
| **Modo pesado** (checkbox en UI) | `TimeoutSegundosPesado` **1200** (20 min) |
| Tope HttpClient + Kestrel | `TimeoutMaximo` **1800** (30 min) |
| Búsqueda web | `TimeoutBusquedaWebSegundos` **45** (cliente aparte) |

- La UI usa **`POST /api/chat/stream`** (SSE): ves los tokens en vivo mientras Ollama genera. Si el stream falla, hace **fallback** a `POST /api/chat`.
- Con **Respuestas largas / modelo pesado** activo se envían a Ollama `options.num_ctx` / `num_predict` según `IAPropia.Ollama` (por defecto 8192 / 4096) y `keep_alive` (ej. `10m`).
- Ajustes en `src/IAPropia.Server/appsettings.json` o en el portable `dist/IAPropia-win-x64/appsettings.json` tras publicar.
- Si sigue lento o con OOM: `ollama ps` y RAM del modelo; probá un modelo más chico o bajá `NumCtx`.

## Internet (solo búsqueda, sin API de chat de pago)

- **`ModoSoloLocal: true`** (por defecto): el LLM es **siempre Ollama**.
- En la UI: **Buscar en internet (DuckDuckGo)** agrega fragmentos de contexto antes de responder (no reemplaza al modelo local).
- Config: `InternetBusqueda.Habilitada` y `MaxFragmentos` en `appsettings.json`.

## Organización de chats

| Ruta | Contenido |
|------|-----------|
| `data/chats/{carpeta}/` | Un JSON por conversación |
| `data/catalogo-carpetas.json` | Carpetas (supervisión por tema) |
| `data/aprendizajes/` | Notas reutilizables entre chats |
| `data/conversaciones/` | Legacy (migración automática a `principal`) |

En la UI: **carpeta**, **título**, **chats relacionados** (lista con casillas; opción «Todas las carpetas»; IDs manual en avanzado), panel **Aprendizajes** por carpeta (ver / eliminar), **Guardar aprendizaje**. Los aprendizajes y chats enlazados se inyectan en el contexto del modelo local cuando coinciden palabras o relaciones.

## Qué incluye

| Pieza | Descripción |
|-------|-------------|
| **API + UI** | `IAPropia.Server` en puerto **5077** |
| **Health** | `GET /api/health`, `/api/health/ollama`, resumen en `GET /api/estado` |
| **Versión** | [`version.json`](version.json) → campo `version` en `/api/estado` |
| **Personas** | `config/personas/*.json` |
| **Memoria** | `data/chats/` + aprendizajes |
| **Logs** | `data/logs/ia-propia-YYYYMMDD.log` (arranque y errores; sin prompts) |
| **Conocimiento** | `conocimiento/` (RAG simple por palabras) |

## Configuración

- `src/IAPropia.Server/appsettings.json` — puerto, Ollama (`NumCtx`, `NumPredict`, `KeepAlive`), `ModoSoloLocal`, internet, carpetas, `Limites` (timeouts arriba).

## API (resumen)

| Método | Ruta | Uso |
|--------|------|-----|
| GET | `/api/health` | Servidor vivo + versión |
| GET | `/api/health/ollama` | Preflight Ollama + lista de modelos |
| GET | `/api/estado` | Personas, carpetas, rutas, Ollama |
| GET/POST | `/api/carpetas` | Listar / crear carpeta de chat |
| GET | `/api/conversaciones?carpetaId=` | Historial por carpeta |
| PATCH | `/api/conversaciones/{id}` | Título, carpeta, chats relacionados |
| GET/POST | `/api/aprendizajes` | Listar / guardar aprendizaje |
| GET/DELETE | `/api/aprendizajes/{id}` | Ver / eliminar aprendizaje |
| POST | `/api/chat` | Mensaje al LLM (respuesta JSON completa) |
| POST | `/api/chat/stream` | Mismo chat en SSE (`event: token` / `done` / `error`) |

## Modo desarrollo (secundario)

Requiere runtime ASP.NET alineado al SDK. En esta máquina suele fallar `dotnet run` por versiones 8.0.29 / 9.0.20 vs instaladas — **usá el portable**.

```powershell
cd src\IAPropia.Server
dotnet watch run   # solo si tu runtime coincide con el SDK
```

## Checklist QA (sprint estabilidad)

1. **Ollama apagado** → pie en advertencia → **Enviar** deshabilitado (sin “forzar”).
2. **`ollama serve` + `ollama pull llama3.2`** → pie verde → mensaje de prueba OK.
3. **Borrar `dist/`** → `Iniciar-IAPropia.bat` republish y abre UI.
4. **Nota en `conocimiento/`** → pregunta relacionada → `fragmentosConocimiento` > 0 en respuesta JSON del chat.

## Relación con RunnerIA

Convive en `RunnerIA/IAPropia` sin código SOT. RunnerIA documenta un enlace en su README; no hay endpoints cruzados.

Versión actual: ver `version.json` (ej. **1.1.1**).
