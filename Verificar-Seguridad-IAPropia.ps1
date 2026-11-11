# Comprueba flags de seguridad de IA propia en ejecución y archivos locales.
param(
    [string]$BaseUrl = 'http://127.0.0.1:5077'
)

$ErrorActionPreference = 'Continue'
$root = $PSScriptRoot
$ok = $true

Write-Host "=== Health ($BaseUrl) ==="
try {
    $h = Invoke-RestMethod -Uri "$BaseUrl/api/health" -TimeoutSec 5
    Write-Host "  version: $($h.version)"
    Write-Host "  requiereApiKey: $($h.requiereApiKey)"
    Write-Host "  validarCloudflareAccess: $($h.validarCloudflareAccess)"
    if (-not $h.requiereApiKey) {
        Write-Host "[WARN] API key no exigida (OK solo uso local)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "[ERROR] No responde $BaseUrl/api/health. ¿IA propia levantada?" -ForegroundColor Red
    $ok = $false
}

Write-Host "`n=== Archivos de perfil ==="
$tunelExample = Join-Path $root 'src\IAPropia.Server\appsettings.Tunel.json.example'
$tunelActive = Join-Path $root 'src\IAPropia.Server\appsettings.Tunel.json'
$lanActive = Join-Path $root 'src\IAPropia.Server\appsettings.Lan.json'
$secrets = Join-Path $root 'src\IAPropia.Server\appsettings.secrets.json'
$distSecrets = Join-Path $root 'dist\IAPropia-win-x64\appsettings.secrets.json'

if (Test-Path $tunelActive) { Write-Host "  appsettings.Tunel.json: presente" } else { Write-Host "  appsettings.Tunel.json: (no) — usar example si hay túnel" }
if (Test-Path $lanActive) { Write-Host "  appsettings.Lan.json: presente" }
if (-not (Test-Path $secrets) -and -not (Test-Path $distSecrets)) {
    Write-Host "[WARN] Sin appsettings.secrets.json (API key / Cloudflare)" -ForegroundColor Yellow
}

Write-Host "`n=== Ollama puerto 11434 (listeners) ==="
try {
    $listeners = Get-NetTCPConnection -LocalPort 11434 -State Listen -ErrorAction SilentlyContinue
    foreach ($l in $listeners) {
        $addr = $l.LocalAddress
        Write-Host "  escucha en $addr :11434"
        if ($addr -notin @('127.0.0.1', '::1')) {
            Write-Host "[WARN] Ollama expuesto fuera de loopback" -ForegroundColor Yellow
            $ok = $false
        }
    }
    if (-not $listeners) { Write-Host "  (sin listener 11434 — Ollama apagado?)" }
} catch {
    Write-Host "  (no se pudo inspeccionar conexiones; ejecutar como usuario normal)"
}

Write-Host "`n=== Prueba API sin clave ==="
try {
    Invoke-RestMethod -Uri "$BaseUrl/api/estado" -TimeoutSec 3 | Out-Null
    $h2 = Invoke-RestMethod -Uri "$BaseUrl/api/health" -TimeoutSec 3
    if ($h2.requiereApiKey) {
        Write-Host "[ERROR] /api/estado respondió sin API key pero RequiereApiKey=true" -ForegroundColor Red
        $ok = $false
    } else {
        Write-Host "  /api/estado accesible sin clave (coherente con RequiereApiKey=false)"
    }
} catch {
    Write-Host "  /api/estado bloqueado o error (esperado si hay API key)" -ForegroundColor Green
}

if ($ok) { Write-Host "`nVerificacion finalizada (revisá WARN)." -ForegroundColor Green }
else { Write-Host "`nVerificacion con problemas." -ForegroundColor Red; exit 1 }
