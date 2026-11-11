# Helpers cloudflared portable (sin administrador). Dot-source desde scripts en la raíz de IAPropia.

function Get-IAPropiaRoot {
    param([string]$FromScriptRoot = $PSScriptRoot)
    return $FromScriptRoot
}

function Get-IAPropiaCloudflaredDir {
    param([string]$Root = (Get-IAPropiaRoot -FromScriptRoot $PSScriptRoot))
    Join-Path $Root 'tools\cloudflared'
}

function Get-IAPropiaTunnelConfigDir {
    param([string]$Root = (Get-IAPropiaRoot -FromScriptRoot $PSScriptRoot))
    Join-Path $Root 'config\cloudflared.local'
}

function Get-IAPropiaCloudflaredExe {
    param([string]$Root = (Get-IAPropiaRoot -FromScriptRoot $PSScriptRoot))

  $portable = Join-Path (Get-IAPropiaCloudflaredDir -Root $Root) 'cloudflared.exe'
    if (Test-Path $portable) { return $portable }

    $cmd = Get-Command cloudflared -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $wingetLink = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Links\cloudflared.exe'
    if (Test-Path $wingetLink) { return $wingetLink }

    return $null
}

function Install-IAPropiaCloudflaredPortable {
    param([string]$Root = (Get-IAPropiaRoot -FromScriptRoot $PSScriptRoot))

    $dir = Get-IAPropiaCloudflaredDir -Root $Root
    $dest = Join-Path $dir 'cloudflared.exe'
    New-Item -ItemType Directory -Force -Path $dir | Out-Null

    $url = 'https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe'
    Write-Host "Descargando cloudflared portable (sin administrador)..."
    Write-Host $url
    Invoke-WebRequest -Uri $url -OutFile $dest -UseBasicParsing
    if (-not (Test-Path $dest)) {
        throw "No se pudo descargar cloudflared en $dest"
    }
    return $dest
}

function Resolve-IAPropiaCloudflaredExe {
    param(
        [string]$Root = (Get-IAPropiaRoot -FromScriptRoot $PSScriptRoot),
        [switch]$InstalarSiFalta
    )

    $exe = Get-IAPropiaCloudflaredExe -Root $Root
    if ($exe) { return $exe }
    if ($InstalarSiFalta) {
        return Install-IAPropiaCloudflaredPortable -Root $Root
    }
    return $null
}

function Get-IAPropiaTunnelConfigPath {
    param([string]$Root = (Get-IAPropiaRoot -FromScriptRoot $PSScriptRoot))
    Join-Path (Get-IAPropiaTunnelConfigDir -Root $Root) 'config.yml'
}

function Get-IAPropiaOriginCertPath {
    param([string]$Root = (Get-IAPropiaRoot -FromScriptRoot $PSScriptRoot))
    Join-Path (Get-IAPropiaTunnelConfigDir -Root $Root) 'cert.pem'
}
