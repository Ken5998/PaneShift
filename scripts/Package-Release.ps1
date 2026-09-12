[CmdletBinding()]
param([string] $Version, [string] $IsccPath = $env:ISCC_PATH)
. "$PSScriptRoot/Release.Common.ps1"
$Version = Get-ReleaseVersion $Version
$publish = Get-PublishDirectory $Version
& "$PSScriptRoot/Verify-Release.ps1" -Version $Version -PublishOnly
if (-not $IsccPath) {
    $candidates = @("${env:ProgramFiles(x86)}/Inno Setup 6/ISCC.exe", "$env:ProgramFiles/Inno Setup 7/ISCC.exe")
    $IsccPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $IsccPath -or -not (Test-Path -LiteralPath $IsccPath)) {
    throw 'Install Inno Setup 6.7+ or pass -IsccPath / set ISCC_PATH. See docs/RELEASING.md.'
}
$installerDirectory = Join-Path $ArtifactsRoot 'installer'
New-Item -ItemType Directory -Path $installerDirectory -Force | Out-Null
$numericVersion = ($Version -split '[-+]')[0] + '.0'
& $IsccPath "/DAppVersion=$Version" "/DFileVersion=$numericVersion" "/DPublishDir=$publish" "/DArtifactDir=$installerDirectory" (Join-Path $RepoRoot 'installer/PaneShift.iss')
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }
$zip = Join-Path $ArtifactsRoot "portable/PaneShift-$Version-win-x64.zip"
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip }
[IO.Compression.ZipFile]::CreateFromDirectory($publish, $zip, [IO.Compression.CompressionLevel]::Optimal, $false)
Write-Host "Packaged $zip and installer. Sign the installer, if configured, before final verification/checksums."
