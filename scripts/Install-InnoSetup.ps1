# Installs a pinned build tool under ignored artifacts/tools; does not require admin.
[CmdletBinding()]
param()
. "$PSScriptRoot/Release.Common.ps1"
$tools = Join-Path $ArtifactsRoot 'tools'
$destination = Join-Path $tools 'InnoSetup'
$compiler = Join-Path $destination 'ISCC.exe'
if (Test-Path -LiteralPath $compiler) { Write-Output $compiler; return }
New-Item -ItemType Directory -Path $tools -Force | Out-Null
$download = Join-Path $tools 'innosetup-6.7.3.exe'
Invoke-WebRequest 'https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe' -OutFile $download
$expected = '9c73c3bae7ed48d44112a0f48e66742c00090bdb5bef71d9d3c056c66e97b732'
if ((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash -ne $expected) { throw 'Inno Setup download hash mismatch.' }
$signature = Get-AuthenticodeSignature -LiteralPath $download
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'CN=Pyrsys B\.V\.') { throw 'Inno Setup publisher verification failed.' }
$process = Start-Process -FilePath $download -ArgumentList @('/CURRENTUSER', '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/NOICONS', "/DIR=`"$destination`"") -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(60000)) { throw 'Inno Setup tool installation is still running. Inspect it before retrying.' }
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $compiler)) { throw 'Inno Setup tool installation failed.' }
Write-Output $compiler
