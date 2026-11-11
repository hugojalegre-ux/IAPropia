# Comprueba IA propia local y (opcional) URL publica en tunel.local.json
param([int]$Puerto = 5077)

$ErrorActionPreference = 'Continue'
$root = $PSScriptRoot
$localUrl = "http://127.0.0.1:$Puerto"

Write-Host "=== Local ==="
try {
    $h = Invoke-RestMethod -Uri "$localUrl/api/health" -TimeoutSec 5
    Write-Host "[OK] $localUrl version=$($h.version)"
} catch {
    Write-Host "[FAIL] $localUrl - $_"
}

$tunelLocal = Join-Path $root 'config\tunel.local.json'
if (-not (Test-Path $tunelLocal)) {
    Write-Host ""
    Write-Host "Sin config\tunel.local.json (corré Configurar-Tunel-Inicial.ps1)"
    exit 0
}

$j = Get-Content $tunelLocal -Raw | ConvertFrom-Json
$publicUrl = "https://$($j.hostname)"
Write-Host ""
Write-Host "=== Publico (solo comprueba que responde algo; Access puede redirigir) ==="
try {
    $resp = Invoke-WebRequest -Uri $publicUrl -TimeoutSec 15 -MaximumRedirection 5 -UseBasicParsing
    Write-Host "[OK] $publicUrl HTTP $($resp.StatusCode)"
} catch {
    if ($_.Exception.Response) {
        $code = [int]$_.Exception.Response.StatusCode
        Write-Host "[INFO] $publicUrl HTTP $code (403/302 suele ser Access esperando login)"
    } else {
        Write-Host "[FAIL] $publicUrl - $($_.Exception.Message)"
    }
}

Write-Host ""
Write-Host "Prueba manual celular 4G: docs\TUNEL-CLOUDFLARE.md seccion Prueba desde celular"
