#Requires -Version 5.1
<#
.SYNOPSIS
Builds a clean, framework-dependent iDock for Windows package without changing the installed app.
.EXAMPLE
.\scripts\build.ps1
.EXAMPLE
.\scripts\build.ps1 -UxPlayArchive C:\Downloads\uxplay-windows.zip
.NOTES
Run without elevation. The .NET 10 SDK (x64) is required. Existing generated outputs
are backed up, never deleted. No Bluetooth, Bonjour, firewall or user settings are changed.
#>
[CmdletBinding()]
param(
    [string]$UxPlayArchive,
    [string]$OutputDirectory = 'dist\iDock',
    [string]$RestoreSource,
    [string]$PackagesDirectory
)

. (Join-Path $PSScriptRoot 'common.ps1')
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$outputPath = Get-iDockFullPath -Path $OutputDirectory -RelativeTo $repositoryRoot
$outputParent = Split-Path -Parent $outputPath
$expectedArchiveHash = '9d3a51c15fc9db857351195e7eb7bbb21700d9ae25d936a54bcf8536b62cca18'
$archiveUrl = 'https://github.com/leapbtw/uxplay-windows/releases/download/2.0.0.1736/uxplay-windows.zip'
$stagePath = $null
$completed = $false

Push-Location $repositoryRoot
try {
    Assert-iDockPrerequisites
    # Only a specific application folder may be moved. Never accept a broad root,
    # source checkout, or arbitrary pre-existing directory as a replacement target.
    if ((Split-Path -Leaf $outputPath) -ne 'iDock' -or
        -not $outputParent -or $outputParent.TrimEnd('\') -eq [IO.Path]::GetPathRoot($outputPath).TrimEnd('\') -or
        $outputPath.Equals($repositoryRoot.TrimEnd('\'), [StringComparison]::OrdinalIgnoreCase)) {
        throw 'OutputDirectory must name a dedicated iDock subfolder, such as dist\iDock.'
    }
    Assert-NoReparsePoint $outputPath
    if (Test-Path -LiteralPath $outputPath) {
        $markerPath = Join-Path $outputPath '.idock-build.json'
        if (-not (Test-Path -LiteralPath $markerPath -PathType Leaf)) {
            throw "Existing output is not marked as a generated iDock for Windows build. Choose a new -OutputDirectory; nothing was overwritten: $outputPath"
        }
        $marker = Get-Content -LiteralPath $markerPath -Raw | ConvertFrom-Json
        if ($marker.Format -ne 1 -or $marker.Application -ne 'iDock') {
            throw 'Existing output has an unrecognized build marker; refusing replacement.'
        }
        Assert-iDockOutputIdle $outputPath
    }
    foreach ($requiredFile in @('source\iDock\iDock.csproj', 'source\blehid-patched\src\BleHid.Cli\BleHid.Cli.csproj', 'source\upstream\README.md', 'global.json', 'LICENSE', 'THIRD_PARTY_NOTICES.md', 'COMPONENTS.json')) {
        if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot $requiredFile) -PathType Leaf)) {
            throw "Required repository file is missing: $requiredFile"
        }
    }
    $null = New-Item -ItemType Directory -Path $outputParent -Force
    $stagePath = Join-Path $outputParent ('.iDock-staging-' + [Guid]::NewGuid().ToString('N'))
    $null = New-Item -ItemType Directory -Path $stagePath

    if ($UxPlayArchive) {
        $archivePath = (Resolve-Path -LiteralPath $UxPlayArchive).Path
    }
    else {
        $cacheDirectory = Join-Path $repositoryRoot '.cache\uxplay\2.0.0.1736'
        $null = New-Item -ItemType Directory -Path $cacheDirectory -Force
        $archivePath = Join-Path $cacheDirectory 'uxplay-windows.zip'
        if (-not (Test-Path -LiteralPath $archivePath -PathType Leaf)) {
            $downloadPath = Join-Path $cacheDirectory ('download-' + [Guid]::NewGuid().ToString('N') + '.zip')
            $previousTls = [Net.ServicePointManager]::SecurityProtocol
            $previousProgress = $ProgressPreference
            try {
                [Net.ServicePointManager]::SecurityProtocol = $previousTls -bor [Net.SecurityProtocolType]::Tls12
                $ProgressPreference = 'SilentlyContinue'
                Write-Host 'Downloading the pinned UxPlay Windows archive from its upstream GitHub release...'
                Invoke-WebRequest -Uri $archiveUrl -OutFile $downloadPath -UseBasicParsing
                if ((Get-FileHash -LiteralPath $downloadPath -Algorithm SHA256).Hash -ne $expectedArchiveHash) {
                    throw "Downloaded UxPlay archive has the wrong SHA256. It was not extracted: $downloadPath"
                }
                Move-Item -LiteralPath $downloadPath -Destination $archivePath
            }
            finally {
                [Net.ServicePointManager]::SecurityProtocol = $previousTls
                $ProgressPreference = $previousProgress
            }
        }
    }
    if ((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash -ne $expectedArchiveHash) {
        throw "UxPlay archive SHA256 mismatch. Nothing was extracted; supply the exact pinned release ZIP: $archivePath"
    }
    $uxplayOutput = Join-Path $stagePath 'vendor\uxplay'
    $null = New-Item -ItemType Directory -Path $uxplayOutput -Force
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($archivePath)
    try {
        foreach ($entry in $zip.Entries) {
            $entryPath = [IO.Path]::GetFullPath((Join-Path $uxplayOutput $entry.FullName))
            if (-not $entryPath.StartsWith($uxplayOutput + '\', [StringComparison]::OrdinalIgnoreCase)) {
                throw 'UxPlay archive contains an unsafe path.'
            }
        }
    }
    finally { $zip.Dispose() }
    [IO.Compression.ZipFile]::ExtractToDirectory($archivePath, $uxplayOutput)

    $launcherProject = Join-Path $repositoryRoot 'source\iDock\iDock.csproj'
    $backendProject = Join-Path $repositoryRoot 'source\blehid-patched\src\BleHid.Cli\BleHid.Cli.csproj'
    foreach ($project in @($launcherProject, $backendProject)) {
        Invoke-CheckedDotnet -Arguments (Get-iDockRestoreArguments $project $RestoreSource $PackagesDirectory)
    }
    Invoke-CheckedDotnet -Arguments @('publish', $launcherProject, '--no-restore', '--configuration', 'Release', '--self-contained', 'false', '-p:PlatformTarget=x64', '--output', $stagePath)
    $backendOutput = Join-Path $stagePath 'vendor\blehid'
    Invoke-CheckedDotnet -Arguments @('publish', $backendProject, '--no-restore', '--configuration', 'Release', '--self-contained', 'false', '-p:PlatformTarget=x64', '--output', $backendOutput)
    foreach ($name in @('LICENSE', 'README.md', 'IDOCK-MODIFICATIONS.md')) {
        Copy-Item -LiteralPath (Join-Path $repositoryRoot ('source\blehid-patched\' + $name)) -Destination $backendOutput
    }
    foreach ($name in @('LICENSE', 'THIRD_PARTY_NOTICES.md', 'COMPONENTS.json', 'global.json', 'README.md', 'PANDUAN.md')) {
        $document = Join-Path $repositoryRoot $name
        if (Test-Path -LiteralPath $document -PathType Leaf) { Copy-Item -LiteralPath $document -Destination $stagePath }
    }
    foreach ($name in @('docs', 'licenses')) {
        $directory = Join-Path $repositoryRoot $name
        if (Test-Path -LiteralPath $directory -PathType Container) { Copy-Item -LiteralPath $directory -Destination $stagePath -Recurse }
    }
    foreach ($name in @('iDock', 'blehid-patched')) {
        Copy-iDockSourceTree -SourceDirectory (Join-Path $repositoryRoot ('source\' + $name)) -DestinationDirectory (Join-Path $stagePath ('source\' + $name))
    }
    $upstreamOutput = Join-Path $stagePath 'source\upstream'
    $null = New-Item -ItemType Directory -Path $upstreamOutput -Force
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'source\upstream\README.md') -Destination $upstreamOutput
    Copy-iDockSourceTree -SourceDirectory (Join-Path $repositoryRoot 'scripts') -DestinationDirectory (Join-Path $stagePath 'scripts')
    foreach ($requiredFile in @('iDock.exe', 'iDock.dll', 'iDock.runtimeconfig.json', 'vendor\blehid\BleHid.Cli.exe', 'vendor\blehid\BleHid.Core.dll', 'vendor\blehid\Microsoft.Windows.SDK.NET.dll', 'vendor\blehid\WinRT.Runtime.dll', 'vendor\blehid\LICENSE', 'vendor\uxplay\uxplay-windows.exe', 'vendor\uxplay\mDNSResponder.exe', 'vendor\uxplay\Qt6Core.dll', 'vendor\uxplay\LICENSE.rtf', 'source\iDock\iDock.csproj', 'source\blehid-patched\src\BleHid.Cli\BleHid.Cli.csproj', 'source\blehid-patched\tests\BleHid.SafetyChecks\BleHid.SafetyChecks.csproj', 'source\blehid-patched\LICENSE', 'source\upstream\README.md', 'scripts\build.ps1', 'scripts\test.ps1', 'scripts\common.ps1', 'global.json')) {
        if (-not (Test-Path -LiteralPath (Join-Path $stagePath $requiredFile) -PathType Leaf)) {
            throw "The generated package is incomplete: $requiredFile"
        }
    }
    $manifest = Get-Content -LiteralPath (Join-Path $repositoryRoot 'COMPONENTS.json') -Raw | ConvertFrom-Json
    [ordered]@{
        Format = 1
        Application = 'iDock'
        DisplayName = $manifest.application.name
        Version = $manifest.application.version
        BuiltAtUtc = [DateTime]::UtcNow.ToString('o')
        UxPlayArchiveSha256 = $expectedArchiveHash
        Runtime = '.NET 10 Windows Desktop, framework-dependent, x64'
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $stagePath '.idock-build.json') -Encoding UTF8

    Assert-NoReparsePoint $outputPath
    if (Test-Path -LiteralPath $outputPath) {
        Assert-iDockOutputIdle $outputPath
        $backupPath = Join-Path $outputParent ('iDock-backup-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
        # Both paths were resolved above and stay in the dedicated output parent.
        Move-Item -LiteralPath $outputPath -Destination $backupPath
        Write-Host "Previous generated build retained at: $backupPath"
    }
    Move-Item -LiteralPath $stagePath -Destination $outputPath
    $completed = $true
    Write-Host "Build complete: $(Join-Path $outputPath 'iDock.exe')"
    Write-Host 'No services, firewall rules, Bluetooth pairing or existing installed-app settings were changed.'
    Write-Host 'Run .\scripts\test.ps1 before using or packaging this build.'
}
finally {
    Pop-Location
    if (-not $completed -and $stagePath -and (Test-Path -LiteralPath $stagePath)) {
        Write-Warning "Build did not finish. Intermediate files were retained for inspection: $stagePath"
    }
}
