# Conecta IA propia al repo hugojalegre-ux/IAPropia (primera vez o push pendiente).
# Requiere: GitHub CLI (gh) — https://cli.github.com

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$owner = 'hugojalegre-ux'
$repo = 'IAPropia'
$remoteUrl = "https://github.com/$owner/$repo.git"

$login = gh api user -q .login 2>$null
Write-Host "GitHub CLI — cuenta activa: $login"

if ($login -ne $owner) {
    Write-Host ""
    Write-Host "Necesitás iniciar sesión como $owner (no como $login)."
    Write-Host "Se abrirá el navegador para autorizar gh."
    gh auth login -h github.com -p https -w
    $logins = gh auth status 2>&1 | Out-String
    if ($logins -match $owner) {
        gh auth switch -u $owner 2>$null
    }
    $login = gh api user -q .login
    if ($login -ne $owner) {
        throw "Seguís como '$login'. Ejecutá: gh auth switch -u $owner"
    }
}

if (-not (git rev-parse --is-inside-work-tree 2>$null)) {
    git init -b main
}

$hasOrigin = git remote get-url origin 2>$null
if (-not $hasOrigin) {
    git remote add origin $remoteUrl
} else {
    git remote set-url origin $remoteUrl
}

$exists = $false
try {
    gh repo view "$owner/$repo" -q .name 2>$null | Out-Null
    $exists = $LASTEXITCODE -eq 0
} catch { $exists = $false }

if (-not $exists) {
    Write-Host "Creando repo público $owner/$repo ..."
    gh repo create "$owner/$repo" --public `
        --description "Asistente personal local (Ollama) - carpetas, aprendizajes, busqueda web opcional" `
        --source=. --remote=origin
} else {
    Write-Host "Repo $owner/$repo ya existe."
}

$status = git status --porcelain
if ($status) {
    Write-Host "Hay cambios sin commitear; commiteá antes del push si querés subirlos."
}

Write-Host "Publicando rama main ..."
git push -u origin main

Write-Host ""
Write-Host "Listo: https://github.com/$owner/$repo"
