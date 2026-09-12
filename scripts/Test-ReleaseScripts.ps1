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
