# Túnel seguro — IA propia en web y celular

Acceso **HTTPS** con **dominio propio** vía [Cloudflare Tunnel](https://developers.cloudflare.com/cloudflare-one/connections/connect-apps/). La app sigue en tu PC (`127.0.0.1:5077`); **Ollama no se expone** (solo `11434` local).

## Resumen

| Componente | Qué hace |
|------------|----------|
| Dominio en Cloudflare | `https://ia.tudominio.com` |
| `cloudflared` | Túnel saliente → no abrís puertos en el router |
| Cloudflare Access | Login (email/Google) **antes** de ver la UI |
| IA propia + Ollama | Solo en tu máquina |

## Checklist dominio y Cloudflare

Marca cada ítem cuando lo completes (primer uso):

- [ ] **Dominio** registrado (cualquier registrador) o uno que ya tengas.
- [ ] Sitio agregado en [dash.cloudflare.com](https://dash.cloudflare.com) → **Add a site**.
- [ ] **Nameservers** del dominio apuntando a los que indica Cloudflare (propagación: minutos a 48 h).
- [ ] Estado del sitio **Active** en Cloudflare.
- [ ] Subdominio elegido, ej. `ia.tudominio.com` (solo IA propia; no mezclar con otras apps sin planear Access).

Anotá acá tu hostname (copialo a `config/tunel.local.json`):

```
Hostname HTTPS: https://ia.________________.com
```

## Checklist Cloudflare Access (obligatorio)

Sin Access **no** publiques el túnel: la app no tiene login propio.

1. [Cloudflare Zero Trust](https://one.dash.cloudflare.com/) → plan **Free** si aplica.
2. **Access** → **Applications** → **Add an application** → **Self-hosted**.
3. **Application domain**: `ia.tudominio.com` (mismo hostname que el túnel).
4. **Policies** → Allow → **Include** → tu email (`hugo@...`).
5. **Identity providers**: activá **One-time PIN** y/o **Google** (como prefieras).
6. Guardá y probá que al abrir la URL pida login **antes** de la UI de IA propia.

- [ ] Application creada con el hostname correcto
- [ ] Política solo con tu(s) email(s)
- [ ] Login probado en navegador de incógnito

## Sin administrador + Git

| En Git (subís / bajás con clone) | Solo en tu PC (no Git) |
|----------------------------------|-------------------------|
| Scripts `.ps1`, `docs/`, plantillas `config/*.example` | `tools/cloudflared/cloudflared.exe` (descarga) |
| `Lib-Cloudflared.ps1` | `config/cloudflared.local/` (cert, config.yml, credenciales) |
| `config/tunel.local.json.example` | `config/tunel.local.json` |
| App `dist/`, `data/` (como siempre) | |

En otra máquina: `git pull` → `.\Preparar-Cloudflared.ps1` (baja el exe) → si ya tenías túnel, copiás `cloudflared.local` o volvés a `Configurar-Tunel-Inicial.ps1`.

Repo: https://github.com/hugojalegre-ux/IAPropia

## Instalar cloudflared (portable, sin admin)

```powershell
cd C:\Repositorio\RunnerIA\IAPropia
.\Preparar-Cloudflared.ps1
```

Descarga `tools\cloudflared\cloudflared.exe` desde GitHub (sin winget ni MSI).

Opcional con admin: `.\Preparar-Cloudflared.ps1 -UsarWinget`

## Crear túnel (una vez)

```powershell
cd C:\Repositorio\RunnerIA\IAPropia
.\Configurar-Tunel-Inicial.ps1
```

Eso guía:

1. `cloudflared tunnel login` (navegador)
2. `cloudflared tunnel create ia-propia`
3. Copia de plantilla → `%USERPROFILE%\.cloudflared\config.yml`
4. `cloudflared tunnel route dns ia-propia ia.tudominio.com`

Plantilla: [`config/cloudflared.config.example.yml`](../config/cloudflared.config.example.yml).

La config vive en **`config/cloudflared.local/`** (gitignored). No subas credenciales ni `cert.pem`.

## Uso diario

Orden:

1. Ollama + IA propia: `Iniciar-IAPropia.bat` (debe abrir [http://127.0.0.1:5077](http://127.0.0.1:5077)).
2. Túnel: `.\Iniciar-Tunel-IAPropia.ps1`
3. Celular o 4G: `https://ia.tudominio.com` → Access → misma UI.

Verificación local:

```powershell
.\Verificar-Tunel-IAPropia.ps1
```

## Prueba desde celular (4G)

- [ ] IA propia y túnel **corriendo** en la PC de casa/oficina.
- [ ] Celular con **datos móviles** (no solo Wi‑Fi de la misma red).
- [ ] Abrís `https://ia.tudominio.com`.
- [ ] Aparece **Cloudflare Access** → iniciás sesión.
- [ ] Carga la UI, pie verde de Ollama (si el modelo está listo).
- [ ] Enviás un mensaje de prueba.

Si falla: revisá que la PC no esté suspendida, que `Iniciar-Tunel` siga en ejecución y que el hostname en Access coincida con el DNS del túnel.

## Qué no exponer

- Puerto **11434** (Ollama) al túnel o al router.
- Carpetas `data/` ni `appsettings.secrets.json`.
- Archivos `credentials-file` de Cloudflare en GitHub.

## Enlaces útiles

| Recurso | URL |
|---------|-----|
| Dashboard Cloudflare | https://dash.cloudflare.com |
| Zero Trust / Access | https://one.dash.cloudflare.com |
| Repo IA propia | https://github.com/hugojalegre-ux/IAPropia |
| Local (PC) | http://127.0.0.1:5077 |

## Servicio Windows (opcional)

Para que el túnel arranque con Windows (después de validar manualmente):

```powershell
cloudflared service install
cloudflared tunnel run ia-propia
```

Consultá la documentación oficial si usás `service install` con `config.yml` en `%USERPROFILE%\.cloudflared\`.
