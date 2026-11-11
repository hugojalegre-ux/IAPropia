# IA propia — servidor local independiente de SOT Accusys
param(
    [switch]$SinAbrirNavegador,
    [switch]$SinPublicar,
    [switch]$SinOllama,
    [int]$Puerto = 0
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$proj = Join-Path $root 'src\IAPropia.Server\IAPropia.Server.csproj'
$distDir = Join-Path $root 'dist\IAPropia-win-x64'
$portableExe = Join-Path $distDir 'IAPropia.Server.exe'
$publishScript = Join-Path $root 'Publicar-Portable.ps1'

if (-not (Test-Path $proj)) {
    Write-Error "No se encontró el proyecto en $proj"
}

function Test-NecesitaPublicar {
    if (-not (Test-Path $portableExe)) { return $true }
    $exeTime = (Get-Item $portableExe).LastWriteTimeUtc
    $projTime = (Get-Item $proj).LastWriteTimeUtc
    if ($projTime -gt $exeTime) { return $true }
    $srcNewer = Get-ChildItem -Path (Join-Path $root 'src') -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Extension -match '\.(cs|json|html|js|css)$' -and $_.LastWriteTimeUtc -gt $exeTime }
    return ($null -ne $srcNewer)
}

if (-not $SinPublicar -and (Test-NecesitaPublicar)) {
    Write-Host "Publicando IA propia (portable)..."
    & $publishScript
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

if (-not (Test-Path $portableExe)) {
    Write-Error "No hay build portable. Ejecutá: .\Publicar-Portable.ps1"
}

if (-not $SinOllama) {
    $prepOllama = Join-Path $root 'Preparar-Ollama.ps1'
    if (Test-Path $prepOllama) {
        Write-Host "Preparando Ollama (serve + modelo si falta)..."
        & $prepOllama -AutoPull
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Ollama no quedó listo; IA propia abrirá igual (revisá el pie de la UI)."
        }
    }
}

$secretsExample = Join-Path $root 'src\IAPropia.Server\appsettings.secrets.json.example'
$secretsDist = Join-Path $distDir 'appsettings.secrets.json'
if (-not (Test-Path $secretsDist) -and (Test-Path $secretsExample)) {
    Copy-Item $secretsExample $secretsDist
}

$url = if ($Puerto -gt 0) { "http://127.0.0.1:$Puerto" } else { 'http://127.0.0.1:5077' }
$env:ASPNETCORE_URLS = $url

if (-not $SinAbrirNavegador) {
    Start-Job -ScriptBlock {
        param($u)
        Start-Sleep -Seconds 2
        Start-Process $u
    } -ArgumentList $url | Out-Null
}

Write-Host "IA propia en $url (portable)"
Write-Host "Detener: Ctrl+C"

Push-Location $distDir
try {
    & $portableExe
} finally {
    Pop-Location
}
