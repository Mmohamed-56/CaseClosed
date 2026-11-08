# Deploy UPSR site + API proxy functions to ubcaseclosed.
# Requires: NETLIFY_AUTH_TOKEN in the environment (never commit it).
# Usage:  pwsh ./scripts/deploy-api-proxy.ps1

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $root

if (-not $env:NETLIFY_AUTH_TOKEN -or $env:NETLIFY_AUTH_TOKEN.Length -lt 8) {
    Write-Error "NETLIFY_AUTH_TOKEN is missing. Paste it into your shell env or Netlify CLI login, then re-run."
}

$siteId = "77866f37-2c33-44a0-a846-3d44200be13b"
$nodeDir = "C:\Users\mags5\AppData\Local\Temp\node-v22.14.0-win-x64"
$npm = if (Test-Path "$nodeDir\npm.cmd") { "$nodeDir\npm.cmd" } else { "npm" }
$npx = if (Test-Path "$nodeDir\npx.cmd") { "$nodeDir\npx.cmd" } else { "npx" }

Write-Host "Deploying site/ + netlify/functions to site $siteId ..."
& $npx --yes netlify-cli deploy --prod --dir=site --functions=netlify/functions --site=$siteId
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "Live base URL: https://ubcaseclosed.netlify.app"
Write-Host "Health check:  https://ubcaseclosed.netlify.app/api/health"
Write-Host "Remember to set Netlify env secrets: GEMINI_API_KEY, ELEVENLABS_API_KEY, CASE_CLOSED_APP_TOKEN"
