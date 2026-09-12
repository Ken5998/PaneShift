[CmdletBinding()]
param([Parameter(Mandatory)][string] $Version, [Parameter(Mandatory)][string] $Repository)
. "$PSScriptRoot/Release.Common.ps1"
$Version = Get-ReleaseVersion $Version
if ($Repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') { throw 'Invalid GitHub repository.' }
$directory = Join-Path $ArtifactsRoot 'release'
$expectedNames = @("PaneShift-$Version-Setup-x64.exe", "PaneShift-$Version-win-x64.zip")
$checksums = @(Get-Content (Join-Path $directory 'SHA256SUMS.txt'))
if ($checksums.Count -ne 2) { throw 'Expected exactly two release checksums.' }
foreach ($name in $expectedNames) {
    $actual = (Get-FileHash -LiteralPath (Join-Path $directory $name) -Algorithm SHA256).Hash.ToLowerInvariant()
    if ("$actual  $name" -cnotin $checksums) { throw "Release checksum mismatch: $name" }
}
$signing = (Get-Content (Join-Path $directory 'signing-status.txt') -Raw).Trim()
$tag = "v$Version"
$encodedTag = [Uri]::EscapeDataString($tag)
$base = "https://github.com/$Repository/releases/download/$encodedTag"
$notes = @"
# PaneShift $tag

## Downloads

- [Installer]($base/PaneShift-$Version-Setup-x64.exe) — installs for the current user.
- [Portable ZIP]($base/PaneShift-$Version-win-x64.zip) — extract the entire directory, then run PaneShift.App.exe.

## Security

- Authenticode: $signing
- [SHA-256 checksums]($base/SHA256SUMS.txt)
- [Code signing policy](https://github.com/$Repository/blob/$encodedTag/docs/CODE_SIGNING.md)

## Requirements

Windows 11 x64. The .NET runtime is included. Exit any running copy before installing or switching between installed and portable builds.

## Changes

"@
$generatedJson = & gh api --method POST "repos/$Repository/releases/generate-notes" -f "tag_name=$tag"
if ($LASTEXITCODE -ne 0) { throw 'Could not generate release changelog.' }
$generated = ($generatedJson | ConvertFrom-Json).body
$notes += $generated
$notesPath = Join-Path $directory 'release-notes.md'
$notes | Set-Content -LiteralPath $notesPath -Encoding utf8NoBOM
$assets = @($expectedNames | ForEach-Object { Join-Path $directory $_ }) + (Join-Path $directory 'SHA256SUMS.txt')
$options = @('release', 'create', $tag, '--repo', $Repository, '--verify-tag', '--draft', '--title', "PaneShift $tag", '--notes-file', $notesPath)
if (($Version -split '\+')[0].Contains('-')) { $options += '--prerelease' }
# An existing draft/public release fails safely; no automatic asset replacement.
& gh @options @assets
if ($LASTEXITCODE -ne 0) { throw 'Draft release creation failed. Inspect any existing release before retrying.' }
