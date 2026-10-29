<#
.SYNOPSIS
    Checks the release files with VirusTotal (used by the release workflow).

.DESCRIPTION
    Uploads the given files to VirusTotal (API v3), waits until all analyses are completed and writes:
      - a Markdown section with the result and a link to the report of every file (for the release notes)
      - a JSON file for a shields.io endpoint badge (attached to the release as virustotal-badge.json)

    The API key is read from the environment variable VIRUSTOTAL_API_KEY.
    The free public API allows 4 requests per minute, so the script waits between requests.

.EXAMPLE
    .\tools\Submit-VirusTotal.ps1 -Path ZabbixTrayMonitor-v1.3.5-win-x64.msi -MarkdownPath virustotal.md -BadgePath virustotal-badge.json
#>
param(
    [Parameter(Mandatory = $true)]
    [string[]] $Path,

    [Parameter(Mandatory = $true)]
    [string] $MarkdownPath,

    [Parameter(Mandatory = $true)]
    [string] $BadgePath,

    [string] $ApiBaseUrl = "https://www.virustotal.com/api/v3",

    # Wartezeit zwischen zwei API-Aufrufen (Public API: 4 Anfragen pro Minute)
    [int] $RequestDelaySeconds = 16,

    [int] $TimeoutMinutes = 30
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrEmpty($env:VIRUSTOTAL_API_KEY)) { throw "VIRUSTOTAL_API_KEY is not set" }

$headers = @{ "x-apikey" = $env:VIRUSTOTAL_API_KEY }

# Dateien bis 32 MB direkt hochladen, größere über eine eigene Upload-URL (bis 650 MB)
$directUploadLimit = 32MB

function Invoke-VirusTotal {
    param([string] $Method, [string] $Uri, [System.IO.FileInfo] $File)

    Start-Sleep -Seconds $RequestDelaySeconds

    if (-not $File) {
        return Invoke-RestMethod -Method $Method -Uri $Uri -Headers $headers
    }

    # Multipart mit Feld "file" in der üblichen Schreibweise (Name und Dateiname in Anführungszeichen);
    # -Form von PowerShell schreibt sie ohne Anführungszeichen und mit zusätzlichem filename*
    $stream = [System.IO.File]::OpenRead($File.FullName)

    try {
        $fileContent = [System.Net.Http.StreamContent]::new($stream)
        $fileContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::new("application/octet-stream")
        $fileContent.Headers.ContentDisposition = [System.Net.Http.Headers.ContentDispositionHeaderValue]::new("form-data")
        $fileContent.Headers.ContentDisposition.Name = '"file"'
        $fileContent.Headers.ContentDisposition.FileName = '"' + $File.Name + '"'

        $content = [System.Net.Http.MultipartFormDataContent]::new()
        $content.Add($fileContent)

        return Invoke-RestMethod -Method $Method -Uri $Uri -Headers $headers -Body $content
    }
    finally {
        $stream.Dispose()
    }
}

# 1. Hochladen
$submissions = foreach ($file in ($Path | ForEach-Object { Get-Item $_ })) {
    $uploadUri = "$ApiBaseUrl/files"

    if ($file.Length -gt $directUploadLimit) {
        $uploadUri = (Invoke-VirusTotal -Method Get -Uri "$ApiBaseUrl/files/upload_url").data
    }

    $sha256 = (Get-FileHash $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    $response = Invoke-VirusTotal -Method Post -Uri $uploadUri -File $file

    Write-Host "Uploaded $($file.Name) ($sha256), analysis $($response.data.id)"

    [pscustomobject]@{
        Name       = $file.Name
        Sha256     = $sha256
        AnalysisId = $response.data.id
        Stats      = $null
    }
}

# 2. Auf die Ergebnisse warten
$deadline = (Get-Date).AddMinutes($TimeoutMinutes)

while ($submissions | Where-Object { -not $_.Stats }) {
    if ((Get-Date) -gt $deadline) { throw "VirusTotal analyses not completed within $TimeoutMinutes minutes" }

    foreach ($submission in ($submissions | Where-Object { -not $_.Stats })) {
        $analysis = Invoke-VirusTotal -Method Get -Uri "$ApiBaseUrl/analyses/$($submission.AnalysisId)"

        if ($analysis.data.attributes.status -eq "completed") {
            $submission.Stats = $analysis.data.attributes.stats
            Write-Host "$($submission.Name): $($submission.Stats.malicious) malicious, $($submission.Stats.suspicious) suspicious"
        }
    }
}

# 3. Ergebnis schreiben
$rows = foreach ($submission in $submissions) {
    $stats = $submission.Stats
    $detections = [int]$stats.malicious + [int]$stats.suspicious
    # Engines mit Ergebnis, ohne nicht unterstützte Dateitypen, Timeouts und Fehler
    $engines = $detections + [int]$stats.undetected + [int]$stats.harmless

    [pscustomobject]@{
        Name       = $submission.Name
        Detections = $detections
        Engines    = $engines
        ReportUrl  = "https://www.virustotal.com/gui/file/$($submission.Sha256)"
    }
}

$markdown = @(
    "## VirusTotal"
    ""
    "| File | Detections | Report |"
    "|---|---|---|"
    foreach ($row in $rows) { "| ``$($row.Name)`` | $($row.Detections) / $($row.Engines) | [Report]($($row.ReportUrl)) |" }
    ""
    "Unsigned .NET applications are sometimes reported by a few engines as a false positive."
)

Set-Content -Path $MarkdownPath -Value ($markdown -join "`n") -Encoding utf8NoBOM

# shields.io Endpoint-Badge: grün ohne Erkennungen, orange bei 1-3 (meist Fehlalarme), sonst rot
$maxDetections = ($rows | Measure-Object -Property Detections -Maximum).Maximum

$badge = [ordered]@{
    schemaVersion = 1
    label         = "VirusTotal"
    message       = if ($maxDetections -eq 0) { "clean" } elseif ($maxDetections -eq 1) { "1 detection" } else { "$maxDetections detections" }
    color         = if ($maxDetections -eq 0) { "brightgreen" } elseif ($maxDetections -le 3) { "orange" } else { "red" }
}

Set-Content -Path $BadgePath -Value ($badge | ConvertTo-Json) -Encoding utf8NoBOM

Write-Host "Result: at most $maxDetections detection(s) per file"
