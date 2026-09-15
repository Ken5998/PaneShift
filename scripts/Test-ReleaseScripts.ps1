# Dependency-free tests of the release scripts. No signing, network, installation or upload.
. "$PSScriptRoot/Release.Common.ps1"
foreach ($file in Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.ps1') {
    $parseTokens = $null; $parseErrors = $null
    $null = [Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref] $parseTokens, [ref] $parseErrors)
    if ($parseErrors.Count) { throw "PowerShell syntax error in $($file.Name): $parseErrors" }
}
$valid = @('0.1.0', '0.1.1', '0.2.0', '1.0.0-rc.1', '1.0.0-alpha+build.42', '65534.0.0')
foreach ($version in $valid) {
    if ((Get-ReleaseVersion $version) -cne $version) { throw "Version not preserved: $version" }
}
$invalid = @('v0.1.0', '01.1.0', '1.0', '1.0.0-01', '1.0.0-', '1.0.0+', '1.0.0/x', '65535.0.0', '1.0.0;exit', "1.0.0`n")
foreach ($version in $invalid) {
    $rejected = $false
    try { $null = Get-ReleaseVersion $version } catch { $rejected = $true }
    if (-not $rejected) { throw "Invalid version accepted: $version" }
}
$rejected = $false
try { Reset-ArtifactDirectory $RepoRoot } catch { $rejected = $true }
if (-not $rejected) { throw 'Unsafe artifact cleanup was accepted.' }
Write-Host "Release script syntax, $($valid.Count + $invalid.Count) version cases, and cleanup boundary passed."
. "$PSScriptRoot/VirusTotal.Multipart.ps1"
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('paneshift-multipart-' + [Guid]::NewGuid().ToString('N') + '.bin')
try {
    [IO.File]::WriteAllBytes($fixture, [byte[]](0, 1, 13, 10, 127, 128, 255))
    $multipart = New-VirusTotalMultipart (Get-Item $fixture)
    try {
        $header = $multipart.Headers.ContentType.ToString()
        if ($header -notmatch '^multipart/form-data; boundary=PaneShift[a-f0-9]+$') { throw 'Unexpected multipart boundary header.' }
        $bytes = $multipart.ReadAsByteArrayAsync().GetAwaiter().GetResult()
        $wire = [Text.Encoding]::Latin1.GetString($bytes)
        if ($wire -notmatch 'name="file"; filename="paneshift-multipart-' -or $wire -match 'filename\*') { throw 'Unexpected multipart disposition.' }
        if (-not $wire.Contains([Text.Encoding]::Latin1.GetString([IO.File]::ReadAllBytes($fixture)))) { throw 'Multipart payload changed file bytes.' }
    } finally { $multipart.Dispose() }
} finally { if (Test-Path -LiteralPath $fixture) { Remove-Item -LiteralPath $fixture } }
Write-Host 'VirusTotal multipart headers and binary payload passed.'
