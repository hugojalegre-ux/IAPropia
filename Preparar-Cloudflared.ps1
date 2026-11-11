# Instala cloudflared (winget) si falta. No configura el túnel.
param([switch]$SoloComprobar)

$ErrorActionPreference = 'Stop'

function Get-CloudflaredExe {
    $cmd = Get-Command cloudflared -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $local = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Links\cloudflared.exe'
    if (Test-Path $local) { return $local }
    $prog = Join-Path $env:ProgramFiles 'cloudflared\cloudflared.exe'
    if (Test-Path $prog) { return $prog }
    return $null
}

$exe = Get-CloudflaredExe
if (-not $exe -and -not $SoloComprobar) {
    Write-Host "Instalando Cloudflare.cloudflared con winget..."
    winget install Cloudflare.cloudflared --accept-package-agreements --accept-source-agreements
    $exe = Get-CloudflaredExe
}

if (-not $exe) {
    Write-Error "cloudflared no encontrado. Instalalo manualmente: https://developers.cloudflare.com/cloudflare-one/connections/connect-apps/install-and-setup/installation/"
}

& $exe --version
Write-Host "cloudflared: $exe"
if ($SoloComprobar) { exit 0 }

Write-Host ""
Write-Host "Siguiente (una vez): .\Configurar-Tunel-Inicial.ps1"
Write-Host "Guia: docs\TUNEL-CLOUDFLARE.md"
