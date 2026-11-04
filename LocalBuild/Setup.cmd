@echo off
rem One-time setup. Asks for administrator approval.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Setup-FluentWeatherLocal.ps1" %*
pause
