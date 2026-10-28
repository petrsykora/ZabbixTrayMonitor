<#
.SYNOPSIS
    Signs files with the code signing certificate from the GitHub secrets (used by the release workflow).

.DESCRIPTION
    Reads the certificate from the environment variables SIGNING_CERT_PFX_BASE64 and SIGNING_CERT_PASSWORD,
    signs the given files with signtool (SHA256 + RFC 3161 timestamp) and checks that a signature is present.

.EXAMPLE
    .\tools\Sign-Files.ps1 -Path publish\ZabbixTrayMonitor.exe, publish\ZabbixTrayMonitor.dll
#>
param(
    [Parameter(Mandatory = $true)]
    [string[]] $Path
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrEmpty($env:SIGNING_CERT_PFX_BASE64)) { throw "SIGNING_CERT_PFX_BASE64 is not set" }

$files = $Path | ForEach-Object { Get-Item $_ }
$pfxPath = Join-Path ([IO.Path]::GetTempPath()) "signing-$([guid]::NewGuid()).pfx"
[IO.File]::WriteAllBytes($pfxPath, [Convert]::FromBase64String($env:SIGNING_CERT_PFX_BASE64))

try {
    $signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" |
        Sort-Object FullName |
        Select-Object -Last 1 -ExpandProperty FullName

    if (-not $signtool) { throw "signtool.exe not found" }

    & $signtool sign /fd SHA256 /f $pfxPath /p $env:SIGNING_CERT_PASSWORD /tr http://timestamp.digicert.com /td SHA256 $files.FullName
    if ($LASTEXITCODE -ne 0) { throw "signtool failed ($LASTEXITCODE)" }

    # Eigenes Zertifikat ist auf dem Runner nicht vertrauenswürdig, deshalb nur prüfen dass eine Signatur vorhanden ist
    foreach ($file in $files) {
        $signature = Get-AuthenticodeSignature $file.FullName
        if ($null -eq $signature.SignerCertificate) { throw "No signature: $($file.FullName)" }
        Write-Host "$($file.Name): $($signature.SignerCertificate.Subject) ($($signature.SignerCertificate.Thumbprint))"
    }
}
finally {
    Remove-Item $pfxPath -Force -ErrorAction SilentlyContinue
}
