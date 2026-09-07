# Shared helpers for the Windows PowerShell 5.1 build and test scripts.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-TestDockPrerequisites {
    if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
        throw 'TestDock builds and tests require Windows.'
    }
    if (-not [Environment]::Is64BitOperatingSystem) {
        throw 'The bundled UxPlay release requires x64 Windows.'
    }
    if ([Environment]::OSVersion.Version.Build -lt 19041) {
        throw 'Windows 10 build 19041 or later is required; Windows 11 is recommended.'
    }
    $null = Get-Command dotnet -ErrorAction Stop
    $sdkVersion = & dotnet --version
    if ($LASTEXITCODE -ne 0 -or "$sdkVersion" -notmatch '^10\.') {
        throw 'Install the .NET 10 SDK (x64), then open a new PowerShell window.'
    }
}

function Invoke-CheckedDotnet {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet failed with exit code $LASTEXITCODE."
    }
}

function Get-TestDockFullPath {
    param([Parameter(Mandatory = $true)][string]$Path, [string]$RelativeTo)
    if (-not [IO.Path]::IsPathRooted($Path)) {
        $Path = Join-Path $RelativeTo $Path
    }
    return [IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
}

function Assert-NoReparsePoint {
    param([Parameter(Mandatory = $true)][string]$Path)
    $currentPath = $Path
    while ($currentPath) {
        if (Test-Path -LiteralPath $currentPath) {
            $item = Get-Item -LiteralPath $currentPath -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Refusing a path through a symbolic link or junction: $currentPath"
            }
        }
        $parentPath = Split-Path -Parent $currentPath
        if ($parentPath -eq $currentPath) { break }
        $currentPath = $parentPath
    }
}

function Assert-TestDockOutputIdle {
    param([Parameter(Mandatory = $true)][string]$Directory)
    $prefix = $Directory.TrimEnd('\') + '\'
    foreach ($process in @(Get-Process)) {
        try {
            $processPath = $process.Path
            if ($processPath -and $processPath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Close the application using this output folder first: $processPath (PID $($process.Id))."
            }
            if (-not $processPath -and $process.ProcessName -in @('TestDock', 'BleHid.Cli', 'uxplay-windows', 'uxplay-bluetooth-beacon', 'mDNSResponder')) {
                throw "Cannot verify the path of $($process.ProcessName) (PID $($process.Id)). Close it before replacing an existing build, or choose a new -OutputDirectory."
            }
        }
        finally { $process.Dispose() }
    }
}

function Get-TestDockRestoreArguments {
    param([string]$Project, [string]$RestoreSource, [string]$PackagesDirectory)
    $arguments = @('restore', $Project, '-p:PlatformTarget=x64')
    if ($RestoreSource) { $arguments += @('--source', $RestoreSource) }
    if ($PackagesDirectory) { $arguments += @('--packages', $PackagesDirectory) }
    return $arguments
}

function Copy-TestDockSourceTree {
    param(
        [Parameter(Mandatory = $true)][string]$SourceDirectory,
        [Parameter(Mandatory = $true)][string]$DestinationDirectory
    )
    # Package reviewed, text-based build inputs only. Never traverse generated output,
    # private runtime state, VCS metadata, dependency caches, or directory junctions.
    $excludedDirectories = @(
        '.git', '.vs', '.idea', '.cache', '.artifacts', '.nuget', '.venv',
        'bin', 'obj', 'dist', 'vendor', 'packages', 'node_modules', 'venv',
        '__pycache__', 'TestResults', 'data', 'logs', 'validation', 'captures'
    )
    $sourceExtensions = @(
        '.cs', '.csproj', '.sln', '.slnx', '.props', '.targets', '.xaml',
        '.manifest', '.config', '.json', '.example', '.md', '.ps1', '.psm1',
        '.psd1', '.yml', '.yaml', '.py', '.txt', '.xml', '.resx'
    )
    $sourceNames = @('LICENSE', 'NOTICE', 'COPYING', '.gitignore', '.gitattributes', '.editorconfig')
    $privateNames = @('secrets.json', 'device.json', 'pointer-settings.json', 'pointer-pacing.json', 'hosts.json')
    Assert-NoReparsePoint $SourceDirectory
    $null = New-Item -ItemType Directory -Path $DestinationDirectory -Force
    foreach ($item in @(Get-ChildItem -LiteralPath $SourceDirectory -Force)) {
        if ($item.PSIsContainer -and $item.Name -in $excludedDirectories) { continue }
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Refusing to package a symbolic link or junction: $($item.FullName)"
        }
        $destination = Join-Path $DestinationDirectory $item.Name
        if ($item.PSIsContainer) {
            Copy-TestDockSourceTree -SourceDirectory $item.FullName -DestinationDirectory $destination
        }
        elseif ($item.Name -notin $privateNames -and $item.Name -notlike '*.local.*' -and ($item.Extension -in $sourceExtensions -or $item.Name -in $sourceNames)) {
            Copy-Item -LiteralPath $item.FullName -Destination $destination
        }
    }
}
