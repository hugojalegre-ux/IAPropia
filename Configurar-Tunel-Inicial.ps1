# Configuración inicial del túnel Cloudflare (interactivo, una vez). Sin administrador.
param(
    [string]$Hostname = '',
    [string]$TunnelName = 'ia-propia',
    [int]$PuertoLocal = 5077
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
. (Join-Path $root 'Lib-Cloudflared.ps1')

& (Join-Path $root 'Preparar-Cloudflared.ps1') | Out-Null
$cloudflared = Resolve-IAPropiaCloudflaredExe -Root $root
$cfDir = Get-IAPropiaTunnelConfigDir -Root $root
$configPath = Get-IAPropiaTunnelConfigPath -Root $root
$certPath = Get-IAPropiaOriginCertPath -Root $root
$tunelLocal = Join-Path $root 'config\tunel.local.json'
$credFile = Join-Path $cfDir "$TunnelName.credentials.json"

New-Item -ItemType Directory -Force -Path (Join-Path $root 'config') | Out-Null
New-Item -ItemType Directory -Force -Path $cfDir | Out-Null

if (-not $Hostname) {
    $Hostname = Read-Host "Hostname publico (sin https), ej. ia.midominio.com"
}
$Hostname = $Hostname.Trim().ToLower() -replace '^https?://', ''

Write-Host ""
Write-Host "1) Login Cloudflare (cert en config\cloudflared.local\cert.pem)"
$null = Read-Host "Enter para abrir el navegador"
& $cloudflared tunnel login --origincert $certPath

Write-Host ""
Write-Host "2) Creando túnel '$TunnelName'..."
$credArgs = @()
if (Test-Path $credFile) { Write-Host "Ya existe $credFile — se reutiliza." }
else { $credArgs = @('--credentials-file', $credFile) }

$createOut = & $cloudflared tunnel --origincert $certPath create @credArgs $TunnelName 2>&1 | Out-String
Write-Host $createOut

$tunnelId = ''
if ($createOut -match '([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})') {
    $tunnelId = $Matches[1]
}

$credPath = $credFile
if (-not (Test-Path $credPath)) {
    $found = Get-ChildItem -Path $cfDir -Filter '*.json' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($found) { $credPath = $found.FullName }
}

if (-not (Test-Path $credPath)) {
    Write-Warning "No se detectaron credenciales en $cfDir. Revisá el paso create."
} else {
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
& $cloudflared --origincert $certPath tunnel route dns $TunnelName $Hostname

@{
    hostname       = $Hostname
    tunnelName     = $TunnelName
    localUrl       = "http://127.0.0.1:$PuertoLocal"
    configDir      = $cfDir
    configuradoUtc = (Get-Date).ToUniversalTime().ToString('o')
} | ConvertTo-Json | Set-Content -Path $tunelLocal -Encoding UTF8
Write-Host "Guardado: $tunelLocal (gitignored)"

Write-Host ""
Write-Host "Listo. Cloudflare Access -> https://$Hostname (docs\TUNEL-CLOUDFLARE.md)"
Write-Host "Uso: Iniciar-IAPropia.bat + .\Iniciar-Tunel-IAPropia.ps1"
