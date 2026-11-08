# Case Closed — deploy API proxy (reads NETLIFY_AUTH_TOKEN from your env; never hardcode it)
$ErrorActionPreference = "Stop"
$nodeDir = "C:\Users\mags5\AppData\Local\Temp\node-v22.14.0-win-x64"
if (-not (Test-Path "$nodeDir\node.exe")) { throw "Portable Node missing at $nodeDir" }
$env:Path = "$nodeDir;$env:Path"
# Reload User env into this process if Magdy just set it in System Properties
$u = [Environment]::GetEnvironmentVariable("NETLIFY_AUTH_TOKEN","User")
if ($u) { $env:NETLIFY_AUTH_TOKEN = $u }
if (-not $env:NETLIFY_AUTH_TOKEN) { throw "NETLIFY_AUTH_TOKEN not set in this shell or User env." }
Set-Location "C:\Users\mags5\My project-worktrees\api-proxy-f9c2"
npx --yes netlify-cli deploy --prod --dir=site --functions=netlify/functions --site=77866f37-2c33-44a0-a846-3d44200be13b
Write-Host "Check: https://ubcaseclosed.netlify.app/api/health"
