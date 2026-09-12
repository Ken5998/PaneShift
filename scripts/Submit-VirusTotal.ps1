[CmdletBinding()]
param([Parameter(Mandatory)][string] $Version)
. "$PSScriptRoot/Release.Common.ps1"
$Version = Get-ReleaseVersion $Version
$directory = Join-Path $ArtifactsRoot 'release'
$notesPath = Join-Path $directory 'virustotal.md'
$notes = [Collections.Generic.List[string]]::new()
$notes.Add("`n## VirusTotal`n")
if ([string]::IsNullOrWhiteSpace($env:VT_API_KEY)) {
    $notes.Add('Not submitted: the optional VirusTotal API key is not configured.')
    $notes | Set-Content -LiteralPath $notesPath -Encoding utf8NoBOM
    return
}
$notes.Add('These artifacts were submitted as public samples. Reports are informational; false positives are possible.')
$headers = @{ 'x-apikey' = $env:VT_API_KEY }
$script:lastRequest = [DateTime]::MinValue
function Invoke-VirusTotal([string] $Uri, [string] $Method = 'Get', [IO.FileInfo] $File) {
    $uriObject = [Uri] $Uri
    if ($uriObject.Scheme -ne 'https' -or ($uriObject.Host -ne 'virustotal.com' -and -not $uriObject.Host.EndsWith('.virustotal.com'))) {
        throw 'Unexpected VirusTotal upload host.'
    }
    for ($attempt = 0; $attempt -lt 3; $attempt++) {
        $delay = 16 - ([DateTime]::UtcNow - $script:lastRequest).TotalSeconds
        if ($delay -gt 0) { Start-Sleep -Milliseconds ([int][Math]::Ceiling($delay * 1000)) }
        $script:lastRequest = [DateTime]::UtcNow
        try {
            $parameters = @{ Uri = $Uri; Method = $Method; Headers = $headers; TimeoutSec = 180; MaximumRedirection = 0 }
            if ($File) { $parameters.Form = @{ file = $File } }
            return Invoke-RestMethod @parameters
        } catch {
            $status = if ($_.Exception.Response) { [int] $_.Exception.Response.StatusCode } else { 0 }
            if ($status -eq 429 -and $attempt -lt 2) { Start-Sleep -Seconds 60; continue }
            # Never include request headers, the key, or raw exception details in logs.
            throw "VirusTotal request unavailable (HTTP $status)."
        }
    }
}
$results = [Collections.Generic.List[object]]::new()
foreach ($name in @("PaneShift-$Version-Setup-x64.exe", "PaneShift-$Version-win-x64.zip")) {
    try {
        $file = Get-Item -LiteralPath (Join-Path $directory $name)
        $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $checksum = "$hash  $name"
        if ($checksum -cnotin @(Get-Content (Join-Path $directory 'SHA256SUMS.txt'))) { throw 'Final artifact checksum mismatch.' }
        if ($file.Length -gt 650MB) { throw 'Artifact exceeds the VirusTotal upload limit.' }
        $uploadUrl = 'https://www.virustotal.com/api/v3/files'
        if ($file.Length -gt 32MB) { $uploadUrl = (Invoke-VirusTotal 'https://www.virustotal.com/api/v3/files/upload_url').data }
        $uploaded = Invoke-VirusTotal $uploadUrl 'Post' $file
        $analysisId = $uploaded.data.id
        if (-not $analysisId) { throw 'VirusTotal returned no analysis ID.' }
        $url = "https://www.virustotal.com/gui/file/$hash/detection"
        $result = [ordered]@{ name = $name; sha256 = $hash; analysisId = $analysisId; url = $url; status = 'queued' }
        $notes.Add("- [$name]($url) — submitted; analysis ID: ``$analysisId``.")
        # Bounded polling. A queued report is still useful; never invent detection counts.
        try {
            for ($poll = 0; $poll -lt 3; $poll++) {
                $analysis = Invoke-VirusTotal "https://www.virustotal.com/api/v3/analyses/$([Uri]::EscapeDataString($analysisId))"
                $result.status = $analysis.data.attributes.status
                if ($result.status -eq 'completed') {
                    $result.stats = $analysis.data.attributes.stats
                    $notes.Add("  Analysis completed: $($result.stats.malicious) malicious, $($result.stats.suspicious) suspicious engine results. See report for context.")
                    break
                }
            }
        } catch { $notes.Add('  Analysis status unavailable; follow the report link later.') }
        $results.Add($result)
    } catch {
        $notes.Add("- ${name}: submission unavailable. No clean-scan claim is made; retry manually if needed.")
        Write-Warning "VirusTotal submission unavailable for $name. Release artifacts are unchanged."
    }
}
$results | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $directory 'virustotal-results.json') -Encoding utf8NoBOM
$notes | Set-Content -LiteralPath $notesPath -Encoding utf8NoBOM
