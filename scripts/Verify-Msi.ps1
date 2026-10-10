[CmdletBinding()]
param([string] $Version)
. "$PSScriptRoot/Release.Common.ps1"
$Version = Get-ReleaseVersion $Version
$msi = Join-Path $ArtifactsRoot ("installer/" + (Get-InstallerFileName $Version))
$installer = New-Object -ComObject WindowsInstaller.Installer
$database = $installer.OpenDatabase($msi, 0)
function Read-MsiRows([string] $Query, [int] $Columns) {
    $view = $database.OpenView($Query)
    try {
        $null = $view.Execute()
        while ($record = $view.Fetch()) {
            try { $row = for ($column = 1; $column -le $Columns; $column++) { $record.StringData($column) }; ,$row }
            finally { $null = [Runtime.InteropServices.Marshal]::FinalReleaseComObject($record) }
        }
    } finally { $null = $view.Close(); $null = [Runtime.InteropServices.Marshal]::FinalReleaseComObject($view) }
}
try {
    $properties = @{}
    foreach ($row in @(Read-MsiRows 'SELECT `Property`, `Value` FROM `Property`' 2)) { $properties[$row[0]] = $row[1] }
    if ($properties['ProductName'] -ne 'PaneShift' -or $properties['ProductVersion'] -ne $Version -or
        $properties['Manufacturer'] -ne 'Kenan Kasumović' -or $properties['ALLUSERS'] -or
        $properties['UpgradeCode'] -ne '{F7E15B50-EF68-4EB4-8889-C91277EA6B89}') { throw 'Unexpected MSI metadata/scope.' }
    $summary = $database.SummaryInformation(0)
    try { if ($summary.Property(7) -ne 'x64;1033' -or -not ([int]$summary.Property(15) -band 8)) { throw 'Expected x64 MSI without elevation requirement.' } }
    finally { $null = [Runtime.InteropServices.Marshal]::FinalReleaseComObject($summary) }
    $files = @(Read-MsiRows 'SELECT `File` FROM `File`' 1)
    if ($files.Count -ne @(Get-ChildItem (Get-PublishDirectory $Version) -Recurse -File).Count) { throw 'MSI payload count mismatch.' }
    foreach ($row in @(Read-MsiRows 'SELECT `Root`, `Key` FROM `Registry`' 2)) {
        if ($row[0] -ne '1' -or -not $row[1].StartsWith('Software\PaneShift\Installer', [StringComparison]::Ordinal)) { throw 'Unexpected MSI registry ownership.' }
    }
    $startupSearch = @(Read-MsiRows 'SELECT `Property`, `Signature_` FROM `AppSearch`' 2) | Where-Object { $_[0] -ceq 'STARTUP_COMMAND' }
    if (-not $startupSearch -or $startupSearch[1] -cne 'StartupCommandSearch' -or
        $properties['SecureCustomProperties'].Split(';') -cnotcontains 'STARTUP_COMMAND' -or
        $properties['MsiHiddenProperties'].Split(';') -cnotcontains 'STARTUP_COMMAND') { throw 'Missing secure/hidden native startup snapshot.' }
    $startupLocator = @(Read-MsiRows 'SELECT `Signature_`, `Root`, `Key`, `Name`, `Type` FROM `RegLocator`' 5) | Where-Object { $_[0] -ceq 'StartupCommandSearch' }
    if (-not $startupLocator -or $startupLocator[1] -ne '1' -or
        $startupLocator[2] -cne 'Software\Microsoft\Windows\CurrentVersion\Run' -or
        $startupLocator[3] -cne 'PaneShift' -or ([int]$startupLocator[4] -band 16) -eq 0) { throw 'Unexpected startup registry search.' }
    $actions = @{}
    foreach ($row in @(Read-MsiRows 'SELECT `Action`, `Type` FROM `CustomAction`' 2)) { $actions[$row[0]] = [int]$row[1] }
    foreach ($action in @('PrepareInstallation', 'RestoreStartup', 'RemoveStartup', 'MigrateLegacy')) {
        if (-not $actions.ContainsKey($action)) { throw "Missing MSI action: $action" }
        if ($actions[$action] -band 2048) { throw 'Custom actions must impersonate the installing user.' }
    }
    if (-not ($actions['RestoreStartup'] -band 256) -or -not ($actions['MigrateLegacy'] -band 512)) { throw 'Missing rollback/commit action scheduling.' }
    Write-Host "Verified per-user MSI $Version ($($files.Count) files, x64, conditional startup cleanup and commit-phase migration). ProductCode $($properties['ProductCode'])"
} finally { $null = [Runtime.InteropServices.Marshal]::FinalReleaseComObject($database); $null = [Runtime.InteropServices.Marshal]::FinalReleaseComObject($installer) }
