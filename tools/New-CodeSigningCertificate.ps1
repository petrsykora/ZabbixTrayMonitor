<#
.SYNOPSIS
    Creates your own code signing certificate for ZabbixTrayMonitor.

.DESCRIPTION
    Creates a self-signed code signing certificate and exports:
      - <Name>.pfx               private key, only used for signing (keep it secret!)
      - <Name>.cer               public certificate, distributed to the clients via GPO
      - <Name>.pfx.base64.txt    value for the GitHub secret SIGNING_CERT_PFX_BASE64

    Afterwards the certificate is removed from your certificate store again,
    so the private key only exists in the PFX file.

.EXAMPLE
    .\New-CodeSigningCertificate.ps1 -Subject "CN=My Company Code Signing" -OutputFolder C:\Signing
#>
param(
    [string] $Subject = "CN=ZabbixTrayMonitor Code Signing",
    [int] $ValidYears = 5,
    [string] $OutputFolder = (Join-Path $PWD "signing"),
    [string] $FileName = "ZabbixTrayMonitor-CodeSigning"
)

$ErrorActionPreference = "Stop"

New-Item -ItemType Directory -Force -Path $OutputFolder | Out-Null

$password = Read-Host -AsSecureString "Password for the PFX file (also needed as GitHub secret SIGNING_CERT_PASSWORD)"

$cert = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject $Subject `
    -KeyAlgorithm RSA `
    -KeyLength 3072 `
    -HashAlgorithm SHA256 `
    -KeyExportPolicy Exportable `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -NotAfter (Get-Date).AddYears($ValidYears)

try {
    $pfxPath = Join-Path $OutputFolder "$FileName.pfx"
    $cerPath = Join-Path $OutputFolder "$FileName.cer"
    $base64Path = Join-Path $OutputFolder "$FileName.pfx.base64.txt"

    Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $password | Out-Null
    Export-Certificate -Cert $cert -FilePath $cerPath -Type CERT | Out-Null
    [Convert]::ToBase64String([IO.File]::ReadAllBytes($pfxPath)) | Set-Content -NoNewline -Path $base64Path

    Write-Host ""
    Write-Host "Certificate created:" -ForegroundColor Green
    Write-Host "  Subject:    $($cert.Subject)"
    Write-Host "  Thumbprint: $($cert.Thumbprint)"
    Write-Host "  Valid until: $($cert.NotAfter)"
    Write-Host ""
    Write-Host "  $pfxPath  -> keep secret, never commit to the repository"
    Write-Host "  $cerPath  -> distribute via GPO (Trusted Root Certification Authorities + Trusted Publishers)"
    Write-Host "  $base64Path  -> paste as GitHub secret SIGNING_CERT_PFX_BASE64, then delete the file"
}
finally {
    # do not leave the private key in the user certificate store
    Remove-Item -Path "Cert:\CurrentUser\My\$($cert.Thumbprint)" -DeleteKey -ErrorAction SilentlyContinue
}
