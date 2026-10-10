[CmdletBinding()]
param([string] $Version)
. "$PSScriptRoot/Release.Common.ps1"
$Version = Get-ReleaseVersion $Version
$directory = Join-Path $ArtifactsRoot "msi-trial/$Version"
$provenance = Get-Content (Join-Path $directory 'provenance.json') -Raw | ConvertFrom-Json
$msi = Join-Path $directory "PaneShift-$Version-MSI-Trial-x64.msi"
$hash = (Get-FileHash -LiteralPath $msi -Algorithm SHA256).Hash.ToLowerInvariant()
if ($hash -ne $provenance.msiSha256 -or "$hash  $(Split-Path $msi -Leaf)" -cnotin @(Get-Content (Join-Path $directory 'SHA256SUMS.txt'))) {
    throw 'MSI checksum mismatch.'
}
if ((Get-FileHash -LiteralPath $provenance.sourceZip -Algorithm SHA256).Hash.ToLowerInvariant() -ne $provenance.sourceZipSha256) {
    throw 'Source ZIP checksum mismatch.'
}
$installer = New-Object -ComObject WindowsInstaller.Installer
$database = $installer.OpenDatabase($msi, 0)
function Read-TrialRows([string] $Query, [int] $Columns) {
    $view = $database.OpenView($Query)
    try {
        $null = $view.Execute()
        while ($record = $view.Fetch()) {
            try {
                $row = for ($column = 1; $column -le $Columns; $column++) { $record.StringData($column) }
                ,$row
            } finally { $null = [Runtime.InteropServices.Marshal]::FinalReleaseComObject($record) }
        }
    } finally {
        $null = $view.Close()
        $null = [Runtime.InteropServices.Marshal]::FinalReleaseComObject($view)
    }
}
try {
    $properties = @{}
    foreach ($row in @(Read-TrialRows 'SELECT `Property`, `Value` FROM `Property`' 2)) { $properties[$row[0]] = $row[1] }
    if ($properties['ProductName'] -ne 'PaneShift (MSI trial)' -or $properties['ProductVersion'] -ne $Version -or
        $properties['Manufacturer'] -ne 'Kenan Kasumović' -or $properties['ALLUSERS']) { throw 'Unexpected MSI product/scope metadata.' }
    if ($properties['UpgradeCode'] -ne '{B7F47094-9D10-44FD-A68B-B4DB9AE77D38}') { throw 'Unexpected trial upgrade identity.' }
    $summary = $database.SummaryInformation(0)
    try {
        if ($summary.Property(7) -ne 'x64;1033' -or -not ([int]$summary.Property(15) -band 8)) {
            throw 'Expected x64 package with no elevation requirement.'
        }
    } finally { $null = [Runtime.InteropServices.Marshal]::FinalReleaseComObject($summary) }
    $files = @(Read-TrialRows 'SELECT `File` FROM `File`' 1)
    if ($files.Count -ne $provenance.payloadFiles) { throw 'MSI file count mismatch.' }
    foreach ($row in @(Read-TrialRows 'SELECT `Root`, `Key` FROM `Registry`' 2)) {
        if ($row[0] -ne '1' -or -not $row[1].StartsWith('Software\PaneShift\MsiTrial', [StringComparison]::Ordinal)) {
            throw 'MSI may only own trial-specific HKCU registry markers.'
        }
    }
    $folders = @{}
    foreach ($row in @(Read-TrialRows 'SELECT `Directory`, `Directory_Parent`, `DefaultDir` FROM `Directory`' 3)) { $folders[$row[0]] = $row }
    $installFolderName = ($folders['INSTALLFOLDER'][2] -split '\|')[-1]
    if ($installFolderName -ne 'PaneShift-MSI-Trial' -or $folders['TrialProgramsFolder'][1] -ne 'LocalAppDataFolder') {
        throw 'Unexpected trial install directory.'
    }
    [pscustomobject]@{ ProductCode = $properties['ProductCode']; Version = $Version; Files = $files.Count;
        PerUser = $true; Msi = $msi; Sha256 = $hash; Signed = (Get-AuthenticodeSignature -LiteralPath $msi).Status }
} finally {
    $null = [Runtime.InteropServices.Marshal]::FinalReleaseComObject($database)
    $null = [Runtime.InteropServices.Marshal]::FinalReleaseComObject($installer)
}
