@echo off
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Iniciar-Tunel-IAPropia.ps1"
pause
