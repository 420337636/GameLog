# Remove the local self-signed certificate from the current user's trust stores.
# Usage: tools\remove-local-cert.cmd            (trust only, keep the cert + private key)
#        tools\remove-local-cert.cmd -All       (also delete it from Cert:\CurrentUser\My)

param(
    [string]$Thumbprint = '36844358F04B4B57C28AF748E2ED3C35A90A82F6',
    [switch]$All
)

$ErrorActionPreference = 'Stop'
$cert = Get-Item "Cert:\CurrentUser\My\$Thumbprint" -ErrorAction SilentlyContinue
if (-not $cert) {
    Write-Host "[WARN] certificate not in CurrentUser\My: $Thumbprint"
}

foreach ($name in @('Root','TrustedPublisher')) {
    $store = New-Object System.Security.Cryptography.X509Certificates.X509Store($name,'CurrentUser')
    $store.Open('ReadWrite')
    $found = $store.Certificates | Where-Object { $_.Thumbprint -eq $Thumbprint }
    foreach ($c in $found) { $store.Remove($c) }
    $store.Close()
    Write-Host "removed from CurrentUser\$name ($($found.Count) entries)"
}

if ($All -and $cert) {
    Remove-Item "Cert:\CurrentUser\My\$Thumbprint" -Force
    Write-Host "removed from CurrentUser\My (private key deleted)"
}

Write-Host "done."
