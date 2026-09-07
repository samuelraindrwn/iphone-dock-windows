<#
.SYNOPSIS
    Process-level regression tests for CLI/app handover.

.DESCRIPTION
    These need a real Bluetooth radio because both front ends start the peripheral, so they
    cannot live in the unit suite. Everything here is automatable; see MANUAL-TESTS.md for the
    scenarios that need a phone or a pair of human eyes.

.EXAMPLE
    .\tests\Invoke-HandoverTests.ps1
#>
[CmdletBinding()]
param(
    [int]$StartupSeconds = 12
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$app = Join-Path $root 'src\BleHid.App\BleHid.App.csproj'
$cli = Join-Path $root 'src\BleHid.Cli\BleHid.Cli.csproj'
$results = [System.Collections.Generic.List[object]]::new()

function Stop-All {
    Get-Process BleHid.App, BleHid.Cli -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 1
}

function Start-Detached([string]$project, [string]$extraArgs) {
    Start-Process dotnet -ArgumentList "run --project `"$project`" --no-build -- $extraArgs" | Out-Null
    Start-Sleep -Seconds $StartupSeconds
}

function Invoke-Stop {
    & dotnet run --project $cli --no-build -- --stop | Out-Null
    Start-Sleep -Seconds 3
}

function Test-Running([string]$name) {
    [bool](Get-Process $name -ErrorAction SilentlyContinue)
}

function Assert-That([string]$name, [bool]$condition, [string]$detail) {
    $results.Add([pscustomobject]@{ Test = $name; Passed = $condition; Detail = $detail })
    $status = if ($condition) { 'PASS' } else { 'FAIL' }
    Write-Host ("  [{0}] {1}" -f $status, $name) -ForegroundColor $(if ($condition) { 'Green' } else { 'Red' })
}

Write-Host "`nBuilding..." -ForegroundColor Cyan
Stop-All
& dotnet build $cli -v q --nologo | Out-Null
& dotnet build $app -v q --nologo | Out-Null

Write-Host "`nHandover scenarios" -ForegroundColor Cyan

# 1. The CLI can retire the desktop app.
Stop-All
Start-Detached $app '--tray'
$appStarted = Test-Running 'BleHid.App'
Invoke-Stop
Assert-That 'CLI --stop retires the tray app' ($appStarted -and -not (Test-Running 'BleHid.App')) `
    "started=$appStarted"

# 2. Relaunching straight after a stop must not inherit the stop request.
#    This is the regression: a ManualReset named event keeps its signal, so the new owner used
#    to see it immediately and exit, leaving nothing owning the radio.
Start-Detached $app '--tray'
Assert-That 'Relaunch after stop survives the latched stop event' (Test-Running 'BleHid.App') ''

# 3. ...and the relaunched instance must still be listening.
Invoke-Stop
Assert-That 'Relaunched app still responds to --stop' (-not (Test-Running 'BleHid.App')) ''

# 4. Background mode is unchanged by all of the above.
Stop-All
Start-Detached $cli '--background'
$bgStarted = Test-Running 'BleHid.Cli'
Invoke-Stop
Assert-That 'Background mode starts and stops' ($bgStarted -and -not (Test-Running 'BleHid.Cli')) `
    "started=$bgStarted"

# 5. Only one process may hold the radio.
Stop-All
Start-Detached $cli '--background'
$held = Test-Running 'BleHid.Cli'
$owned = $false
$mutex = New-Object System.Threading.Mutex($true, 'Local\BleHid.Peripheral', [ref]$owned)
Assert-That 'Peripheral mutex is held while background mode runs' ($held -and -not $owned) `
    "background=$held, acquired=$owned"
$mutex.Dispose()

Stop-All

Write-Host "`nDiagnostics" -ForegroundColor Cyan

# 6. --diagnose must produce the file we ask bug reporters for.
$report = Join-Path $env:LOCALAPPDATA 'BleHid\logs\diagnostics.txt'
Remove-Item $report -ErrorAction SilentlyContinue
& dotnet run --project $cli --no-build -- --diagnose | Out-Null
$exists = Test-Path $report
$content = if ($exists) { Get-Content $report -Raw } else { '' }
Assert-That '--diagnose writes the report into the logs folder' $exists $report
Assert-That '--diagnose records adapter role support' ($content -match 'Peripheral role') ''
Assert-That '--diagnose records the advertisement outcome' ($content -match 'Advertisement status') ''

# 7. A transient Aborted before Started must not read as a failure.
Assert-That 'Transient Aborted is annotated as expected' `
    ($content -notmatch 'Aborted \(error: \w+\)\s*$' -or $content -match 'expected while starting') ''

Write-Host ''
$failed = @($results | Where-Object { -not $_.Passed })
$results | Format-Table -AutoSize
if ($failed.Count -gt 0) {
    Write-Host "$($failed.Count) of $($results.Count) failed." -ForegroundColor Red
    exit 1
}
Write-Host "All $($results.Count) passed." -ForegroundColor Green
