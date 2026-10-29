<#
.SYNOPSIS
    Checks the release files with VirusTotal (used by the release workflow).

.DESCRIPTION
    Uploads the given files to VirusTotal (API v3), waits until all analyses are completed and writes:
      - a Markdown section with the result and a link to the report of every file (for the release notes)
      - a JSON file for a shields.io endpoint badge (attached to the release as virustotal-badge.json)

    With -ReleaseTag, the Markdown section is added to the notes of that GitHub release (an existing VirusTotal
    section is replaced) and the badge file is attached to the release. If it is the latest release, the badge file
    is also stored in the branch -BadgeBranch, where the README badge reads it. This needs the GitHub CLI with
    GH_TOKEN and GH_REPO.

    The API key is read from the environment variable VIRUSTOTAL_API_KEY.
    The free public API allows 4 requests per minute, so the script waits between requests.

.EXAMPLE
    .\tools\Submit-VirusTotal.ps1 -Path ZabbixTrayMonitor-v1.3.5-win-x64.msi -MarkdownPath virustotal.md -BadgePath virustotal-badge.json -ReleaseTag v1.3.5
#>
param(
    [Parameter(Mandatory = $true)]
    [string[]] $Path,

    [Parameter(Mandatory = $true)]
    [string] $MarkdownPath,

    [Parameter(Mandatory = $true)]
    [string] $BadgePath,

    # GitHub-Release, dessen Beschreibung und Dateien aktualisiert werden (leer = nur Dateien schreiben)
    [string] $ReleaseTag,

    # Branch mit virustotal-badge.json für das Badge in der README (nur mit -ReleaseTag)
    [string] $BadgeBranch = "badges",

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

# 4. Release aktualisieren
if ($ReleaseTag) {
    $notes = gh release view $ReleaseTag --json body --jq .body
    if ($LASTEXITCODE -ne 0) { throw "Reading the release notes of $ReleaseTag failed" }

    # Vorherigen VirusTotal-Abschnitt (bei erneuter Prüfung) entfernen, er steht immer am Ende
    $notes = ($notes -join "`n") -replace "(?s)\s*## VirusTotal\n.*$", ""

    $notesPath = Join-Path ([IO.Path]::GetTempPath()) "release-notes-$([guid]::NewGuid()).md"
    Set-Content -Path $notesPath -Value (($notes.TrimEnd(), ($markdown -join "`n")) -join "`n`n") -Encoding utf8NoBOM

    try {
        gh release edit $ReleaseTag --notes-file $notesPath | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Updating the release notes of $ReleaseTag failed" }
    }
    finally {
        Remove-Item $notesPath -ErrorAction SilentlyContinue
    }

    gh release upload $ReleaseTag $BadgePath --clobber
    if ($LASTEXITCODE -ne 0) { throw "Uploading $BadgePath to $ReleaseTag failed" }

    Write-Host "Release $ReleaseTag updated"

    # Badge-Datei für die README in den Branch $BadgeBranch (shields.io lädt keine Dateien von github.com/.../releases,
    # aber von raw.githubusercontent.com). Der Branch hat immer genau einen Commit ohne Vorgänger und wird nur für das
    # neueste Release aktualisiert, damit die Prüfung eines älteren Releases das Badge nicht überschreibt.
    $latestTag = gh api "repos/{owner}/{repo}/releases/latest" --jq .tag_name
    if ($LASTEXITCODE -ne 0) { throw "Reading the latest release failed" }

    if ($latestTag -ne $ReleaseTag) {
        Write-Host "$ReleaseTag is not the latest release ($latestTag), badge branch not updated"
        return
    }

    $blob = gh api "repos/{owner}/{repo}/git/blobs" -f "content=$(Get-Content $BadgePath -Raw)" -f encoding=utf-8 --jq .sha
    if ($LASTEXITCODE -ne 0) { throw "Creating the badge blob failed" }

    $tree = gh api "repos/{owner}/{repo}/git/trees" `
        -f "tree[][path]=virustotal-badge.json" -f "tree[][mode]=100644" -f "tree[][type]=blob" -f "tree[][sha]=$blob" --jq .sha
    if ($LASTEXITCODE -ne 0) { throw "Creating the badge tree failed" }

    $commit = gh api "repos/{owner}/{repo}/git/commits" -f "message=VirusTotal result of $ReleaseTag" -f "tree=$tree" --jq .sha
    if ($LASTEXITCODE -ne 0) { throw "Creating the badge commit failed" }

    gh api -X PATCH "repos/{owner}/{repo}/git/refs/heads/$BadgeBranch" -f "sha=$commit" -F force=true --silent 2>$null

    if ($LASTEXITCODE -ne 0) {
        # Branch existiert noch nicht
        gh api "repos/{owner}/{repo}/git/refs" -f "ref=refs/heads/$BadgeBranch" -f "sha=$commit" --silent
        if ($LASTEXITCODE -ne 0) { throw "Updating the branch $BadgeBranch failed" }
    }

    Write-Host "Badge branch $BadgeBranch updated"
}
