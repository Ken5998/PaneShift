[CmdletBinding()]
param([string] $Version)
. "$PSScriptRoot/Release.Common.ps1"
. "$PSScriptRoot/Msi.Common.ps1"
$Version = Get-ReleaseVersion $Version
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'MSI releases require a stable three-part numeric version.' }
$parts = $Version.Split('.')
if ([int]$parts[0] -gt 255 -or [int]$parts[1] -gt 255) { throw 'MSI major/minor version must not exceed 255.' }
& "$PSScriptRoot/Verify-Release.ps1" -Version $Version -PublishOnly
$publish = Get-PublishDirectory $Version
$work = Join-Path $ArtifactsRoot "msi-build/$Version"
Reset-ArtifactDirectory $work
& dotnet build (Join-Path $RepoRoot 'installer/CustomActions/PaneShift.Installer.csproj') -c Release -p:Platform=x64 "-p:Version=$Version"
if ($LASTEXITCODE -ne 0) { throw 'Installer custom actions build failed.' }
$customActions = Join-Path $RepoRoot 'installer/CustomActions/bin/x64/Release/net48/PaneShift.Installer.CA.dll'
if (-not (Test-Path -LiteralPath $customActions)) { throw 'Installer custom action package missing.' }
$output = Join-Path $ArtifactsRoot "installer/PaneShift-$Version-Setup-x64.msi"
New-Item -ItemType Directory (Split-Path $output -Parent) -Force | Out-Null
Invoke-MsiPackage -Version $Version -Payload $publish -WorkDirectory $work -Msi $output -Authoring 'installer/PaneShift.wxs' -Identity 'Msi' -CustomActions $customActions
Write-Host "Built per-user x64 MSI: $output"
