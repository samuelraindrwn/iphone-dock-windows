#Requires -Version 5.1
<#
.SYNOPSIS
Runs iDock for Windows launcher and BLE backend regression checks without connecting to hardware.
.NOTES
Requires Windows with a desktop session and the .NET 10 SDK (x64). WPF windows are
offscreen; Bluetooth advertising, pairing and input hooks are not activated.
#>
[CmdletBinding()]
param(
    [string]$PackageDirectory = 'dist\iDock',
    [string]$RestoreSource,
    [string]$PackagesDirectory
)

. (Join-Path $PSScriptRoot 'common.ps1')
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$packagePath = Get-iDockFullPath -Path $PackageDirectory -RelativeTo $repositoryRoot
$testDirectory = Join-Path $repositoryRoot ('.artifacts\tests\' + [Guid]::NewGuid().ToString('N'))
$previousDataRoot = [Environment]::GetEnvironmentVariable('BLEHID_DATA_DIR', 'Process')

function Invoke-OwnedTestProcess {
    param([string]$Executable, [string]$ArgumentLine, [string]$LogPrefix)
    $standardOutput = Join-Path $testDirectory ($LogPrefix + '-stdout.txt')
    $standardError = Join-Path $testDirectory ($LogPrefix + '-stderr.txt')
    $parameters = @{
        FilePath = $Executable
        WorkingDirectory = (Split-Path -Parent $Executable)
        WindowStyle = 'Hidden'
        PassThru = $true
        RedirectStandardOutput = $standardOutput
        RedirectStandardError = $standardError
    }
    if ($ArgumentLine) { $parameters.ArgumentList = $ArgumentLine }
    $testProcess = Start-Process @parameters
    # Keep an owned handle even if a short-lived child exits before WaitForExit.
    $null = $testProcess.Handle
    try {
        if (-not $testProcess.WaitForExit(60000)) {
            # Only the process created by this invocation may be stopped.
            $testProcess.Kill()
            $testProcess.WaitForExit()
            throw "The $LogPrefix test exceeded 60 seconds. Logs: $testDirectory"
        }
        $testProcess.WaitForExit()
        $testProcess.Refresh()
        if ($testProcess.ExitCode -ne 0) {
            throw "The $LogPrefix test failed (exit $($testProcess.ExitCode)). Logs: $testDirectory"
        }
    }
    finally { $testProcess.Dispose() }
    return $standardOutput
}

Push-Location $repositoryRoot
try {
    Assert-iDockPrerequisites
    $launcher = Join-Path $packagePath 'iDock.exe'
    if (-not (Test-Path -LiteralPath $launcher -PathType Leaf)) {
        throw 'Build the package first with .\scripts\build.ps1, or supply -PackageDirectory.'
    }
    $null = New-Item -ItemType Directory -Path $testDirectory -Force
    [Environment]::SetEnvironmentVariable('BLEHID_DATA_DIR', (Join-Path $testDirectory 'blehid-data'), 'Process')
    $launcherReport = Join-Path $testDirectory 'launcher.txt'
    $null = Invoke-OwnedTestProcess -Executable $launcher -ArgumentLine ('--self-test "' + $launcherReport + '"') -LogPrefix 'launcher'
    if (-not (Test-Path -LiteralPath $launcherReport -PathType Leaf)) { throw 'Launcher did not create a test report.' }
    $launcherChecks = @(Get-Content -LiteralPath $launcherReport | Where-Object { $_ -like 'PASS *' }).Count
    if ($launcherChecks -ne 230) {
        throw "Expected 230 launcher checks; found $launcherChecks. Report: $launcherReport"
    }
    Write-Host "PASS: $launcherChecks launcher checks."

    $safetyProject = Join-Path $repositoryRoot 'source\blehid-patched\tests\BleHid.SafetyChecks\BleHid.SafetyChecks.csproj'
    Invoke-CheckedDotnet -Arguments (Get-iDockRestoreArguments $safetyProject $RestoreSource $PackagesDirectory)
    $safetyOutput = Join-Path $testDirectory 'safety-bin'
    Invoke-CheckedDotnet -Arguments @('build', $safetyProject, '--no-restore', '--configuration', 'Release', '-p:PlatformTarget=x64', '--output', $safetyOutput)
    $safetyLog = Invoke-OwnedTestProcess -Executable (Join-Path $safetyOutput 'BleHid.Core.Tests.exe') -LogPrefix 'backend'
    $safetyText = Get-Content -LiteralPath $safetyLog -Raw
    if ($safetyText -notmatch 'All 115 hardware-free safety checks passed\.') {
        throw "Backend did not report all 115 passing checks. Report: $safetyLog"
    }
    Write-Host 'PASS: 115 hardware-free BLE backend checks.'
    Write-Host "All checks passed. Reports and isolated test data: $testDirectory"
    Write-Host 'Hardware compatibility, AirPlay latency, and actual iPhone control still require a manual device test.'
}
finally {
    [Environment]::SetEnvironmentVariable('BLEHID_DATA_DIR', $previousDataRoot, 'Process')
    Pop-Location
}
