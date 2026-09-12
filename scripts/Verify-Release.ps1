[CmdletBinding()]
param([string] $Version, [switch] $PublishOnly, [switch] $RequireSigned)
. "$PSScriptRoot/Release.Common.ps1"
$Version = Get-ReleaseVersion $Version
$publish = Get-PublishDirectory $Version
$exe = Join-Path $publish 'PaneShift.App.exe'
foreach ($required in @('PaneShift.App.exe', 'PaneShift.App.dll', 'PaneShift.Core.dll', 'PaneShift.Windows.dll',
    'PaneShift.App.runtimeconfig.json', 'PaneShift.App.deps.json', 'coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll',
    'PresentationFramework.dll', 'wpfgfx_cor3.dll', 'LICENSE.txt', 'licenses/Microsoft.NETCore.App/LICENSE.TXT',
    'licenses/Microsoft.NETCore.App/THIRD-PARTY-NOTICES.TXT', 'licenses/Microsoft.WindowsDesktop.App/LICENSE')) {
    if (-not (Test-Path -LiteralPath (Join-Path $publish $required) -PathType Leaf)) { throw "Missing production file: $required" }
}
$files = @(Get-ChildItem -LiteralPath $publish -Recurse -File)
foreach ($file in $files) {
    if ($file.Extension -in @('.pdb', '.cs', '.csproj', '.sln', '.iss', '.pfx', '.key') -or $file.Name -match '(?i)(testhost|\.Tests\.|xunit)') {
        throw "Unexpected development/private file: $($file.Name)"
    }
}
$metadata = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe)
$numericVersion = ($Version -split '[-+]')[0] + '.0'
if ($metadata.ProductName -ne 'PaneShift' -or $metadata.ProductVersion -ne $Version -or $metadata.FileVersion -ne $numericVersion -or
    $metadata.FileDescription -ne 'Keyboard-first window management for Windows' -or $metadata.CompanyName -ne 'Kenan Kasumović') {
    throw "Unexpected executable metadata: $($metadata | Out-String)"
}
$reader = [IO.BinaryReader]::new([IO.File]::OpenRead($exe))
try {
    $reader.BaseStream.Position = 0x3c
    $reader.BaseStream.Position = $reader.ReadInt32() + 4
    if ($reader.ReadUInt16() -ne 0x8664) { throw 'Published executable is not x64.' }
} finally { $reader.Dispose() }
$nativeText = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($exe))
foreach ($manifestSetting in @('PerMonitorV2', 'level="asInvoker"', 'uiAccess="false"')) {
    if (-not $nativeText.Contains($manifestSetting)) { throw "Published manifest is missing $manifestSetting" }
}
$runtime = Get-Content (Join-Path $publish 'PaneShift.App.runtimeconfig.json') -Raw | ConvertFrom-Json
if (-not $runtime.runtimeOptions.PSObject.Properties['includedFrameworks']) { throw 'Publish is not self-contained.' }
if ($RequireSigned) { Assert-Authenticode $exe }
if ($PublishOnly) { Write-Host "Verified self-contained win-x64 publish $Version ($($files.Count) files)."; return }

$zip = Join-Path $ArtifactsRoot "portable/PaneShift-$Version-win-x64.zip"
$setup = Join-Path $ArtifactsRoot "installer/PaneShift-$Version-Setup-x64.exe"
if (-not (Test-Path -LiteralPath $setup)) { throw 'Installer missing.' }
$installerMetadata = [Diagnostics.FileVersionInfo]::GetVersionInfo($setup)
if ($installerMetadata.ProductVersion.Trim() -ne $Version -or $installerMetadata.FileVersion.Trim() -ne $numericVersion) { throw 'Installer version mismatch.' }
if ($RequireSigned) { Assert-Authenticode $setup }
$archive = [IO.Compression.ZipFile]::OpenRead($zip)
try {
    $entries = @($archive.Entries | Where-Object { $_.Name })
    if ($entries.Count -ne $files.Count) { throw 'ZIP file count differs from publish directory.' }
    foreach ($entry in $entries) {
        $path = [IO.Path]::GetFullPath((Join-Path $publish $entry.FullName))
        if (-not $path.StartsWith($publish + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe ZIP path.' }
        $stream = $entry.Open()
        try { $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)) }
        finally { $stream.Dispose() }
        if ($hash -ne (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash) { throw "ZIP mismatch: $($entry.FullName)" }
    }
} finally { $archive.Dispose() }
$lines = foreach ($artifact in @($setup, $zip)) {
    '{0}  {1}' -f (Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash.ToLowerInvariant(), (Split-Path $artifact -Leaf)
}
$lines | Set-Content -LiteralPath (Join-Path $ArtifactsRoot 'SHA256SUMS.txt') -Encoding utf8NoBOM
$status = if ($RequireSigned) { 'Signed: application and installer have valid timestamped Authenticode signatures.' } else { 'Unsigned: PaneShift signing is not enabled for this build.' }
$status | Set-Content -LiteralPath (Join-Path $ArtifactsRoot 'signing-status.txt') -Encoding utf8NoBOM
Write-Host "Verified final artifacts and wrote SHA256SUMS.txt. $status"
