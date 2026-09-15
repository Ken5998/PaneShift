# Assemble only public site files; release binaries remain on GitHub Releases.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$output = Join-Path $root 'artifacts/site'
New-Item -ItemType Directory -Path "$output/assets" -Force | Out-Null
Copy-Item "$root/site/index.html", "$root/site/style.css", "$root/site/app.js" -Destination $output -Force
Copy-Item "$root/assets/paneshift.png", "$root/assets/paneshift.ico" -Destination "$output/assets" -Force
Copy-Item "$root/docs/images/settings-shortcuts.png", "$root/docs/images/settings-layout.png", "$root/docs/images/settings-general.png" -Destination "$output/assets" -Force
Set-Content "$output/.nojekyll" ''
Write-Host "Static website assembled in $output"
