# Publica IA propia portable (self-contained, sin depender del runtime instalado)
param(
    [switch]$Zip
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$proj = Join-Path $root 'src\IAPropia.Server\IAPropia.Server.csproj'
$out = Join-Path $root 'dist\IAPropia-win-x64'

Write-Host "Publicando IA propia (win-x64, self-contained)..."
dotnet publish $proj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $out

Copy-Item (Join-Path $root 'config') (Join-Path $out 'config') -Recurse -Force
Copy-Item (Join-Path $root 'conocimiento') (Join-Path $out 'conocimiento') -Recurse -Force
if (Test-Path (Join-Path $root 'version.json')) {
    Copy-Item (Join-Path $root 'version.json') (Join-Path $out 'version.json') -Force
}
New-Item -ItemType Directory -Force -Path (Join-Path $out 'data\conversaciones') | Out-Null

$secretsExample = Join-Path $root 'src\IAPropia.Server\appsettings.secrets.json.example'
$secretsDest = Join-Path $out 'appsettings.secrets.json'
if (-not (Test-Path $secretsDest) -and (Test-Path $secretsExample)) {
    Copy-Item $secretsExample $secretsDest
}

@'
@echo off
chcp 65001 >nul
set ASPNETCORE_URLS=http://127.0.0.1:5077
start "" "%~dp0IAPropia.Server.exe"
timeout /t 2 >nul
start http://127.0.0.1:5077
'@ | Set-Content -Encoding UTF8 (Join-Path $out 'Iniciar-IAPropia.bat')

Write-Host "Listo: $out"

if ($Zip) {
    $zipPath = Join-Path $root 'dist\IAPropia-win-x64.zip'
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path $out -DestinationPath $zipPath
    Write-Host "ZIP: $zipPath"
}
