<#
.SYNOPSIS
    Converts the LICENSE text file to RTF for the license dialogs of the installers.

.DESCRIPTION
    The MSI (WixUI license dialog) and setup.exe (WiX standard bootstrapper) show the license as RTF.
    The RTF is generated at build time so it always matches LICENSE, including all copyright lines.

.EXAMPLE
    .\tools\New-LicenseRtf.ps1 -Source LICENSE -Destination installer\License.rtf
#>
param(
    [string] $Source = "LICENSE",
    [string] $Destination = "installer\License.rtf"
)

$ErrorActionPreference = "Stop"

$text = [IO.File]::ReadAllText((Resolve-Path $Source), [Text.Encoding]::UTF8).Replace("`r`n", "`n").Trim()

# Zeilenumbrüche innerhalb eines Absatzes entfernen, damit der Text im Dialog sauber umbricht;
# Copyright-Zeilen bleiben jeweils eigene Zeilen
$paragraphs = $text -split "`n\s*`n" | ForEach-Object {
    $lines = $_ -split "`n" | ForEach-Object { $_.Trim() }
    if ($lines[0] -like "Copyright*") { $lines -join "`n" } else { $lines -join " " }
}
$text = $paragraphs -join "`n`n"
$body = [Text.StringBuilder]::new()

foreach ($character in $text.ToCharArray()) {
    switch ($character) {
        '\' { [void]$body.Append('\\') }
        '{' { [void]$body.Append('\{') }
        '}' { [void]$body.Append('\}') }
        "`n" { [void]$body.Append("\par`n") }
        default {
            # Nicht-ASCII-Zeichen (zB. ý) als Unicode-Escape, "?" ist der Ersatz für alte RTF-Leser
            if ([int]$character -gt 127) { [void]$body.Append("\u$([int]$character)?") }
            else { [void]$body.Append($character) }
        }
    }
}

$rtf = "{\rtf1\ansi\ansicpg1252\deff0{\fonttbl{\f0\fswiss Segoe UI;}}\viewkind4\uc1\pard\f0\fs18 " + $body.ToString() + "\par`n}"

$destinationPath = [IO.Path]::GetFullPath($Destination)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destinationPath)) | Out-Null
[IO.File]::WriteAllText($destinationPath, $rtf, [Text.Encoding]::ASCII)
Write-Host "License RTF written to $destinationPath"
