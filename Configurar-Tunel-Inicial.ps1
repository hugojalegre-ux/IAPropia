# Configuración inicial del túnel Cloudflare (interactivo, una vez).
param(
    [string]$Hostname = '',
    [string]$TunnelName = 'ia-propia',
    [int]$PuertoLocal = 5077
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
& (Join-Path $root 'Preparar-Cloudflared.ps1') | Out-Null

$cloudflared = (Get-Command cloudflared).Source
$cfDir = Join-Path $env:USERPROFILE '.cloudflared'
$configPath = Join-Path $cfDir 'config.yml'
$tunelLocal = Join-Path $root 'config\tunel.local.json'

New-Item -ItemType Directory -Force -Path (Join-Path $root 'config') | Out-Null
New-Item -ItemType Directory -Force -Path $cfDir | Out-Null

if (-not $Hostname) {
    $Hostname = Read-Host "Hostname publico (sin https), ej. ia.midominio.com"
}
$Hostname = $Hostname.Trim().ToLower() -replace '^https?://', ''

Write-Host ""
Write-Host "1) Se abrira el navegador para: cloudflared tunnel login"
$null = Read-Host "Enter para continuar"
& $cloudflared tunnel login

Write-Host ""
Write-Host "2) Creando túnel '$TunnelName'..."
$createOut = & $cloudflared tunnel create $TunnelName 2>&1 | Out-String
Write-Host $createOut

$tunnelId = ''
if ($createOut -match '([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})') {
    $tunnelId = $Matches[1]
}

$credPattern = Join-Path $cfDir '*.json'
$creds = Get-ChildItem -Path $credPattern -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
if (-not $creds -and $tunnelId) {
    $maybe = Join-Path $cfDir "$tunnelId.json"
    if (Test-Path $maybe) { $creds = Get-Item $maybe }
}

if (-not $creds) {
    Write-Warning "No se detecto archivo de credenciales en $cfDir. Revisá cloudflared tunnel create."
} else {
    $credPath = $creds.FullName
    $tunnelRef = if ($tunnelId) { $tunnelId } else { $TunnelName }
    $yml = @"
tunnel: $tunnelRef
credentials-file: $credPath

ingress:
  - hostname: $Hostname
    service: http://127.0.0.1:$PuertoLocal
  - service: http_status:404
"@
    Set-Content -Path $configPath -Value $yml -Encoding UTF8
    Write-Host "Config escrita: $configPath"
}

Write-Host ""
Write-Host "3) DNS: cloudflared tunnel route dns $TunnelName $Hostname"
& $cloudflared tunnel route dns $TunnelName $Hostname

$tunelObj = @{
    hostname     = $Hostname
    tunnelName   = $TunnelName
    localUrl     = "http://127.0.0.1:$PuertoLocal"
    configuradoUtc = (Get-Date).ToUniversalTime().ToString('o')
}
$tunelObj | ConvertTo-Json | Set-Content -Path $tunelLocal -Encoding UTF8
Write-Host "Guardado: $tunelLocal"

Write-Host ""
Write-Host "Listo. Configurá Cloudflare Access para https://$Hostname (ver docs\TUNEL-CLOUDFLARE.md)"
Write-Host "Uso diario: Iniciar-IAPropia.bat luego .\Iniciar-Tunel-IAPropia.ps1"
