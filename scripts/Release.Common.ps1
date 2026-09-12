Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:RepoRoot = Split-Path $PSScriptRoot -Parent
$script:ArtifactsRoot = Join-Path $RepoRoot 'artifacts'

function Get-ReleaseVersion([string] $Version) {
    if (-not $Version) {
        [xml] $properties = Get-Content (Join-Path $RepoRoot 'Directory.Build.props') -Raw
        $Version = [string] $properties.Project.PropertyGroup.Version.'#text'
    }
    # SemVer 2.0, including prerelease identifiers without numeric leading zeros.
    $pattern = '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-((?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*))*))?(?:\+([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?\z'
    if ($Version -cnotmatch $pattern) { throw "Invalid semantic version: $Version" }
    foreach ($component in ($Version -split '[-+]')[0].Split('.')) {
        if ([long] $component -gt 65534) { throw 'Windows version components must not exceed 65534.' }
    }
    return $Version
}

function Get-PublishDirectory([string] $Version) {
    return Join-Path $ArtifactsRoot "portable/PaneShift-$Version-win-x64"
}

function Reset-ArtifactDirectory([string] $Path) {
    $full = [IO.Path]::GetFullPath($Path)
    $root = [IO.Path]::GetFullPath($ArtifactsRoot)
    if (-not $full.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean outside artifacts: $full"
    }
    # Reject junctions/symlinks on the target and its ancestors before recursive deletion.
    for ($probe = $full; $probe.Length -ge $root.Length; $probe = Split-Path $probe -Parent) {
        if ((Test-Path -LiteralPath $probe) -and ((Get-Item -LiteralPath $probe).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Refusing to clean a redirected artifact directory: $probe"
        }
    }
    if (Test-Path -LiteralPath $full) { Remove-Item -LiteralPath $full -Recurse -Force }
    New-Item -ItemType Directory -Path $full -Force | Out-Null
}

function Assert-Authenticode([string] $Path) {
    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne 'Valid' -or -not $signature.TimeStamperCertificate) {
        throw "A valid timestamped Authenticode signature is required: $Path ($($signature.Status))"
    }
}
