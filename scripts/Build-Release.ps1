[CmdletBinding()]
param([string] $Version, [string] $IsccPath = $env:ISCC_PATH)
. "$PSScriptRoot/Release.Common.ps1"
$Version = Get-ReleaseVersion $Version
$solution = Join-Path $RepoRoot 'PaneShift.sln'
& dotnet restore $solution
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
& dotnet build $solution -c Release --no-restore "-p:Version=$Version"
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
& dotnet test $solution -c Release --no-build --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
& "$PSScriptRoot/Publish-Release.ps1" -Version $Version
& "$PSScriptRoot/Package-Release.ps1" -Version $Version -IsccPath $IsccPath
& "$PSScriptRoot/Verify-Release.ps1" -Version $Version
