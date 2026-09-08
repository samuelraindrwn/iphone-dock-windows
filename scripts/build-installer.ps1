#Requires -Version 5.1
<#
.SYNOPSIS
Builds the offline, self-contained x64 installer without installing iDock.
.EXAMPLE
.\scripts\build-installer.ps1
.EXAMPLE
.\scripts\build-installer.ps1 -UxPlayArchive C:\Downloads\uxplay-windows.zip -InnoSetupInstaller C:\Downloads\innosetup-6.7.3.exe
.NOTES
Build prerequisites remain the .NET 10 SDK and Windows. End users need neither
the SDK nor a separately installed runtime. The signed Inno compiler is unpacked
in its official current-user portable mode inside .artifacts, not installed.
This script does not run the resulting installer, publish a release, or sign it.
#>
[CmdletBinding()]
param(
    [string]$UxPlayArchive,
    [string]$InnoSetupInstaller,
    [string]$RestoreSource,
    [string]$PackagesDirectory,
    [string]$OutputDirectory = 'dist\releases',
    [string]$PackageDirectory,
    [switch]$SkipBuild
)

. (Join-Path $PSScriptRoot 'common.ps1')
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$installerRoot = Join-Path $repositoryRoot 'installer'
$toolchain = (Get-Content -LiteralPath (Join-Path $installerRoot 'toolchain.json') -Raw | ConvertFrom-Json).innoSetup
$manifest = Get-Content -LiteralPath (Join-Path $repositoryRoot 'COMPONENTS.json') -Raw | ConvertFrom-Json
$version = $manifest.application.version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Installer version must be a three-part numeric version.' }
$outputPath = Get-iDockFullPath $OutputDirectory $repositoryRoot
$distRoot = Join-Path $repositoryRoot 'dist'
if (-not $outputPath.StartsWith($distRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Installer output must be a dedicated subfolder of this repository dist directory.'
}
Assert-NoReparsePoint $outputPath
if ($SkipBuild -and -not $PackageDirectory) { throw '-SkipBuild requires an explicit generated -PackageDirectory; a stale payload is never selected automatically.' }
if (-not $PackageDirectory) {
    $PackageDirectory = 'dist\installer-payload\' + [Guid]::NewGuid().ToString('N') + '\iDock'
}
$payloadPath = Get-iDockFullPath $PackageDirectory $repositoryRoot
if (-not $payloadPath.StartsWith($distRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or
    (Split-Path -Leaf $payloadPath) -ne 'iDock') {
    throw 'PackageDirectory must be a dedicated generated iDock subfolder under repository dist.'
}
$null = New-Item -ItemType Directory -Path $outputPath -Force

Push-Location $repositoryRoot
try {
    Assert-iDockPrerequisites
    & (Join-Path $installerRoot 'Test-Installer.ps1')
    if (-not $SkipBuild) {
        $buildParameters = @{ OutputDirectory = $payloadPath; SelfContained = $true; Runtime = 'win-x64' }
        if ($UxPlayArchive) { $buildParameters.UxPlayArchive = $UxPlayArchive }
        if ($RestoreSource) { $buildParameters.RestoreSource = $RestoreSource }
        if ($PackagesDirectory) { $buildParameters.PackagesDirectory = $PackagesDirectory }
        & (Join-Path $PSScriptRoot 'build.ps1') @buildParameters
    }

    # Refuse arbitrary or private installation folders. Only the generated,
    # self-contained build output may be bundled, never the user's live app root.
    Assert-NoReparsePoint $payloadPath
    $marker = Get-Content -LiteralPath (Join-Path $payloadPath '.idock-build.json') -Raw | ConvertFrom-Json
    if ($marker.Application -ne 'iDock' -or $marker.Format -ne 1 -or
        $marker.Version -ne $version -or -not $marker.SelfContained) {
        throw 'The installer requires a matching generated self-contained iDock payload.'
    }
    foreach ($required in @('iDock.exe', 'iDock.dll', 'coreclr.dll', 'hostfxr.dll', 'PresentationFramework.dll',
        'vendor\blehid\BleHid.Cli.exe', 'vendor\blehid\coreclr.dll', 'vendor\blehid\hostfxr.dll',
        'vendor\uxplay\uxplay-windows.exe', 'vendor\uxplay\mDNSResponder.exe', 'vendor\uxplay\LICENSE.rtf',
        'LICENSE', 'THIRD_PARTY_NOTICES.md', 'README.md', 'README.en.md', 'docs\INSTALL.md', 'docs\en\INSTALL.md', 'docs\en\USAGE.md', 'source\iDock\iDock.csproj',
        'licenses\dotnet\Microsoft.NETCore.App\LICENSE.TXT', 'licenses\dotnet\Microsoft.NETCore.App\THIRD-PARTY-NOTICES.TXT',
        'licenses\dotnet\Microsoft.WindowsDesktop.App\LICENSE')) {
        if (-not (Test-Path -LiteralPath (Join-Path $payloadPath $required) -PathType Leaf)) { throw "Missing installer payload file: $required" }
    }
    foreach ($file in @(Get-ChildItem -LiteralPath $payloadPath -Force -Recurse)) {
        $relative = $file.FullName.Substring($payloadPath.Length + 1)
        if (($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
            $relative -match '(^|\\)(\.git|\.cache|\.artifacts|data|logs|validation|obj|bin)(\\|$)' -or
            $file.Name -match '\.local\.(ps1|md)$|\.(log|pfx|p12|pem|key|ble|btsnoop)$' -or
            $file.Name -in @('pointer-settings.json', 'ui-settings.json', 'hosts.json', 'device.json', 'secrets.json', '.env')) {
            throw "Private, generated, or linked content is forbidden in the installer: $relative"
        }
    }
    # Include the new installer build inputs, while retaining the original manual
    # build path and all first-party/upstream licensing and source references.
    Copy-iDockSourceTree -SourceDirectory $installerRoot -DestinationDirectory (Join-Path $payloadPath 'installer')
    foreach ($file in @('iDock.iss', 'installed.mode')) {
        Copy-Item -LiteralPath (Join-Path $installerRoot $file) -Destination (Join-Path $payloadPath 'installer')
    }

    if ($InnoSetupInstaller) { $compilerInstaller = (Resolve-Path -LiteralPath $InnoSetupInstaller).Path }
    else {
        $cachePath = Join-Path $repositoryRoot ('.cache\innosetup\' + $toolchain.version)
        $null = New-Item -ItemType Directory -Path $cachePath -Force
        $compilerInstaller = Join-Path $cachePath ('innosetup-' + $toolchain.version + '.exe')
        if (-not (Test-Path -LiteralPath $compilerInstaller -PathType Leaf)) {
            $download = Join-Path $cachePath ('download-' + [Guid]::NewGuid().ToString('N') + '.exe')
            $previousTls = [Net.ServicePointManager]::SecurityProtocol
            try {
                [Net.ServicePointManager]::SecurityProtocol = $previousTls -bor [Net.SecurityProtocolType]::Tls12
                Invoke-WebRequest -Uri $toolchain.url -OutFile $download -UseBasicParsing
                if ((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash -ne $toolchain.sha256) { throw 'Inno Setup download hash mismatch.' }
                Move-Item -LiteralPath $download -Destination $compilerInstaller
            }
            finally { [Net.ServicePointManager]::SecurityProtocol = $previousTls }
        }
    }
    if ((Get-FileHash -LiteralPath $compilerInstaller -Algorithm SHA256).Hash -ne $toolchain.sha256) { throw 'Inno Setup installer hash mismatch; it will not be executed.' }
    $signature = Get-AuthenticodeSignature -LiteralPath $compilerInstaller
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -ne $toolchain.publisherSubject) {
        throw 'Inno Setup requires a valid Authenticode signature from the pinned publisher.'
    }
    $compilerDirectory = Join-Path $repositoryRoot ('.artifacts\installer-compiler\' + [Guid]::NewGuid().ToString('N'))
    Assert-NoReparsePoint $compilerDirectory
    $null = New-Item -ItemType Directory -Path $compilerDirectory -Force
    # Official portable mode disables registration, icons and associations. A
    # fresh directory also prevents reusing an altered cached compiler payload.
    $compilerProcess = Start-Process -FilePath $compilerInstaller -ArgumentList @('/PORTABLE=1', '/CURRENTUSER', '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/NOICONS', '/SP-', ('/DIR="' + $compilerDirectory + '"')) -WindowStyle Hidden -Wait -PassThru
    if ($compilerProcess.ExitCode -ne 0) { throw "Portable compiler extraction failed: $($compilerProcess.ExitCode)" }
    $compiler = Join-Path $compilerDirectory 'ISCC.exe'
    $compilerSignature = Get-AuthenticodeSignature -LiteralPath $compiler
    if ($compilerSignature.Status -ne 'Valid' -or $compilerSignature.SignerCertificate.Subject -ne $toolchain.publisherSubject) { throw 'Extracted compiler signature verification failed.' }
    Copy-Item -LiteralPath (Join-Path $compilerDirectory 'license.txt') -Destination (Join-Path $payloadPath 'licenses\Inno-Setup-LICENSE.txt')

    $artifactName = 'iDock-Setup-' + $version + '-win-x64.exe'
    $artifact = Join-Path $outputPath $artifactName
    if (Test-Path -LiteralPath $artifact) {
        $backup = Join-Path $outputPath ('previous-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
        $null = New-Item -ItemType Directory -Path $backup
        foreach ($name in @($artifactName, 'SHA256SUMS.txt', 'installer-build.json')) {
            $previous = Join-Path $outputPath $name
            if (Test-Path -LiteralPath $previous -PathType Leaf) { Move-Item -LiteralPath $previous -Destination $backup }
        }
        Write-Host "Previous installer output preserved in: $backup"
    }
    & $compiler '/Qp' ("/DPackageDir=$payloadPath") ("/DReleaseDir=$outputPath") ("/DAppVersion=$version") (Join-Path $installerRoot 'iDock.iss')
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $artifact -PathType Leaf)) { throw 'Inno Setup compilation failed.' }
    $hash = (Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $artifactName" | Set-Content -LiteralPath (Join-Path $outputPath 'SHA256SUMS.txt') -Encoding ASCII
    $signatureState = (Get-AuthenticodeSignature -LiteralPath $artifact).Status.ToString()
    [ordered]@{
        Application = 'iDock for Windows'; Version = $version; Installer = $artifactName
        Sha256 = $hash; SizeBytes = (Get-Item -LiteralPath $artifact).Length
        BuiltAtUtc = [DateTime]::UtcNow.ToString('o'); Architecture = 'win-x64'; SelfContained = $true
        PayloadRelativePath = $payloadPath.Substring($repositoryRoot.Length + 1)
        InnoSetupVersion = $toolchain.version; CompilerDownloadSha256 = $toolchain.sha256
        InstallerSignature = $signatureState; FreshMachineInstallTested = $false
        RuntimeVersion = (Get-Content -LiteralPath (Join-Path $payloadPath 'iDock.runtimeconfig.json') -Raw | ConvertFrom-Json).runtimeOptions.includedFrameworks
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $outputPath 'installer-build.json') -Encoding UTF8
    Write-Host "Installer built: $artifact"
    Write-Host "SHA256: $hash"
    Write-Host "Installer signature: $signatureState. The compiler is signed; that does not sign iDock."
    Write-Host 'No iDock installation or service/Firewall/Bluetooth change was performed. Test install/upgrade/uninstall on a clean Windows VM before publishing.'
}
finally { Pop-Location }
