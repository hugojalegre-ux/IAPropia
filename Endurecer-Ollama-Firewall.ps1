# Bloquea conexiones ENTRANTES a Ollama (11434) desde fuera de loopback.
# Requiere PowerShell como administrador. Ejecutar solo si querés endurecer LAN.

#Requires -RunAsAdministrator

$ErrorActionPreference = 'Stop'
$ruleName = 'IAPropia-Bloquear-Ollama-11434-Externo'

$existing = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Regla '$ruleName' ya existe."
    exit 0
}

New-NetFirewallRule -DisplayName $ruleName `
    -Direction Inbound `
    -LocalPort 11434 `
    -Protocol TCP `
    -Action Block `
    -Profile Any `
    -Description 'Evita que otros hosts usen Ollama en esta PC (IA propia usa solo 127.0.0.1).'

Write-Host "Regla creada. Ollama sigue accesible desde esta máquina vía 127.0.0.1."
