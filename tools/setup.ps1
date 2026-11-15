$ErrorActionPreference = "Stop"
$root = git rev-parse --show-toplevel
Set-Location $root

Write-Host "Checking tools..."
$checks = @(
  @{ Name = "git"; Cmd = { git --version } },
  @{ Name = "gh (logged in)"; Cmd = { gh auth status 2>&1 | Out-Null; if ($LASTEXITCODE -ne 0) { throw "run: gh auth login" }; "ok" } },
  @{ Name = "node"; Cmd = { node -v } },
  @{ Name = "pnpm"; Cmd = { $v = pnpm -v 2>$null; if (-not $v) { throw "install: winget install --id pnpm.pnpm -e" }; $v } },
  @{ Name = ".NET 10 SDK"; Cmd = { $s = dotnet --list-sdks | Select-String "^10\."; if (-not $s) { throw "install: winget install --id Microsoft.DotNet.SDK.10 -e" }; $s[0].ToString() } },
  @{ Name = "dotnet-ef"; Cmd = { dotnet ef --version 2>&1 | Select-Object -Last 1 } },
  @{ Name = "docker"; Cmd = { docker --version } }
)
foreach ($c in $checks) {
  try { $out = & $c.Cmd; Write-Host ("  OK   {0}: {1}" -f $c.Name, $out) }
  catch { Write-Host ("  MISS {0}: {1}" -f $c.Name, $_.Exception.Message) -ForegroundColor Yellow }
}

$globalRules = Join-Path $env:USERPROFILE ".claude\CLAUDE.md"
if (-not (Test-Path $globalRules)) { Write-Host "  MISS global rules: copy Projects\global-CLAUDE.md to $globalRules" -ForegroundColor Yellow } else { Write-Host "  OK   global rules: $globalRules" }

git config core.hooksPath .githooks
Write-Host "Git hooks enabled (.githooks: commit-msg, pre-push)."

$claudeDir = Join-Path $env:USERPROFILE ".claude"
$settingsPath = Join-Path $claudeDir "settings.json"
if (-not (Test-Path $claudeDir)) { New-Item -ItemType Directory -Path $claudeDir | Out-Null }
if (Test-Path $settingsPath) {
  Copy-Item $settingsPath "$settingsPath.bak" -Force
  $settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
} else {
  $settings = [pscustomobject]@{}
}
$settings | Add-Member -Force -NotePropertyName attribution -NotePropertyValue ([pscustomobject]@{ commit = ""; pr = "" })
$json = $settings | ConvertTo-Json -Depth 32
[System.IO.File]::WriteAllText($settingsPath, $json, (New-Object System.Text.UTF8Encoding $false))
Write-Host "User-level Claude Code attribution disabled in $settingsPath (backup: settings.json.bak)."

node tools/verify.mjs
