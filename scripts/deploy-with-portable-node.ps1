# Case Closed — deploy UPSR site + API proxy functions
# Reads NETLIFY_AUTH_TOKEN from process or User env; never hardcode it.
$ErrorActionPreference = "Stop"
$nodeDir = "C:\Users\mags5\AppData\Local\Temp\node-v22.14.0-win-x64"
if (-not (Test-Path "$nodeDir\node.exe")) { throw "Portable Node missing at $nodeDir" }
$env:Path = "$nodeDir;$env:Path"
$u = [Environment]::GetEnvironmentVariable("NETLIFY_AUTH_TOKEN", "User")
if ($u) { $env:NETLIFY_AUTH_TOKEN = $u }
if (-not $env:NETLIFY_AUTH_TOKEN) { throw "NETLIFY_AUTH_TOKEN not set in this shell or User env." }

$repo = Split-Path -Parent $PSScriptRoot
Set-Location $repo
if (-not (Test-Path "site\index.html")) { throw "site/index.html missing under $repo" }
if (-not (Test-Path "netlify\functions")) { throw "netlify/functions missing under $repo" }

npx --yes netlify-cli deploy --prod --dir=site --functions=netlify/functions --site=77866f37-2c33-44a0-a846-3d44200be13b
Write-Host "Live: https://ubcaseclosed.netlify.app/"
Write-Host "Health: https://ubcaseclosed.netlify.app/api/health"
