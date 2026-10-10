[CmdletBinding()]
param(
    [string] $Version = '0.2.0',
    [string] $ZipPath,
    [string] $ExpectedZipSha256
)
. "$PSScriptRoot/Release.Common.ps1"
$Version = Get-ReleaseVersion $Version
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'The MSI trial requires a three-part numeric version.' }
$parts = $Version.Split('.')
if ([int]$parts[0] -gt 255 -or [int]$parts[1] -gt 255) { throw 'MSI major/minor version must not exceed 255.' }
if (-not $ZipPath) {
    if ($Version -ne '0.2.0') { throw 'Supply -ZipPath and -ExpectedZipSha256 for another version.' }
    $ZipPath = Join-Path $ArtifactsRoot 'github-v0.2.0/PaneShift-0.2.0-win-x64.zip'
    if (-not $ExpectedZipSha256) { $ExpectedZipSha256 = 'c79397d3215b9fd31a52243acb6b0da1db1584739d93b50e917487774ee2f306' }
}
if ($ExpectedZipSha256 -notmatch '^[a-fA-F0-9]{64}$') { throw 'Supply the verified ZIP SHA-256.' }
$ZipPath = (Resolve-Path -LiteralPath $ZipPath).Path
$zipHash = (Get-FileHash -LiteralPath $ZipPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($zipHash -ne $ExpectedZipSha256.ToLowerInvariant()) { throw 'Source ZIP SHA-256 mismatch.' }

$outputDirectory = Join-Path $ArtifactsRoot "msi-trial/$Version"
$workDirectory = Join-Path $outputDirectory 'build'
Reset-ArtifactDirectory $workDirectory
$payload = Join-Path $workDirectory 'payload'
New-Item -ItemType Directory -Path $payload | Out-Null
$archive = [IO.Compression.ZipFile]::OpenRead($ZipPath)
try {
    foreach ($entry in $archive.Entries) {
        $destination = [IO.Path]::GetFullPath((Join-Path $payload $entry.FullName))
        if (-not $destination.StartsWith($payload + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Unsafe ZIP entry.'
        }
    }
} finally { $archive.Dispose() }
[IO.Compression.ZipFile]::ExtractToDirectory($ZipPath, $payload)
$files = @(Get-ChildItem -LiteralPath $payload -Recurse -File | Sort-Object FullName)
foreach ($required in @('PaneShift.App.exe', 'PaneShift.App.dll', 'coreclr.dll', 'hostfxr.dll', 'LICENSE.txt')) {
    if (-not (Test-Path -LiteralPath (Join-Path $payload $required) -PathType Leaf)) { throw "Missing production file: $required" }
}
foreach ($file in $files) {
    if ($file.Extension -in @('.pdb', '.cs', '.csproj', '.pfx', '.key')) { throw "Unexpected payload file: $($file.Name)" }
}
$metadata = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $payload 'PaneShift.App.exe'))
if ($metadata.ProductVersion -ne $Version) { throw 'Source application version mismatch.' }

. "$PSScriptRoot/Msi.Common.ps1"
$msi = Join-Path $outputDirectory "PaneShift-$Version-MSI-Trial-x64.msi"
Invoke-MsiPackage -Version $Version -Payload $payload -WorkDirectory $workDirectory -Msi $msi -Authoring 'installer/MsiTrial.wxs' -Identity 'MsiTrial'
$toolVersion = & (Join-Path $ArtifactsRoot 'tools/wix/wix.exe') --version
$msiHash = (Get-FileHash -LiteralPath $msi -Algorithm SHA256).Hash.ToLowerInvariant()
"$msiHash  $(Split-Path $msi -Leaf)" | Set-Content (Join-Path $outputDirectory 'SHA256SUMS.txt') -Encoding utf8NoBOM
[ordered]@{ version = $Version; wixVersion = $toolVersion; sourceZip = $ZipPath; sourceZipSha256 = $zipHash;
    payloadFiles = $files.Count; msi = $msi; msiSha256 = $msiHash; trial = $true } |
    ConvertTo-Json | Set-Content (Join-Path $outputDirectory 'provenance.json') -Encoding utf8NoBOM
Write-Host "Built unsigned MSI trial: $msi ($($files.Count) unchanged ZIP files)."
