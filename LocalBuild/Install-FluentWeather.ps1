<#
.SYNOPSIS
    Installs FluentWeather (Local) from the files inside FluentWeather-Setup.exe.

.DESCRIPTION
    FluentWeather-Setup.exe extracts its files to a temporary folder and runs this script as the
    signed-in user. The script:
      1. checks Windows and the files,
      2. adds the app's signing certificate to "Trusted People" of this PC (administrator approval),
      3. installs the app package and the Microsoft packages it needs, for the signed-in user,
      4. copies the tray icons program and the uninstaller to %LOCALAPPDATA%\Programs\FluentWeather,
      5. adds a Startup item for the tray icons, Start menu items and an entry in Settings > Apps,
      6. starts the tray icons and opens the app.
    Nothing is downloaded. Exit code 0 means installed.
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
$MenuDir      = Join-Path $Programs 'FluentWeather'

function Write-Step([string]$Text) { Write-Host "[*] $Text" -ForegroundColor Cyan }
function Write-Ok([string]$Text)   { Write-Host "[+] $Text" -ForegroundColor Green }

function Stop-Install([string]$Text) {
    Write-Host ''
    Write-Host "[x] $Text" -ForegroundColor Red
    Write-Host '    Nothing else was changed.'
    Read-Host 'Press Enter to close' | Out-Null
    exit 1
}

try {
    Write-Host 'FluentWeather (Local) setup' -ForegroundColor Cyan
    Write-Host ''

    # --- Checks -------------------------------------------------------------------------------

    $info = Get-Content -LiteralPath (Join-Path $Here 'install-info.json') -Raw | ConvertFrom-Json

    if ([Environment]::OSVersion.Version.Build -lt 17763) {
        Stop-Install 'FluentWeather needs Windows 10 version 1809 or later, or Windows 11.'
    }
    $osArch = if ($env:PROCESSOR_ARCHITEW6432) { $env:PROCESSOR_ARCHITEW6432 } else { $env:PROCESSOR_ARCHITECTURE }
    if ($info.Architecture -eq 'x64' -and $osArch -notin @('AMD64', 'ARM64')) {
        Stop-Install "This setup contains the 64-bit version of the app. This PC runs $osArch Windows."
    }
    if (Test-Path -LiteralPath (Join-Path $env:ProgramFiles 'FluentWeatherLocal\config.json')) {
        Stop-Install 'This PC has the FluentWeather local build from Setup.cmd. It already has everything this setup installs.'
    }

    $me = [Security.Principal.WindowsIdentity]::GetCurrent().Name
    $desk = $null
    try { $desk = (Get-CimInstance -ClassName Win32_ComputerSystem).UserName } catch { }
    if ($desk -and ($desk -ine $me)) {
        Stop-Install "This runs as '$me' but the signed-in user is '$desk'. Start FluentWeather-Setup.exe without 'Run as administrator'."
    }

    $cerPath  = Join-Path $Here 'FluentWeatherLocalBuild.cer'
    $msixPath = Join-Path $Here $info.MsixFile
    foreach ($f in @($cerPath, $msixPath, (Join-Path $Here 'FluentWeatherTray.exe'), (Join-Path $Here 'FluentWeather-Uninstall.exe'))) {
        if (-not (Test-Path -LiteralPath $f)) { Stop-Install "A file is missing from the setup: $(Split-Path -Leaf $f)" }
    }

    # The certificate in the setup must be the one that signed the app package.
    $cer = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($cerPath)
    if ($cer.Thumbprint -ne $info.Thumbprint) { Stop-Install 'The certificate in the setup does not match the app.' }
    $sig = Get-AuthenticodeSignature -FilePath $msixPath
    if (-not $sig.SignerCertificate -or $sig.SignerCertificate.Thumbprint -ne $info.Thumbprint) {
        Stop-Install 'The app package is not signed with the certificate in the setup.'
    }
    if ($sig.Status -in 'HashMismatch', 'NotSigned', 'Incompatible', 'NotSupportedFileFormat') {
        Stop-Install "The app package signature is invalid (status: $($sig.Status)). The file may be damaged."
    }

    # --- Certificate ------------------------------------------------------------------------------

    $trusted = Join-Path 'Cert:\LocalMachine\TrustedPeople' $info.Thumbprint
    if (Test-Path -LiteralPath $trusted) {
        Write-Ok 'The signing certificate is already trusted on this PC.'
    } else {
        Write-Step 'Asking for administrator approval to add the signing certificate to Trusted People.'
        $trustScript = Join-Path $Here 'Trust-Certificate.ps1'
        try {
            $p = Start-Process -FilePath (Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe') -Verb RunAs -Wait -PassThru `
                -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$trustScript`"", '-CerPath', "`"$cerPath`"")
        } catch {
            Stop-Install 'Administrator approval was declined. Windows only installs the app with its certificate trusted.'
        }
        if (-not (Test-Path -LiteralPath $trusted)) { Stop-Install 'The certificate could not be added.' }
        Write-Ok 'Signing certificate added to Trusted People.'
    }

    # --- App package ---------------------------------------------------------------------------

    Get-Process -Name 'FluentWeatherTray' -ErrorAction SilentlyContinue | ForEach-Object {
        $path = $null
        try { $path = $_.Path } catch { }
        if ($path -and $path.StartsWith($InstallDir + '\', [StringComparison]::OrdinalIgnoreCase)) {
            Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
        }
    }

    Write-Step "Installing FluentWeather (Local) $($info.Version). This takes up to a minute."
    $addArgs = @{
        Path                      = $msixPath
        ForceApplicationShutdown  = $true
        ForceUpdateFromAnyVersion = $true
    }
    $deps = @($info.Dependencies | ForEach-Object { Join-Path $Here $_ })
    if ($deps.Count -gt 0) { $addArgs.DependencyPath = $deps }
    Add-AppxPackage @addArgs

    $pkg = Get-AppxPackage -Name $PackageName | Select-Object -First 1
    if (-not $pkg) { Stop-Install 'The app package did not install.' }
    Write-Ok "App installed: $($pkg.PackageFullName)"

    # --- Tray icons and uninstaller -----------------------------------------------------------------

    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
    foreach ($name in 'FluentWeatherTray.exe', 'FluentWeather-Uninstall.exe', 'README.txt', 'FluentWeather.ico', 'install-info.json') {
        $src = Join-Path $Here $name
        if (Test-Path -LiteralPath $src) { Copy-Item -LiteralPath $src -Destination (Join-Path $InstallDir $name) -Force }
    }

    # The tray icons read the app's data folder name from this file.
    New-Item -ItemType Directory -Path $TrayWorkDir -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $TrayWorkDir 'package-family-name.txt') -Value $pkg.PackageFamilyName -Encoding ASCII
    Set-Content -LiteralPath (Join-Path $TrayWorkDir 'direct-install.txt') -Value 'Created by FluentWeather-Setup.exe' -Encoding ASCII
    $icon = Join-Path $Here 'tray-icon.png'
    if (Test-Path -LiteralPath $icon) { Copy-Item -LiteralPath $icon -Destination (Join-Path $TrayWorkDir 'tray-icon.png') -Force }

    $tray = Join-Path $InstallDir 'FluentWeatherTray.exe'
    $uninstaller = Join-Path $InstallDir 'FluentWeather-Uninstall.exe'
    $ico = Join-Path $InstallDir 'FluentWeather.ico'
    $shell = New-Object -ComObject WScript.Shell
    function New-Shortcut([string]$Path, [string]$Target, [string]$Arguments, [string]$Description) {
        $s = $shell.CreateShortcut($Path)
        $s.TargetPath = $Target
        if ($Arguments) { $s.Arguments = $Arguments }
        $s.WorkingDirectory = $InstallDir
        $s.Description = $Description
        if (Test-Path -LiteralPath $ico) { $s.IconLocation = "$ico,0" }
        $s.Save()
    }
    # Start menu folder "FluentWeather" with the app, the tray icons, the readme and the uninstaller.
    New-Item -ItemType Directory -Path $MenuDir -Force | Out-Null
    New-Shortcut (Join-Path $MenuDir 'FluentWeather.lnk') (Join-Path $env:WINDIR 'explorer.exe') "shell:AppsFolder\$($pkg.PackageFamilyName)!App" 'Open FluentWeather'
    New-Shortcut (Join-Path $MenuDir 'FluentWeather tray icons.lnk') $tray '' 'Start the FluentWeather tray icons'
    New-Shortcut (Join-Path $MenuDir 'Read me.lnk') (Join-Path $InstallDir 'README.txt') '' 'How FluentWeather is installed, used and removed'
    New-Shortcut (Join-Path $MenuDir 'Uninstall FluentWeather.lnk') $uninstaller '' 'Remove FluentWeather (Local), its tray icons and its certificate'
    New-Shortcut (Join-Path $Startup 'FluentWeather Tray.lnk') $tray '' 'FluentWeather tray icons'
    Write-Ok 'Start menu folder "FluentWeather" and the Startup item for the tray icons added.'

    # An entry in Settings > Apps that removes everything, not only the app package.
    New-Item -Path $UninstallKey -Force | Out-Null
    $values = @{
        DisplayName     = 'FluentWeather (Local) with tray icons'
        DisplayVersion  = [string]$info.Version
        Publisher       = 'duedilly'
        InstallLocation = $InstallDir
        UninstallString = "`"$uninstaller`""
        DisplayIcon     = $ico
        Comments        = 'FluentWeather by Gabboxl with saved locations and tray icons'
        URLInfoAbout    = 'https://github.com/Duedilly108/FluentWeather'
    }
    foreach ($k in $values.Keys) { New-ItemProperty -Path $UninstallKey -Name $k -Value $values[$k] -PropertyType String -Force | Out-Null }
    foreach ($k in 'NoModify', 'NoRepair') { New-ItemProperty -Path $UninstallKey -Name $k -Value 1 -PropertyType DWord -Force | Out-Null }
    Write-Ok 'Added "FluentWeather (Local) with tray icons" to Settings > Apps.'

    # --- Start ---------------------------------------------------------------------------------------

    Start-Process -FilePath $tray -WorkingDirectory $InstallDir
    Start-Process -FilePath (Join-Path $env:WINDIR 'explorer.exe') -ArgumentList "shell:AppsFolder\$($pkg.PackageFamilyName)!App"
    Write-Ok 'Done.'
    Start-Sleep -Seconds 2
    exit 0
}
catch {
    Stop-Install "Setup stopped: $($_.Exception.Message)"
}
