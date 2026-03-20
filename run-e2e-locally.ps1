#Requires -Version 7
<#
.SYNOPSIS
    Builds, serves, and runs Playwright E2E tests locally.
.PARAMETER Filter
    Optional test filter passed to dotnet test (e.g. "HomePageTests").
.PARAMETER SkipBuild
    Skip dotnet build/publish if the app is already built.
.PARAMETER Port
    Port to serve the app on. Defaults to 5000.
#>
param(
    [string]$Filter,
    [switch]$SkipBuild,
    [switch]$Headed,
    [int]$Port = 5000
)

$ErrorActionPreference = 'Stop'
$Root = $PSScriptRoot

function Step([string]$msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Die([string]$msg)  { Write-Host "ERROR: $msg" -ForegroundColor Red; exit 1 }

# --- 1. Build & publish ---
if (-not $SkipBuild) {
    Step "Building solution"
    dotnet build "$Root/SBOMViewer.slnx" -c Release --nologo
    if ($LASTEXITCODE -ne 0) { Die "Build failed" }

    Step "Publishing Blazor app"
    dotnet publish "$Root/src/SBOMViewer.Blazor" -c Release --output "$Root/publish_output" --nologo
    if ($LASTEXITCODE -ne 0) { Die "Publish failed" }
}

# --- 2. Install Playwright Chromium if needed ---
$PlaywrightScript = "$Root/tests/SBOMViewer.E2E.Tests/bin/Release/net10.0/playwright.ps1"
if (-not (Test-Path $PlaywrightScript)) {
    Die "playwright.ps1 not found at $PlaywrightScript — run without -SkipBuild first"
}

Step "Installing Playwright Chromium (no-op if already installed)"
& $PlaywrightScript install chromium | Out-Null

# --- 3. Check serve is available ---
if (-not (Get-Command serve -ErrorAction SilentlyContinue)) {
    Step "Installing 'serve' globally via npm"
    npm install -g serve
    if ($LASTEXITCODE -ne 0) { Die "Failed to install serve" }
}

# --- 4. Start static file server ---
Step "Starting static file server on port $Port"
$ServerProcess = Start-Process -FilePath "cmd.exe" `
    -ArgumentList "/c", "serve", "-s", "$Root/publish_output/wwwroot", "-l", "$Port" `
    -PassThru -NoNewWindow

# Ensure server is killed when script exits
try {
    # --- 5. Wait for server to be ready ---
    Step "Waiting for server to be ready"
    $Ready = $false
    for ($i = 1; $i -le 30; $i++) {
        try {
            $null = Invoke-WebRequest -Uri "http://localhost:$Port" -UseBasicParsing -TimeoutSec 1
            Write-Host "Ready after ${i}s"
            $Ready = $true
            break
        } catch {
            Start-Sleep -Seconds 1
        }
    }
    if (-not $Ready) { Die "Server did not become ready within 30s" }

    # --- 6. Run E2E tests ---
    Step "Running E2E tests"
    $TestArgs = @(
        "test", "$Root/tests/SBOMViewer.E2E.Tests",
        "-c", "Release",
        "--no-build",
        "-e", "BASE_URL=http://localhost:$Port",
        "-e", "PLAYWRIGHT_BROWSERS_PATH=0",
        "-e", "HEADED=$(if ($Headed) { '1' } else { '0' })"
    )
    if ($Filter) {
        $TestArgs += "--filter", $Filter
    }

    dotnet @TestArgs
    $ExitCode = $LASTEXITCODE
} finally {
    # --- 7. Stop server ---
    Step "Stopping server"
    Stop-Process -Id $ServerProcess.Id -Force -ErrorAction SilentlyContinue
}

exit $ExitCode
