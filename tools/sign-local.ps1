# Sign dist\GameLog.exe with the local self-signed code signing certificate.
# Usage: tools\sign-local.cmd
# The certificate must exist in Cert:\CurrentUser\My and be trusted locally
# (tools\sign-local.cmd creates/uses thumbprint below).

param(
    [string]$Thumbprint = '36844358F04B4B57C28AF748E2ED3C35A90A82F6'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'dist\GameLog.exe'

if (-not (Test-Path $exe)) {
    Write-Host "[ERROR] not found: $exe" -ForegroundColor Red
    Write-Host "        run build.cmd first"
    exit 1
}

$cert = Get-Item "Cert:\CurrentUser\My\$Thumbprint" -ErrorAction SilentlyContinue
if (-not $cert) {
    Write-Host "[ERROR] certificate not found: $Thumbprint" -ForegroundColor Red
    Write-Host "        create one with:"
    Write-Host '        New-SelfSignedCertificate -Type CodeSigningCert -Subject "CN=GameLog, O=GameLog" -KeyUsage DigitalSignature -FriendlyName "GameLog Local Code Signing" -CertStoreLocation Cert:\CurrentUser\My -NotAfter (Get-Date).AddYears(5) -KeyExportPolicy Exportable'
    exit 1
}

$sig = Set-AuthenticodeSignature -FilePath $exe -Certificate $cert `
    -TimestampServer 'http://timestamp.digicert.com' -HashAlgorithm SHA256
Write-Host "signed: $exe"
Write-Host "status: $($sig.Status)  $($sig.StatusMessage)"

$check = Get-AuthenticodeSignature $exe
if ($check.Status -ne 'Valid') {
    Write-Host "[WARN] signature is not valid: $($check.Status)" -ForegroundColor Yellow
    Write-Host "       (the certificate must be trusted in CurrentUser\Root)"
    exit 2
}

Write-Host "signer : $($check.SignerCertificate.Subject)"
Write-Host "thumb  : $($check.SignerCertificate.Thumbprint)"
Write-Host "verify : tools\verify-signature.cmd"
