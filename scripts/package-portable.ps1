#Requires -Version 5.1
<#
.SYNOPSIS
Creates the portable release ZIP from a generated self-contained package.
.EXAMPLE
$build = Get-Content .\dist\releases\installer-build.json -Raw | ConvertFrom-Json
.\scripts\package-portable.ps1 -PackageDirectory $build.PayloadRelativePath
.NOTES
Does not build, install, run or publish iDock. Existing ZIP/checksums are retained
in a scoped backup. Never point this script at a live portable installation.
#>
[CmdletBinding()]
param([string]$PackageDirectory, [string]$OutputDirectory = 'dist\releases')

. (Join-Path $PSScriptRoot 'common.ps1')

function Assert-iDockPortableRelativePath {
    param([string]$RelativePath, [bool]$IsReparsePoint = $false)
    $normalized = $RelativePath.Replace('/', '\')
    $name = Split-Path -Leaf $normalized
    if ($IsReparsePoint -or [IO.Path]::IsPathRooted($normalized) -or
        $normalized -match '(^|\\)\.\.(\\|$)' -or
        $normalized -ieq 'installed.mode' -or
        $normalized -match '(^|\\)(\.git|\.cache|\.artifacts|data|logs|validation|obj|bin)(\\|$)' -or
        $name -match '\.local\.(ps1|md)$|\.(log|pfx|p12|pem|key|ble|btsnoop|dmp)$' -or
        $name -in @('pointer-settings.json', 'ui-settings.json', 'hotkey-settings.json', 'hosts.json', 'device.json', 'secrets.json', '.env') -or
        ($name -like '.env.*' -and $name -ne '.env.example')) {
        throw "Portable packaging refuses private, installed-mode, or linked content: $RelativePath"
    }
}

function Get-iDockPortableInputFiles {
    param([string]$Directory, [string]$Version)
    Assert-NoReparsePoint $Directory
    $marker = Get-Content -LiteralPath (Join-Path $Directory '.idock-build.json') -Raw | ConvertFrom-Json
    if ($marker.Format -ne 1 -or $marker.Application -ne 'iDock' -or $marker.Version -ne $Version -or -not $marker.SelfContained) {
        throw 'Portable ZIP requires the matching generated self-contained build marker.'
    }
    foreach ($required in @('iDock.exe', 'iDock.dll', 'iDock.runtimeconfig.json', 'coreclr.dll', 'hostfxr.dll', 'PresentationFramework.dll', 'Microsoft.Windows.SDK.NET.dll', 'WinRT.Runtime.dll',
        'vendor\blehid\BleHid.Cli.exe', 'vendor\blehid\BleHid.Cli.runtimeconfig.json', 'vendor\blehid\coreclr.dll',
        'vendor\blehid\hostfxr.dll', 'vendor\uxplay\uxplay-windows.exe', 'vendor\uxplay\mDNSResponder.exe',
        'LICENSE', 'THIRD_PARTY_NOTICES.md', 'README.md', 'README.en.md', 'docs\INSTALL.md', 'docs\en\INSTALL.md', 'docs\en\USAGE.md',
        'licenses\dotnet\Microsoft.NETCore.App\LICENSE.TXT', 'licenses\dotnet\Microsoft.NETCore.App\THIRD-PARTY-NOTICES.TXT',
        'licenses\dotnet\Microsoft.WindowsDesktop.App\LICENSE', 'licenses\Inno-Setup-LICENSE.txt')) {
        if (-not (Test-Path -LiteralPath (Join-Path $Directory $required) -PathType Leaf)) { throw "Portable payload is incomplete: $required" }
    }
    foreach ($configPath in @('iDock.runtimeconfig.json', 'vendor\blehid\BleHid.Cli.runtimeconfig.json')) {
        $runtime = (Get-Content -LiteralPath (Join-Path $Directory $configPath) -Raw | ConvertFrom-Json).runtimeOptions
        if (-not $runtime.PSObject.Properties['includedFrameworks'] -or
            $runtime.PSObject.Properties['framework'] -or $runtime.PSObject.Properties['frameworks']) {
            throw "Portable runtime is not self-contained: $configPath"
        }
    }
    $files = @()
    foreach ($item in @(Get-ChildItem -LiteralPath $Directory -Force -Recurse)) {
        $relative = $item.FullName.Substring($Directory.Length + 1)
        Assert-iDockPortableRelativePath $relative (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)
        if (-not $item.PSIsContainer) {
            $files += [pscustomobject]@{ Path = $item.FullName; RelativePath = $relative; Length = $item.Length; LastWriteTimeUtc = $item.LastWriteTimeUtc }
        }
    }
    return $files | Sort-Object RelativePath
}

function Invoke-iDockPortablePackaging {
    param([string]$PackagePath, [string]$ReleasePath)
    $repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
    if (-not $PackagePath) { throw '-PackageDirectory is required; a stale payload is never chosen automatically.' }
    $payload = Get-iDockFullPath $PackagePath $repositoryRoot
    $output = Get-iDockFullPath $ReleasePath $repositoryRoot
    $distPrefix = (Join-Path $repositoryRoot 'dist') + '\'
    if (-not $payload.StartsWith($distPrefix, [StringComparison]::OrdinalIgnoreCase) -or
        (Split-Path -Leaf $payload) -ne 'iDock' -or -not $output.StartsWith($distPrefix, [StringComparison]::OrdinalIgnoreCase) -or
        $output.Equals($payload, [StringComparison]::OrdinalIgnoreCase) -or $output.StartsWith($payload + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Use dedicated generated payload/release subdirectories under repository dist; never a live installation.'
    }
    Assert-NoReparsePoint $output
    $manifest = Get-Content -LiteralPath (Join-Path $repositoryRoot 'COMPONENTS.json') -Raw | ConvertFrom-Json
    $version = $manifest.application.version
    if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Release version must be a three-part numeric version.' }
    $files = @(Get-iDockPortableInputFiles $payload $version)
    $null = New-Item -ItemType Directory -Path $output -Force
    $reportPath = Join-Path $output 'installer-build.json'
    $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    $installerName = 'iDock-Setup-' + $version + '-win-x64.exe'
    $installerPath = Join-Path $output $installerName
    if ($report.Version -ne $version -or $report.Installer -ne $installerName -or -not $report.SelfContained -or
        -not (Test-Path -LiteralPath $installerPath -PathType Leaf) -or
        $report.Sha256 -ne (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash -or
        -not $payload.Equals((Get-iDockFullPath $report.PayloadRelativePath $repositoryRoot), [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Installer, its build report and the portable payload must be from the same verified build.'
    }
    $zipName = 'iDock-' + $version + '-win-x64-portable.zip'
    $zipPath = Join-Path $output $zipName
    $temporary = Join-Path $output ('.portable-' + [Guid]::NewGuid().ToString('N') + '.partial')
    # Windows PowerShell 5.1 must load the enum's assembly before resolving
    # ZipArchiveMode in the Open call; FileSystem alone does not guarantee it.
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::Open($temporary, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in $files) {
            $current = Get-Item -LiteralPath $file.Path -Force
            if ($current.Length -ne $file.Length -or $current.LastWriteTimeUtc -ne $file.LastWriteTimeUtc) {
                throw "Payload changed while packaging: $($file.RelativePath)"
            }
            # Explicit file inventory prevents ambient logs created later from
            # entering the archive. Entries have no absolute machine paths.
            $null = [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.Path, $file.RelativePath.Replace('\', '/'), [IO.Compression.CompressionLevel]::Optimal)
        }
    }
    finally { $archive.Dispose() }
    $inspection = [IO.Compression.ZipFile]::OpenRead($temporary)
    try {
        if ($inspection.Entries.Count -ne $files.Count) { throw 'Portable archive entry count does not match the payload.' }
        foreach ($entry in $inspection.Entries) { Assert-iDockPortableRelativePath $entry.FullName }
    }
    finally { $inspection.Dispose() }
    if ((Test-Path -LiteralPath $zipPath) -or (Test-Path -LiteralPath (Join-Path $output 'SHA256SUMS.txt'))) {
        $backup = Join-Path $output ('previous-portable-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
        $null = New-Item -ItemType Directory -Path $backup
        foreach ($name in @($zipName, 'SHA256SUMS.txt')) {
            $previous = Join-Path $output $name
            if (Test-Path -LiteralPath $previous -PathType Leaf) { Move-Item -LiteralPath $previous -Destination $backup }
        }
        Write-Host "Previous portable/checksum files preserved: $backup"
    }
    Move-Item -LiteralPath $temporary -Destination $zipPath
    # Hash exact final assets, not a wildcard that could include old builds,
    # backups, temporary files, or the checksum file itself.
    $lines = foreach ($name in @($installerName, $zipName, 'installer-build.json')) {
        $hash = (Get-FileHash -LiteralPath (Join-Path $output $name) -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $name"
    }
    $lines | Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS.txt') -Encoding ASCII
    Write-Host "Portable ZIP ready: $zipPath"
    Write-Host "Verified $($files.Count) files. SHA256SUMS.txt covers installer, portable ZIP and build report."
}

if ($MyInvocation.InvocationName -ne '.') { Invoke-iDockPortablePackaging $PackageDirectory $OutputDirectory }
