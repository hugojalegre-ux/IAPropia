# Inicia cloudflared hacia IA propia en 127.0.0.1:5077 (requiere config.yml).
param(
    [string]$TunnelName = 'ia-propia',
    [int]$PuertoLocal = 5077,
    [switch]$SinComprobarLocal
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
& (Join-Path $root 'Preparar-Cloudflared.ps1') -SoloComprobar | Out-Null

$localUrl = "http://127.0.0.1:$PuertoLocal"

if (-not $SinComprobarLocal) {
    try {
        $h = Invoke-RestMethod -Uri "$localUrl/api/health" -TimeoutSec 3
        Write-Host "IA propia OK local ($($h.version))"
    } catch {
        Write-Error "IA propia no responde en $localUrl. Ejecutá primero Iniciar-IAPropia.bat"
    }
}

$configPath = Join-Path $env:USERPROFILE '.cloudflared\config.yml'
if (-not (Test-Path $configPath)) {
    Write-Error "Falta $configPath. Ejecutá .\Configurar-Tunel-Inicial.ps1"
}

$tunelLocal = Join-Path $root 'config\tunel.local.json'
if (Test-Path $tunelLocal) {
    try {
        $j = Get-Content $tunelLocal -Raw | ConvertFrom-Json
        if ($j.hostname) {
            Write-Host "URL publica esperada: https://$($j.hostname) (tras Cloudflare Access)"
        }
    } catch { }
}

Write-Host "Iniciando túnel '$TunnelName' (Ctrl+C para detener)..."
cloudflared tunnel run $TunnelName
