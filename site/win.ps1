[CmdletBinding(SupportsShouldProcess)]
param(
    [switch] $NoLaunch,
    [switch] $Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$repository = 'Ken5998/PaneShift'
$releaseApi = "https://api.github.com/repos/$repository/releases/latest"
$installedExe = Join-Path $env:LOCALAPPDATA 'Programs\PaneShift\PaneShift.App.exe'
$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) ('PaneShift-install-' + [Guid]::NewGuid().ToString('N'))
$stoppedRunningInstance = $false

function Get-TrustedAssetUri([string] $Value, [string] $ExpectedName, [string] $ExpectedTag) {
    $uri = [Uri] $Value
    $expectedPath = "/$repository/releases/download/$ExpectedTag/$ExpectedName"
    if ($uri.Scheme -ne 'https' -or $uri.Host -ne 'github.com' -or $uri.AbsolutePath -cne $expectedPath) {
        throw "Unexpected download URL for $ExpectedName."
    }
    return $uri.AbsoluteUri
}

function Get-ProductVersion([string] $Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    return [Diagnostics.FileVersionInfo]::GetVersionInfo($Path).ProductVersion
}

try {
    if (-not [Environment]::Is64BitOperatingSystem -or [Environment]::OSVersion.Version -lt [Version]'10.0.22000') {
        throw 'PaneShift requires Windows 11 x64.'
    }

    Write-Host 'Finding the latest PaneShift release...'
    $headers = @{ Accept = 'application/vnd.github+json'; 'User-Agent' = 'PaneShift-Installer' }
    $release = Invoke-RestMethod -Uri $releaseApi -Headers $headers
    $tag = [string] $release.tag_name
    if ($release.draft -or $release.prerelease -or $tag -cnotmatch '^v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') {
        throw "Unexpected latest release tag: $tag"
    }

    $version = $tag.Substring(1)
    $installerName = "PaneShift-$version-Setup-x64.exe"
    $installerAsset = @($release.assets | Where-Object name -CEQ $installerName)
    $checksumAsset = @($release.assets | Where-Object name -CEQ 'SHA256SUMS.txt')
    if ($installerAsset.Count -ne 1 -or $checksumAsset.Count -ne 1) {
        throw 'The latest release does not contain exactly one installer and checksum file.'
    }

    $installerUri = Get-TrustedAssetUri $installerAsset[0].browser_download_url $installerName $tag
    $checksumUri = Get-TrustedAssetUri $checksumAsset[0].browser_download_url 'SHA256SUMS.txt' $tag
    $installedVersion = Get-ProductVersion $installedExe
    $running = @(Get-Process -Name 'PaneShift.App' -ErrorAction SilentlyContinue)

    if (-not $Force -and $installedVersion -eq $version) {
        Write-Host "PaneShift $version is already installed."
        if (-not $NoLaunch -and $running.Count -eq 0) {
            if ($PSCmdlet.ShouldProcess($installedExe, 'Launch PaneShift')) { Start-Process -FilePath $installedExe }
        }
        return
    }

    [void] [IO.Directory]::CreateDirectory($temporaryDirectory)
    $installerPath = Join-Path $temporaryDirectory $installerName
    $checksumPath = Join-Path $temporaryDirectory 'SHA256SUMS.txt'
    Write-Host "Downloading PaneShift $version..."
    Invoke-WebRequest -Uri $installerUri -Headers $headers -OutFile $installerPath -UseBasicParsing
    Invoke-WebRequest -Uri $checksumUri -Headers $headers -OutFile $checksumPath -UseBasicParsing

    $escapedName = [regex]::Escape($installerName)
    $checksumLines = @(Get-Content -LiteralPath $checksumPath | Where-Object { $_ -match "^[0-9a-fA-F]{64}  $escapedName$" })
    if ($checksumLines.Count -ne 1) { throw "No unique SHA-256 checksum was found for $installerName." }
    $expectedHash = [regex]::Match($checksumLines[0], '^[0-9a-fA-F]{64}').Value
    $actualHash = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash
    if ($actualHash -cne $expectedHash.ToUpperInvariant()) { throw 'Installer SHA-256 verification failed.' }
    Write-Host 'SHA-256 verified.'

    if (-not $PSCmdlet.ShouldProcess("PaneShift $version", 'Install or update')) { return }

    if ($running.Count -gt 0) {
        Write-Host 'Closing the running PaneShift instance...'
        try { $running | Stop-Process -ErrorAction Stop }
        catch { throw 'PaneShift could not be closed. Exit it from the tray, or rerun this command from an elevated terminal if it is running as administrator.' }
        foreach ($process in $running) {
            try {
                $process.WaitForExit(10000)
                if (-not $process.HasExited) { throw 'PaneShift did not exit within 10 seconds.' }
            } catch [InvalidOperationException] { }
        }
        $stoppedRunningInstance = $true
    }

    $arguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', '/TASKS="startmenu"')
    $setup = Start-Process -FilePath $installerPath -ArgumentList $arguments -Wait -PassThru
    if ($setup.ExitCode -ne 0) { throw "PaneShift Setup exited with code $($setup.ExitCode)." }
    if ((Get-ProductVersion $installedExe) -ne $version) { throw 'The installed PaneShift version could not be verified.' }

    Write-Host "PaneShift $version installed successfully."
    if (-not $NoLaunch) {
        Start-Process -FilePath $installedExe
        Write-Host 'PaneShift is running in the notification area.'
    }
} catch {
    if ($stoppedRunningInstance -and -not $NoLaunch -and (Test-Path -LiteralPath $installedExe -PathType Leaf)) {
        try { Start-Process -FilePath $installedExe }
        catch { Write-Warning 'The previous PaneShift installation could not be restarted after the update failed.' }
    }
    throw
} finally {
    $fullTemporaryDirectory = [IO.Path]::GetFullPath($temporaryDirectory)
    $fullTempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if ($fullTemporaryDirectory.StartsWith($fullTempRoot, [StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $fullTemporaryDirectory)) {
        [IO.Directory]::Delete($fullTemporaryDirectory, $true)
    }
}
