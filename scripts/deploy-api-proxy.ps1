# Deploy UPSR site + API proxy functions to ubcaseclosed.
# Requires: NETLIFY_AUTH_TOKEN in the environment or Windows User env (never commit it).
# Usage:  pwsh ./scripts/deploy-api-proxy.ps1

$ErrorActionPreference = "Stop"
$env:GIT_TERMINAL_PROMPT = "0"
$env:GCM_INTERACTIVE = "never"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $root

$nodeDir = "C:\Users\mags5\AppData\Local\Temp\node-v22.14.0-win-x64"
if (Test-Path "$nodeDir\node.exe") {
    $env:Path = "$nodeDir;$env:Path"
}

# Pick up User env if Magdy set it in System Properties after this shell started
$u = [Environment]::GetEnvironmentVariable("NETLIFY_AUTH_TOKEN", "User")
if ($u) { $env:NETLIFY_AUTH_TOKEN = $u }
$m = [Environment]::GetEnvironmentVariable("NETLIFY_AUTH_TOKEN", "Machine")
if (-not $env:NETLIFY_AUTH_TOKEN -and $m) { $env:NETLIFY_AUTH_TOKEN = $m }

if (-not $env:NETLIFY_AUTH_TOKEN -or $env:NETLIFY_AUTH_TOKEN.Length -lt 8) {
    Write-Error "NETLIFY_AUTH_TOKEN is missing. Set it as a Windows User env var (or in this shell), then re-run. Do not paste it into chat."
}

$siteId = "77866f37-2c33-44a0-a846-3d44200be13b"
$npx = if (Test-Path "$nodeDir\npx.cmd") { "$nodeDir\npx.cmd" } else { "npx" }

Write-Host "Using node: $(node -v)"
Write-Host "Deploying site/ + netlify/functions to site $siteId (non-interactive) ..."
& $npx --yes netlify-cli deploy --prod --dir=site --functions=netlify/functions --site=$siteId --auth $env:NETLIFY_AUTH_TOKEN
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "Live base URL: https://ubcaseclosed.netlify.app"
Write-Host "Health check:  https://ubcaseclosed.netlify.app/api/health"
Write-Host "Remember Netlify env secrets: GEMINI_API_KEY, ELEVENLABS_API_KEY, CASE_CLOSED_APP_TOKEN"
