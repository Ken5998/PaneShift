[CmdletBinding()]
param([string] $Version)
. "$PSScriptRoot/Release.Common.ps1"
$Version = Get-ReleaseVersion $Version
$publish = Get-PublishDirectory $Version
Reset-ArtifactDirectory $publish
& dotnet publish (Join-Path $RepoRoot 'src/PaneShift.App/PaneShift.App.csproj') -c Release -r win-x64 --self-contained true -p:PublishProfile=Release-win-x64 "-p:Version=$Version" -p:DebugType=None -p:DebugSymbols=false -p:CopyOutputSymbolsToPublishDirectory=false -o $publish
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }
Copy-Item -LiteralPath (Join-Path $RepoRoot 'LICENSE') -Destination (Join-Path $publish 'LICENSE.txt')
# Runtime pack licenses are not automatically included by dotnet publish.
$assets = Get-Content (Join-Path $RepoRoot 'src/PaneShift.App/obj/project.assets.json') -Raw | ConvertFrom-Json
$runtime = Get-Content (Join-Path $publish 'PaneShift.App.runtimeconfig.json') -Raw | ConvertFrom-Json
foreach ($framework in $runtime.runtimeOptions.includedFrameworks) {
    $packageName = "$($framework.name.ToLowerInvariant()).runtime.win-x64"
    $pack = $null
    foreach ($packageRoot in $assets.packageFolders.PSObject.Properties.Name) {
        $candidate = Join-Path $packageRoot "$packageName/$($framework.version)"
        if (Test-Path -LiteralPath $candidate) { $pack = $candidate; break }
    }
    if (-not $pack) { throw "Cannot locate runtime license pack: $packageName $($framework.version)" }
    $notices = @(Get-ChildItem -LiteralPath $pack -File | Where-Object Name -Match '^(LICENSE(?:\..*)?|THIRD-PARTY-NOTICES(?:\..*)?)$')
    if (-not $notices.Count) { throw "Runtime license missing: $packageName" }
    $licenseDirectory = Join-Path $publish "licenses/$($framework.name)"
    New-Item -ItemType Directory -Path $licenseDirectory -Force | Out-Null
    foreach ($notice in $notices) { Copy-Item -LiteralPath $notice.FullName -Destination $licenseDirectory }
}
& "$PSScriptRoot/Verify-Release.ps1" -Version $Version -PublishOnly
Write-Host "Published $publish"
