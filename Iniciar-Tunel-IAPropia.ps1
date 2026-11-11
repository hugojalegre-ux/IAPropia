# Inicia cloudflared hacia IA propia (config en config/cloudflared.local/, sin admin).
param(
    [string]$TunnelName = 'ia-propia',
    [int]$PuertoLocal = 5077,
    [switch]$SinComprobarLocal
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
. (Join-Path $root 'Lib-Cloudflared.ps1')

$cloudflared = Resolve-IAPropiaCloudflaredExe -Root $root -InstalarSiFalta
if (-not $cloudflared) {
    Write-Error "cloudflared no disponible. Ejecutá .\Preparar-Cloudflared.ps1"
}

$localUrl = "http://127.0.0.1:$PuertoLocal"

if (-not $SinComprobarLocal) {
    try {
        $h = Invoke-RestMethod -Uri "$localUrl/api/health" -TimeoutSec 3
        Write-Host "IA propia OK local ($($h.version))"
        if (-not $h.requiereApiKey) {
            Write-Warning "Seguridad: RequiereApiKey=FALSE. Copiá appsettings.Tunel.json.example -> appsettings.Tunel.json y ApiKey en secrets (ver docs\SEGURIDAD.md)."
        }
        if (-not $h.validarCloudflareAccess) {
            Write-Warning "Seguridad: ValidarCloudflareAccess=FALSE. Con túnel público debería estar en true + audiencia en secrets."
        }
    } catch {
        Write-Error "IA propia no responde en $localUrl. Ejecutá primero Iniciar-IAPropia.bat"
    }
}

$configPath = Get-IAPropiaTunnelConfigPath -Root $root
$certPath = Get-IAPropiaOriginCertPath -Root $root
if (-not (Test-Path $configPath)) {
    Write-Error "Falta $configPath. Ejecutá .\Configurar-Tunel-Inicial.ps1"
}

$tunelLocal = Join-Path $root 'config\tunel.local.json'
if (Test-Path $tunelLocal) {
    try {
        $j = Get-Content $tunelLocal -Raw | ConvertFrom-Json
        if ($j.hostname) {
            Write-Host "URL publica esperada: https://$($j.hostname)"
        }
    } catch { }
}

Write-Host "Túnel '$TunnelName' (Ctrl+C para detener)..."
if (Test-Path $certPath) {
    & $cloudflared --origincert $certPath --config $configPath tunnel run $TunnelName
} else {
    & $cloudflared --config $configPath tunnel run $TunnelName
}
