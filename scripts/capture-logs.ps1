<#
.SYNOPSIS
    CoralLedger Blue E2E Test Log Capture Script

.DESCRIPTION
    This script:
    1. Starts the Aspire application in the background
    2. Captures stdout/stderr to timestamped log files
    3. Optionally runs Playwright E2E tests and captures browser console
    4. Outputs comprehensive logs for debugging

.PARAMETER RunE2ETests
    If specified, runs Playwright E2E tests after Aspire starts

.PARAMETER WaitTimeSeconds
    Time to wait for Aspire to start before running tests (default: 45)

.PARAMETER TestFilter
    Optional filter for specific E2E tests (e.g., "Dashboard")

.PARAMETER SkipBuild
    Skip the build step (use if already built)

.EXAMPLE
    .\capture-logs.ps1
    # Starts Aspire and captures logs only

.EXAMPLE
    .\capture-logs.ps1 -RunE2ETests -TestFilter "Dashboard"
    # Starts Aspire, runs Dashboard tests, captures all logs
#>

param(
    [switch]$RunE2ETests,
    [int]$WaitTimeSeconds = 45,
    [string]$TestFilter = "",
    [switch]$SkipBuild
)

# Configuration
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$LogsDir = Join-Path $ProjectRoot "logs"
$Timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$LogFile = Join-Path $LogsDir "e2e-$Timestamp.log"
$AspireProject = Join-Path $ProjectRoot "src\CoralLedger.Blue.AppHost\CoralLedger.Blue.AppHost.csproj"
$E2ETestProject = Join-Path $ProjectRoot "tests\CoralLedger.Blue.E2E.Tests\CoralLedger.Blue.E2E.Tests.csproj"
$HttpsPort = 7232

# Ensure logs directory exists
if (-not (Test-Path $LogsDir)) {
    New-Item -ItemType Directory -Path $LogsDir -Force | Out-Null
}

# Helper function to write to both console and log
function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $LogMessage = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] [$Level] $Message"
    Write-Host $LogMessage
    Add-Content -Path $LogFile -Value $LogMessage
}

# Start logging
Write-Log "=========================================="
Write-Log "CoralLedger Blue E2E Log Capture Started"
Write-Log "=========================================="

# Capture system information
Write-Log "System Information:"
Write-Log "  OS: $([System.Environment]::OSVersion.VersionString)"
Write-Log "  PowerShell: $($PSVersionTable.PSVersion)"
Write-Log "  Working Directory: $ProjectRoot"

# Get .NET version
try {
    $dotnetVersion = dotnet --version
    Write-Log "  .NET SDK: $dotnetVersion"
} catch {
    Write-Log "  .NET SDK: Unable to determine" "WARN"
}

Write-Log "  Expected HTTPS Port: $HttpsPort"
Write-Log "------------------------------------------"

# Build the solution first (unless skipped)
if (-not $SkipBuild) {
    Write-Log "Building solution..."
    $buildOutput = & dotnet build "$ProjectRoot\CoralLedger.sln" --configuration Release 2>&1
    $buildOutput | ForEach-Object { Add-Content -Path $LogFile -Value "  [BUILD] $_" }

    if ($LASTEXITCODE -ne 0) {
        Write-Log "Build failed! See log for details." "ERROR"
        exit 1
    }
    Write-Log "Build successful"
} else {
    Write-Log "Skipping build (--SkipBuild specified)"
}

Write-Log "------------------------------------------"
Write-Log "Starting Aspire Application..."

# Start Aspire in background
$aspireLogFile = Join-Path $LogsDir "aspire-$Timestamp.log"

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = "dotnet"
$psi.Arguments = "run --project `"$AspireProject`" --no-build"
$psi.UseShellExecute = $false
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.CreateNoWindow = $true
$psi.WorkingDirectory = $ProjectRoot

$aspireProcess = New-Object System.Diagnostics.Process
$aspireProcess.StartInfo = $psi

# Capture output asynchronously
$outputBuilder = New-Object System.Text.StringBuilder
$errorBuilder = New-Object System.Text.StringBuilder

$aspireProcess.add_OutputDataReceived({
    param($sender, $e)
    if ($e.Data) {
        [void]$outputBuilder.AppendLine($e.Data)
        Add-Content -Path $aspireLogFile -Value "[OUT] $($e.Data)"
    }
})

$aspireProcess.add_ErrorDataReceived({
    param($sender, $e)
    if ($e.Data) {
        [void]$errorBuilder.AppendLine($e.Data)
        Add-Content -Path $aspireLogFile -Value "[ERR] $($e.Data)"
    }
})

$aspireProcess.Start() | Out-Null
$aspireProcess.BeginOutputReadLine()
$aspireProcess.BeginErrorReadLine()

Write-Log "Aspire process started (PID: $($aspireProcess.Id))"
Write-Log "Aspire logs: $aspireLogFile"

# Wait for application to start
Write-Log "Waiting up to $WaitTimeSeconds seconds for Aspire to initialize..."
$elapsed = 0
$healthCheckUrl = "https://localhost:$HttpsPort/api/diagnostics/ready"
$appReady = $false

# Ignore SSL errors for localhost
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

while ($elapsed -lt $WaitTimeSeconds) {
    Start-Sleep -Seconds 2
    $elapsed += 2

    try {
        $response = Invoke-WebRequest -Uri $healthCheckUrl -TimeoutSec 5 -UseBasicParsing -ErrorAction Stop
        if ($response.StatusCode -eq 200) {
            $appReady = $true
            Write-Log "Application is ready! (Health check passed after $elapsed seconds)"

            # Log the health check response
            $healthData = $response.Content | ConvertFrom-Json
            Write-Log "Health Status: $($healthData.status)"
            foreach ($check in $healthData.checks.PSObject.Properties) {
                Write-Log "  - $($check.Name): $($check.Value.status)"
            }
            break
        }
    } catch {
        Write-Host "." -NoNewline
    }
}

Write-Host "" # New line after dots

if (-not $appReady) {
    Write-Log "Application failed to become ready within $WaitTimeSeconds seconds" "WARN"
    Write-Log "Continuing anyway - some tests may fail"
}

Write-Log "------------------------------------------"

# Run E2E tests if requested
$testExitCode = 0
if ($RunE2ETests) {
    Write-Log "Running Playwright E2E Tests..."

    # Check if E2E test project exists
    if (-not (Test-Path $E2ETestProject)) {
        Write-Log "E2E test project not found at: $E2ETestProject" "WARN"
        Write-Log "Skipping E2E tests"
    } else {
        # Set environment variable for base URL
        $env:E2E_BASE_URL = "https://localhost:$HttpsPort"
        $env:PLAYWRIGHT_HEADLESS = "true"

        # Build test args
        $testResultsFile = Join-Path $LogsDir "e2e-results-$Timestamp.trx"
        $testArgs = "test `"$E2ETestProject`" --logger `"trx;LogFileName=$testResultsFile`" --no-build"
        if ($TestFilter) {
            $testArgs += " --filter `"$TestFilter`""
        }

        Write-Log "Executing: dotnet $testArgs"
        $testOutput = Invoke-Expression "dotnet $testArgs" 2>&1
        $testOutput | ForEach-Object {
            Write-Host $_
            Add-Content -Path $LogFile -Value "[E2E] $_"
        }

        $testExitCode = $LASTEXITCODE
        Write-Log "E2E Tests completed with exit code: $testExitCode"

        # Copy Playwright artifacts to logs
        $artifactsDir = Join-Path $ProjectRoot "tests\CoralLedger.E2E.Tests\playwright-artifacts"
        if (Test-Path $artifactsDir) {
            $targetDir = Join-Path $LogsDir "playwright-$Timestamp"
            Copy-Item -Path $artifactsDir -Destination $targetDir -Recurse -Force
            Write-Log "Playwright artifacts copied to: $targetDir"
        }
    }
}

Write-Log "------------------------------------------"
Write-Log "Capture Summary:"
Write-Log "  Main Log: $LogFile"
Write-Log "  Aspire Log: $aspireLogFile"
Write-Log "  Logs Directory: $LogsDir"

# Cleanup
Write-Log "Stopping Aspire process..."
if (-not $aspireProcess.HasExited) {
    $aspireProcess.Kill()
    $aspireProcess.WaitForExit(5000) | Out-Null
}
Write-Log "Aspire process stopped"

# Capture final Aspire output summary
$finalErrors = $errorBuilder.ToString()
if ($finalErrors) {
    Write-Log "Aspire stderr output detected - check $aspireLogFile for details" "WARN"
}

Write-Log "=========================================="
Write-Log "Log capture completed: $LogFile"
Write-Log "=========================================="

# Return exit code based on test results
if ($RunE2ETests) {
    exit $testExitCode
}
exit 0
