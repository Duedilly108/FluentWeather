<#
.SYNOPSIS
    Removes what FluentWeather-Setup.exe installed.

.DESCRIPTION
    FluentWeather-Uninstall.exe extracts this script to a temporary folder and runs it as the
    signed-in user. It removes, in this order:
      1. the tray icons (it ends them first),
      2. the app package with its saved locations and settings,
      3. the Startup item, the Start menu items and the entry in Settings > Apps,
      4. the tray settings in %LOCALAPPDATA%\FluentWeatherLocal,
      5. the signing certificate in Trusted People (administrator approval).
    The uninstaller then deletes %LOCALAPPDATA%\Programs\FluentWeather after it has closed.
    It does not run on a PC that has the FluentWeather local build from Setup.cmd, because there the
    same app belongs to that build. Exit code 0 means removed.
#>

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

$Here         = $PSScriptRoot
$PackageName  = 'FluentWeather.LocalBuild'
$InstallDir   = Join-Path $env:LOCALAPPDATA 'Programs\FluentWeather'
$TrayWorkDir  = Join-Path $env:LOCALAPPDATA 'FluentWeatherLocal'
$UninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\FluentWeatherDirect'
$Programs     = [Environment]::GetFolderPath('Programs')
$Startup      = [Environment]::GetFolderPath('Startup')

function Write-Step([string]$Text) { Write-Host "[*] $Text" -ForegroundColor Cyan }
function Write-Ok([string]$Text)   { Write-Host "[+] $Text" -ForegroundColor Green }
function Write-Note([string]$Text) { Write-Host "[!] $Text" -ForegroundColor Yellow }

function Stop-Uninstall([string]$Text) {
    Write-Host ''
    Write-Host "[x] $Text" -ForegroundColor Red
    Read-Host 'Press Enter to close' | Out-Null
    exit 1
}

try {
    Write-Host 'FluentWeather (Local) uninstall' -ForegroundColor Cyan
    Write-Host ''

    if (Test-Path -LiteralPath (Join-Path $env:ProgramFiles 'FluentWeatherLocal\config.json')) {
        Stop-Uninstall 'This PC has the FluentWeather local build from Setup.cmd. Remove it in Settings > Apps > "FluentWeather local build". Nothing was changed.'
    }

    $info = Get-Content -LiteralPath (Join-Path $Here 'install-info.json') -Raw | ConvertFrom-Json

    # 1. Tray icons
    Get-Process -Name 'FluentWeatherTray' -ErrorAction SilentlyContinue | ForEach-Object {
        $path = $null
        try { $path = $_.Path } catch { }
        if ($path -and $path.StartsWith($InstallDir + '\', [StringComparison]::OrdinalIgnoreCase)) {
            Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
        }
    }
    Write-Ok 'Tray icons ended.'

    # 2. App package. Windows also removes its data: saved locations and settings.
    $pkgs = @(Get-AppxPackage -Name $PackageName)
    if ($pkgs.Count -gt 0) {
        Write-Step 'Removing the app.'
        $pkgs | Remove-AppxPackage
        Write-Ok 'App removed.'
    } else {
        Write-Ok 'The app was not installed.'
    }

    # 3. Shortcuts and the Settings > Apps entry
    $lnk = Join-Path $Startup 'FluentWeather Tray.lnk'
    if (Test-Path -LiteralPath $lnk) { Remove-Item -LiteralPath $lnk -Force }
    $menuDir = Join-Path $Programs 'FluentWeather'
    if (Test-Path -LiteralPath $menuDir) { Remove-Item -LiteralPath $menuDir -Recurse -Force }
    if (Test-Path -LiteralPath $UninstallKey) { Remove-Item -LiteralPath $UninstallKey -Recurse -Force }
    Write-Ok 'Startup item, Start menu folder and Settings > Apps entry removed.'

    # 4. Tray settings. The folder is removed only if the setup created it.
    if (Test-Path -LiteralPath (Join-Path $TrayWorkDir 'direct-install.txt')) {
        Remove-Item -LiteralPath $TrayWorkDir -Recurse -Force -ErrorAction SilentlyContinue
        Write-Ok 'Tray settings removed.'
    }

    # 5. Certificates. Every setup version added a certificate with this name. Remove all of them.
    $subject = [string]$info.CertSubject
    function Get-OurCertificates { @(Get-ChildItem -LiteralPath 'Cert:\LocalMachine\TrustedPeople' | Where-Object { $_.Subject -eq $subject }) }
    if ($subject -and (Get-OurCertificates).Count -gt 0) {
        Write-Step 'Asking for administrator approval to remove the signing certificate.'
        $command = "Get-ChildItem -LiteralPath 'Cert:\LocalMachine\TrustedPeople' | Where-Object { `$_.Subject -eq '$subject' } | Remove-Item -Force"
        try {
            Start-Process -FilePath (Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe') -Verb RunAs -Wait `
                -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', "`"$command`"") | Out-Null
        } catch { }
        if ((Get-OurCertificates).Count -gt 0) {
            Write-Note 'The certificate is still in Trusted People, because administrator approval was declined.'
            Write-Note 'Run FluentWeather-Uninstall.exe again to remove it.'
        } else {
            Write-Ok 'Signing certificate removed.'
        }
    }

    Write-Ok 'Done.'
    Start-Sleep -Seconds 2
    exit 0
}
catch {
    Stop-Uninstall "Uninstall stopped: $($_.Exception.Message)"
}
