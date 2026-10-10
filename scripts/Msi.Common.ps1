# Shared pinned compiler and deterministic payload authoring for MSI packaging.
function Invoke-MsiPackage {
    param([string] $Version, [string] $Payload, [string] $WorkDirectory, [string] $Msi,
          [string] $Authoring, [string] $Identity, [string] $CustomActions = '')
    $files = @(Get-ChildItem -LiteralPath $Payload -Recurse -File | Sort-Object FullName)
    $wixDirectory = Join-Path $ArtifactsRoot 'tools/wix'
    $wix = Join-Path $wixDirectory 'wix.exe'
    if (-not (Test-Path -LiteralPath $wix)) {
        & dotnet tool install wix --version 6.0.2 --tool-path $wixDirectory --add-source https://api.nuget.org/v3/index.json
        if ($LASTEXITCODE -ne 0) { throw 'WiX installation failed.' }
    }
    $toolVersion = & $wix --version
    if ($LASTEXITCODE -ne 0 -or $toolVersion -notmatch '^6\.0\.2\+') { throw 'MSI packaging requires pinned WiX 6.0.2.' }
    $extension = Join-Path $wixDirectory '.wix/extensions/WixToolset.UI.wixext/6.0.2/wixext6/WixToolset.UI.wixext.dll'
    if (-not (Test-Path -LiteralPath $extension)) {
        Push-Location $wixDirectory
        try {
            & $wix extension add WixToolset.UI.wixext/6.0.2
            if ($LASTEXITCODE -ne 0) { throw 'WiX UI extension installation failed.' }
        } finally { Pop-Location }
    }
    if (-not (Test-Path -LiteralPath $extension)) { throw 'Pinned WiX UI extension is missing.' }

    # Deterministic per-path IDs. HKCU key paths are required for per-user MSI components.
    function Get-MsiId([string] $Prefix, [string] $RelativePath) {
        $bytes = [Text.Encoding]::UTF8.GetBytes($RelativePath.ToLowerInvariant())
        return $Prefix + [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).Substring(0, 32)
    }
    function ConvertTo-MsiXml([string] $Value) { return [Security.SecurityElement]::Escape($Value) }
    function Get-MsiGuid([string] $Id) {
        $bytes = [Text.Encoding]::UTF8.GetBytes(('PaneShift.' + $Identity + '|') + $Id)
        $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
        return '{0}-{1}-8{2}-A{3}-{4}' -f $hash.Substring(0, 8), $hash.Substring(8, 4), $hash.Substring(13, 3), $hash.Substring(17, 3), $hash.Substring(20, 12)
    }
    $directories = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::OrdinalIgnoreCase)
    $directories[''] = 'INSTALLFOLDER'
    $xml = [Collections.Generic.List[string]]::new()
    $xml.Add('<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs"><Fragment>')
    foreach ($directory in @(Get-ChildItem -LiteralPath $payload -Recurse -Directory | Sort-Object FullName)) {
        $relative = [IO.Path]::GetRelativePath($payload, $directory.FullName)
        $parent = [IO.Path]::GetDirectoryName($relative)
        $id = Get-MsiId 'd' $relative
        $directories[$relative] = $id
        $xml.Add(('<DirectoryRef Id="{0}"><Directory Id="{1}" Name="{2}" /></DirectoryRef>' -f $directories[$parent], $id, (ConvertTo-MsiXml $directory.Name)))
    }
    $group = if ($Identity -eq 'MsiTrial') { 'TrialPayload' } else { 'Payload' }; $marker = if ($Identity -eq 'MsiTrial') { 'MsiTrial' } else { 'Installer' }; $xml.Add(('<ComponentGroup Id="{0}">' -f $group))
    foreach ($file in $files) {
        $relative = [IO.Path]::GetRelativePath($payload, $file.FullName)
        $id = Get-MsiId 'c' $relative
        $directory = $directories[[IO.Path]::GetDirectoryName($relative)]
        $xml.Add(('<Component Id="{0}" Directory="{1}" Guid="{3}"><File Id="f{0}" Source="{2}" KeyPath="no" /><RegistryValue Root="HKCU" Key="Software\PaneShift\{4}\Components" Name="{0}" Type="integer" Value="1" KeyPath="yes" /></Component>' -f $id, $directory, (ConvertTo-MsiXml $file.FullName), (Get-MsiGuid $id), $marker))
    }
    foreach ($pair in $directories.GetEnumerator()) {
        $id = Get-MsiId 'r' $pair.Key
        $xml.Add(('<Component Id="{0}" Directory="{1}" Guid="*"><RemoveFolder Id="{0}" On="uninstall" /><RegistryValue Root="HKCU" Key="Software\PaneShift\{2}\Directories" Name="{0}" Type="integer" Value="1" KeyPath="yes" /></Component>' -f $id, $pair.Value, $marker))
    }
    $xml.Add('</ComponentGroup></Fragment></Wix>')
    $payloadSource = Join-Path $workDirectory 'Payload.wxs'
    $xml | Set-Content -LiteralPath $payloadSource -Encoding utf8NoBOM
    $license = Get-Content (Join-Path $RepoRoot 'LICENSE') -Raw
    $rtf = [Text.StringBuilder]::new('{\rtf1\ansi\deff0{\fonttbl{\f0 Segoe UI;}}\f0\fs18 ')
    foreach ($character in $license.ToCharArray()) {
        $code = [int]$character
        if ($character -eq "`r") { continue }
        if ($character -eq "`n") { $null = $rtf.Append('\par '); continue }
        if ($character -in @('\', '{', '}')) { $null = $rtf.Append('\') }
        if ($code -gt 127) { $null = $rtf.Append('\u' + $code + '?') } else { $null = $rtf.Append($character) }
    }
    $null = $rtf.Append('}')
    $licensePath = Join-Path $workDirectory 'License.rtf'
    $rtf.ToString() | Set-Content -LiteralPath $licensePath -Encoding ascii

    & $wix build (Join-Path $RepoRoot $Authoring) $payloadSource -arch x64 -ext $extension `
        -d "AppVersion=$Version" -d "ProjectRoot=$RepoRoot" -d "LicenseRtf=$licensePath" -d "CustomActions=$CustomActions" `
        -intermediatefolder (Join-Path $workDirectory 'intermediate') -pdbtype none -out $msi
    if ($LASTEXITCODE -ne 0) { throw 'MSI build failed.' }

}
