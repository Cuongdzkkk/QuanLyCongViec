@echo off
cd /d "%~dp0.."

set "API_URL=%~1"
if "%API_URL%"=="" set "API_URL=https://localhost:7033;http://localhost:5136"

powershell -NoProfile -ExecutionPolicy Bypass -File ".\Backend\run-api.ps1" -Url "%API_URL%"
