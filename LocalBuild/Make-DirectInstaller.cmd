@echo off
rem Makes FluentWeather-Setup.exe, FluentWeather-Uninstall.exe and README.txt in a folder
rem "FluentWeather-Direct" on your Desktop, plus a zip of them to share. Others can install
rem the app with them without building it. Run it without "Run as administrator".
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Make-DirectInstaller.ps1" %*
echo.
pause
