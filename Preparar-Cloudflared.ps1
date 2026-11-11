# cloudflared portable en tools/cloudflared/ — sin administrador (no usa winget/MSI).
param(
    [switch]$SoloComprobar,
    [switch]$UsarWinget
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
. (Join-Path $root 'Lib-Cloudflared.ps1')

$exe = Resolve-IAPropiaCloudflaredExe -Root $root -InstalarSiFalta:(-not $SoloComprobar -and -not $UsarWinget)

if (-not $exe -and $UsarWinget -and -not $SoloComprobar) {
    Write-Host "Intentando winget (puede pedir administrador)..."
    winget install Cloudflare.cloudflared --accept-package-agreements --accept-source-agreements
    $exe = Resolve-IAPropiaCloudflaredExe -Root $root
}

if (-not $exe) {
    if ($SoloComprobar) {
        Write-Host "cloudflared no instalado. Ejecutá: .\Preparar-Cloudflared.ps1"
        exit 1
    }
    Write-Error "cloudflared no encontrado tras intento de instalación."
}

& $exe --version
Write-Host "cloudflared: $exe"
if ($SoloComprobar) { exit 0 }

Write-Host ""
Write-Host "Config del túnel (local, no Git): config\cloudflared.local\"
Write-Host "Siguiente (una vez): .\Configurar-Tunel-Inicial.ps1"
Write-Host "Guia: docs\TUNEL-CLOUDFLARE.md"
