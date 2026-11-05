<#
.SYNOPSIS
    Adds the app's signing certificate to "Trusted People" of this PC.

.DESCRIPTION
    Install-FluentWeather.ps1 runs this script with administrator approval. It is the only step of
    the setup that needs administrator rights. The certificate file holds only the public part of
    the certificate. Its private key was deleted right after the app was signed, so nothing else
    can ever be signed with it. Certificates of earlier versions with the same name are removed.
#>
param([Parameter(Mandatory = $true)][string]$CerPath)

$ErrorActionPreference = 'Stop'
try {
    $new = Import-Certificate -FilePath $CerPath -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople'
    # Earlier versions of the setup added their own certificate with the same name. Remove those.
    Get-ChildItem -LiteralPath 'Cert:\LocalMachine\TrustedPeople' |
        Where-Object { $_.Subject -eq $new.Subject -and $_.Thumbprint -ne $new.Thumbprint } |
        Remove-Item -Force
    exit 0
} catch {
    Write-Host "[x] $($_.Exception.Message)" -ForegroundColor Red
    Read-Host 'Press Enter to close' | Out-Null
    exit 1
}
