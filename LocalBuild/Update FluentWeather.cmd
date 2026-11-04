@echo off
rem Checks for a new FluentWeather release. If one exists, shows its notes and asks before it updates.
rem Run it without "Run as administrator".
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Update-FluentWeatherLocal.ps1" %*
echo.
pause
