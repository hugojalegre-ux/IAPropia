# Arranca Ollama (portable o instalado) sin permisos de administrador y opcionalmente descarga el modelo de IA propia.
param(
    [switch]$AutoPull,
    [string]$Modelo = "",
    [int]$EsperaSegundos = 25
)

$ErrorActionPreference = 'Stop'

function Get-OllamaExe {
    $candidatos = @(
        (Join-Path $env:LOCALAPPDATA 'OllamaPortable\ollama.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Ollama\ollama.exe'),
        (Join-Path ${env:ProgramFiles} 'Ollama\ollama.exe')
    )
    $cmd = Get-Command ollama -ErrorAction SilentlyContinue
    if ($cmd -and $cmd.Source) { return $cmd.Source }
    foreach ($p in $candidatos) {
        if (Test-Path $p) { return $p }
    }
    return $null
}

function Get-ModeloDesdeAppsettings {
    param([string]$Root)
    $paths = @(
        (Join-Path $Root 'dist\IAPropia-win-x64\appsettings.json'),
        (Join-Path $Root 'src\IAPropia.Server\appsettings.json')
    )
    foreach ($path in $paths) {
        if (-not (Test-Path $path)) { continue }
        try {
            $j = Get-Content $path -Raw | ConvertFrom-Json
            return $j.IAPropia.Ollama.Modelo
        } catch {}
    }
    return 'llama3.2'
}

function Test-OllamaPuerto {
    try {
        $null = Invoke-RestMethod -Uri 'http://127.0.0.1:11434/api/tags' -TimeoutSec 3
        return $true
    } catch {
        return $false
    }
}

function Test-ModeloInstalado([string]$Modelo) {
    try {
        $r = Invoke-RestMethod -Uri 'http://127.0.0.1:11434/api/tags' -TimeoutSec 5
        foreach ($m in $r.models) {
            $n = $m.name
            $prefijo = "${Modelo}:"
            if ($n -eq $Modelo -or $n.StartsWith($prefijo) -or $n.Split(':')[0] -eq $Modelo) { return $true }
        }
    } catch {}
    return $false
}

$root = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Modelo)) {
    $Modelo = Get-ModeloDesdeAppsettings -Root $root
}

$ollama = Get-OllamaExe
if (-not $ollama) {
    Write-Host @"
No se encontro ollama.exe.
Instalacion portable sin admin (recomendada):
  winget install Ollama.Ollama.Portable --accept-package-agreements --location "$env:LOCALAPPDATA\OllamaPortable"
Reabri la terminal despues de instalar.
"@
    exit 1
}

Write-Host "Ollama: $ollama"

if (-not (Test-OllamaPuerto)) {
    Write-Host "Iniciando ollama serve..."
    Start-Process -FilePath $ollama -ArgumentList 'serve' -WindowStyle Hidden
    $deadline = (Get-Date).AddSeconds($EsperaSegundos)
    while ((Get-Date) -lt $deadline) {
        if (Test-OllamaPuerto) { break }
        Start-Sleep -Seconds 1
    }
}

if (-not (Test-OllamaPuerto)) {
    Write-Error "Ollama no respondio en http://127.0.0.1:11434 tras $EsperaSegundos s."
}

Write-Host "Ollama responde en :11434"

if (-not (Test-ModeloInstalado $Modelo)) {
    if ($AutoPull) {
        Write-Host "Descargando modelo $Modelo (puede tardar varios minutos)..."
        & $ollama pull $Modelo
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    } else {
        Write-Host "Modelo $Modelo no instalado. Ejecuta: .\Preparar-Ollama.ps1 -AutoPull"
    }
} else {
    Write-Host "Modelo $Modelo ya disponible."
}

Write-Host "Listo para IA propia."
