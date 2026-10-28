<#
.SYNOPSIS
    Erstellt ein eigenes Code-Signing-Zertifikat für ZabbixTrayMonitor.

.DESCRIPTION
    Erzeugt ein selbstsigniertes Code-Signing-Zertifikat und exportiert:
      - <Name>.pfx               privater Schlüssel, nur für das Signieren (geheim halten!)
      - <Name>.cer               öffentliches Zertifikat, wird per GPO auf die Clients verteilt
      - <Name>.pfx.base64.txt    Inhalt für das GitHub Secret SIGNING_CERT_PFX_BASE64

    Das Zertifikat wird danach wieder aus dem eigenen Zertifikatsspeicher entfernt,
    damit der private Schlüssel nur noch in der PFX-Datei existiert.

.EXAMPLE
    .\New-CodeSigningCertificate.ps1 -Subject "CN=Meine Firma Code Signing" -OutputFolder C:\Signing
#>
param(
    [string] $Subject = "CN=ZabbixTrayMonitor Code Signing",
    [int] $ValidYears = 5,
    [string] $OutputFolder = (Join-Path $PWD "signing"),
    [string] $FileName = "ZabbixTrayMonitor-CodeSigning"
)

$ErrorActionPreference = "Stop"

New-Item -ItemType Directory -Force -Path $OutputFolder | Out-Null

$password = Read-Host -AsSecureString "Passwort für die PFX-Datei (wird auch als GitHub Secret SIGNING_CERT_PASSWORD gebraucht)"

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
    Write-Host "Zertifikat erstellt:" -ForegroundColor Green
    Write-Host "  Subject:    $($cert.Subject)"
    Write-Host "  Thumbprint: $($cert.Thumbprint)"
    Write-Host "  Gültig bis: $($cert.NotAfter)"
    Write-Host ""
    Write-Host "  $pfxPath  -> geheim halten, nicht ins Repository"
    Write-Host "  $cerPath  -> per GPO verteilen (Vertrauenswürdige Stammzertifizierungsstellen + Vertrauenswürdige Herausgeber)"
    Write-Host "  $base64Path  -> Inhalt als GitHub Secret SIGNING_CERT_PFX_BASE64 eintragen, danach Datei löschen"
}
finally {
    # privaten Schlüssel nicht im Benutzerzertifikatsspeicher liegen lassen
    Remove-Item -Path "Cert:\CurrentUser\My\$($cert.Thumbprint)" -DeleteKey -ErrorAction SilentlyContinue
}
