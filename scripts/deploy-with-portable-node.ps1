# Case Closed — deploy API proxy (reads NETLIFY_AUTH_TOKEN from your env; never hardcode it)
# Non-interactive only — never prompts Git Credential Manager / browser login.
$ErrorActionPreference = "Stop"
$env:GIT_TERMINAL_PROMPT = "0"
$env:GCM_INTERACTIVE = "never"

$nodeDir = "C:\Users\mags5\AppData\Local\Temp\node-v22.14.0-win-x64"
if (-not (Test-Path "$nodeDir\node.exe")) { throw "Portable Node missing at $nodeDir" }
$env:Path = "$nodeDir;$env:Path"

# Reload User env into this process if Magdy just set it in System Properties
$u = [Environment]::GetEnvironmentVariable("NETLIFY_AUTH_TOKEN", "User")
if ($u) { $env:NETLIFY_AUTH_TOKEN = $u }
$m = [Environment]::GetEnvironmentVariable("NETLIFY_AUTH_TOKEN", "Machine")
if (-not $env:NETLIFY_AUTH_TOKEN -and $m) { $env:NETLIFY_AUTH_TOKEN = $m }

if (-not $env:NETLIFY_AUTH_TOKEN -or $env:NETLIFY_AUTH_TOKEN.Length -lt 8) {
    throw "NETLIFY_AUTH_TOKEN not set. Set User env NETLIFY_AUTH_TOKEN (Netlify personal access token), open a NEW PowerShell, then re-run this script. Do not paste the token into chat."
}

$root = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path "$root\netlify\functions\gemini.mjs")) {
    throw "Expected gemini function missing at $root\netlify\functions\gemini.mjs"
}
Set-Location $root

Write-Host "Using node: $(node -v)"
Write-Host "Deploying site/ + netlify/functions from $root (non-interactive) ..."
& npx --yes netlify-cli deploy --prod --dir=site --functions=netlify/functions --site=77866f37-2c33-44a0-a846-3d44200be13b --auth $env:NETLIFY_AUTH_TOKEN
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Check: https://ubcaseclosed.netlify.app/api/health"
