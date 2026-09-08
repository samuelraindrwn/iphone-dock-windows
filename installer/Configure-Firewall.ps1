#Requires -Version 5.1
<#
Product-scoped installer helper. This script never changes Bonjour, Bluetooth,
network profiles, or the global Firewall policy. Dot-source to test pure helpers.
#>
[CmdletBinding()]
param(
    [ValidateSet('Preflight', 'Install', 'Uninstall', 'PreflightUninstall')][string]$Mode = 'Preflight',
    [string]$InstallDirectory,
    [int]$InstallerProcessId,
    [string]$UninstallerPath,
    [switch]$AllowPublicWireless
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-iDockFirewallDefinitions {
    param([string]$Directory, [switch]$AllowPublicWireless)
    foreach ($entry in @(@('TCP', 6), @('UDP', 17))) {
        [pscustomobject]@{
            Name = 'iDock.AirPlay.' + $entry[0] + '.Private.v1'
            Description = 'iDock installer owned rule {4C3A1080-B451-4B31-B7D9-08237950642B}; local trusted-network AirPlay only.'
            ApplicationName = Join-Path $Directory 'vendor\uxplay\uxplay-windows.exe'
            Grouping = 'iDock for Windows'
            Protocol = [int]$entry[1]
            Direction = 1
            Action = 1
            Enabled = $true
            Profiles = 2 # NET_FW_PROFILE2_PRIVATE, never DOMAIN or PUBLIC.
            LocalAddresses = '*'
            RemoteAddresses = 'LocalSubnet'
            LocalPorts = '*'
            RemotePorts = '*'
            InterfaceTypes = 'All'
            EdgeTraversal = $false
            ServiceName = $null
            Interfaces = $null
            IcmpTypesAndCodes = $null
            EdgeTraversalOptions = 0
            LocalAppPackageId = $null
            LocalUserOwner = $null
            LocalUserAuthorizedList = $null
            RemoteUserAuthorizedList = $null
            RemoteMachineAuthorizedList = $null
            SecureFlags = 0
        }
    }
    if ($AllowPublicWireless) { Get-iDockPublicWirelessDefinitions $Directory }
}

function Get-iDockPublicWirelessDefinitions {
    param([string]$Directory)
    # Reuse the same complete INetFwRule3 shape without changing the established
    # v0.5.0 Private definitions or giving the new rules the old ownership names.
    foreach ($definition in @(Get-iDockFirewallDefinitions $Directory)) {
        $definition.Name = $definition.Name.Replace('.Private.v1', '.PublicWireless.v1')
        $definition.Description = 'iDock installer owned rule {4C3A1080-B451-4B31-B7D9-08237950642B}; explicit opt-in AirPlay from local peers on any Public wireless network, not a single SSID.'
        $definition.Profiles = 4 # NET_FW_PROFILE2_PUBLIC; never changes network trust.
        $definition.InterfaceTypes = 'Wireless'
        $definition
    }
}

function Test-iDockOwnedFirewallRule {
    param($Rule, $Definition)
    # Compare individual values, not mutable COM/CIM wrapper identity. A changed
    # rule belongs to the administrator; fail closed instead of overwriting it.
    foreach ($property in $Definition.PSObject.Properties.Name) {
        if ("$($Rule.$property)" -ine "$($Definition.$property)") { return $false }
    }
    return $true
}

function Get-iDockFirewallMatches {
    param($Policy, [string]$Name)
    return @($Policy.Rules | Where-Object { $_.Name -ieq $Name })
}

function Assert-iDockFirewallPlan {
    param($Policy, $Definitions)
    foreach ($definition in $Definitions) {
        $matches = @(Get-iDockFirewallMatches $Policy $definition.Name)
        if ($matches.Count -gt 1 -or ($matches.Count -eq 1 -and -not (Test-iDockOwnedFirewallRule $matches[0] $definition))) {
            throw "Firewall rule name is already used or was modified: $($definition.Name). No existing rule was changed. Ask your administrator to review this rule before changing the Public Wi-Fi option or continuing Setup."
        }
    }
}

function New-iDockFirewallRuleObject {
    return New-Object -ComObject HNetCfg.FWRule
}

function New-iDockFirewallRuleFromDefinition {
    param($Definition)
    $rule = New-iDockFirewallRuleObject
    foreach ($property in $Definition.PSObject.Properties.Name) {
        # Unset optional INetFwRule3 defaults may be null; their setters do not
        # uniformly accept null. Inspect all defaults as part of verification.
        if ($null -ne $Definition.$property) { $rule.$property = $Definition.$property }
    }
    if (-not (Test-iDockOwnedFirewallRule $rule $Definition)) {
        throw "In-memory Firewall scope verification failed: $($Definition.Name)"
    }
    return $rule
}

function Invoke-iDockFirewallChanges {
    param($Policy, $Definitions, [ValidateSet('Preflight', 'Install', 'Uninstall')][string]$Operation, [object[]]$RemoveDefinitions = @())
    if ($Operation -eq 'Uninstall') {
        foreach ($definition in $Definitions) {
            $matches = @(Get-iDockFirewallMatches $Policy $definition.Name)
            if ($matches.Count -eq 1 -and (Test-iDockOwnedFirewallRule $matches[0] $definition)) {
                $Policy.Rules.Remove($definition.Name)
            }
            elseif ($matches.Count -gt 0) {
                Write-Warning "Preserved a modified or ambiguous Firewall rule: $($definition.Name)"
            }
        }
        return
    }
    # This union also validates unchecked opt-in names. A modified public rule
    # must not be silently retained or overwritten when the user deselects it.
    Assert-iDockFirewallPlan $Policy (@($Definitions) + @($RemoveDefinitions))
    if ($Operation -eq 'Preflight') { return }
    $created = @()
    $removed = @()
    try {
        foreach ($definition in $Definitions) {
            # Re-check immediately before each write to detect concurrent edits.
            Assert-iDockFirewallPlan $Policy @($definition)
            if (@(Get-iDockFirewallMatches $Policy $definition.Name).Count -eq 1) { continue }
            $rule = New-iDockFirewallRuleFromDefinition $definition
            $Policy.Rules.Add($rule)
            $created += $definition
            $actual = @(Get-iDockFirewallMatches $Policy $definition.Name)
            if ($actual.Count -ne 1 -or -not (Test-iDockOwnedFirewallRule $actual[0] $definition)) {
                throw "Firewall verification failed: $($definition.Name)"
            }
        }
        foreach ($definition in $RemoveDefinitions) {
            Assert-iDockFirewallPlan $Policy @($definition)
            if (@(Get-iDockFirewallMatches $Policy $definition.Name).Count -eq 0) { continue }
            # Record exact ownership before removing so a later failed removal
            # can restore the earlier one. Re-check names during rollback too.
            $removed += $definition
            $Policy.Rules.Remove($definition.Name)
            if (@(Get-iDockFirewallMatches $Policy $definition.Name).Count -ne 0) {
                throw "Firewall deselection verification failed: $($definition.Name)"
            }
        }
    }
    catch {
        $failure = $_
        foreach ($definition in $created) {
            try {
                $actual = @(Get-iDockFirewallMatches $Policy $definition.Name)
                if ($actual.Count -eq 1 -and (Test-iDockOwnedFirewallRule $actual[0] $definition)) { $Policy.Rules.Remove($definition.Name) }
                elseif ($actual.Count -gt 0) { Write-Warning "Could not safely roll back changed rule: $($definition.Name)" }
            }
            catch { Write-Warning "Rollback needs administrator review: $($definition.Name): $($_.Exception.Message)" }
        }
        foreach ($definition in $removed) {
            try {
                $actual = @(Get-iDockFirewallMatches $Policy $definition.Name)
                if ($actual.Count -eq 0) {
                    $Policy.Rules.Add((New-iDockFirewallRuleFromDefinition $definition))
                    $actual = @(Get-iDockFirewallMatches $Policy $definition.Name)
                }
                if ($actual.Count -ne 1 -or -not (Test-iDockOwnedFirewallRule $actual[0] $definition)) {
                    Write-Warning "Could not safely restore a removed rule; administrator review required: $($definition.Name)"
                }
            }
            catch { Write-Warning "Rollback needs administrator review: $($definition.Name): $($_.Exception.Message)" }
        }
        throw $failure
    }
}

function Assert-iDockInstallerDirectory {
    param([string]$Directory)
    if (-not $Directory) { throw 'InstallDirectory is required.' }
    $resolved = [IO.Path]::GetFullPath($Directory).TrimEnd('\')
    $expected = Join-Path ([Environment]::GetFolderPath('ProgramFiles')) 'iDock'
    if (-not $resolved.Equals($expected, [StringComparison]::OrdinalIgnoreCase)) {
        throw "This installer supports only the protected application directory: $expected"
    }
    $current = $resolved
    while ($current) {
        if (Test-Path -LiteralPath $current) {
            if (((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Installation path contains a symbolic link or junction: $current"
            }
        }
        $current = Split-Path -Parent $current
    }
    if (Test-Path -LiteralPath $resolved -PathType Container) {
        foreach ($item in @(Get-ChildItem -LiteralPath $resolved -Force -Recurse)) {
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Installation folder contains a symbolic link or junction: $($item.FullName)"
            }
        }
    }
    return $resolved
}

function Assert-iDockUninstallerIdentity {
    param([string]$Directory, [string]$OriginalPath, [string]$OriginalHash, [string]$CallerHash)
    if (-not $OriginalPath) { throw 'Original uninstaller identity is required.' }
    $original = [IO.Path]::GetFullPath($OriginalPath)
    if (-not (Split-Path -Parent $original).Equals($Directory, [StringComparison]::OrdinalIgnoreCase) -or
        (Split-Path -Leaf $original) -notmatch '^unins\d+\.exe$' -or
        $OriginalHash -notmatch '^[0-9a-fA-F]{64}$' -or $OriginalHash -ine $CallerHash) {
        throw 'The original uninstaller is not the same verified executable as the active uninstall process.'
    }
    return $original
}

function Assert-iDockInstallerProcesses {
    param([string]$Directory, [object[]]$Processes, [int]$CallerProcessId, [string]$VerifiedUninstallerPath)
    foreach ($process in $Processes) {
        # Inno passes its own PID; never exempt other processes by a loose
        # uninstaller filename pattern. No process is killed by this helper.
        if ($CallerProcessId -gt 0 -and $process.Id -eq $CallerProcessId) { continue }
        $path = $process.Path
        # Inno's elevated/unelevated first-phase stubs wait at the original
        # path while its byte-identical TEMP copy performs uninstallation.
        # This exception is granted only after comparing both image hashes.
        if ($path -and $VerifiedUninstallerPath -and $path.Equals($VerifiedUninstallerPath, [StringComparison]::OrdinalIgnoreCase)) { continue }
        if (-not $path -and $process.ProcessName -in @('iDock', 'BleHid.Cli', 'uxplay-windows', 'uxplay-bluetooth-beacon')) {
            throw "Cannot verify an active iDock component (PID $($process.Id)). Close iDock, control and mirroring before continuing."
        }
        if ($path -and $path.StartsWith($Directory + '\', [StringComparison]::OrdinalIgnoreCase) -and
            -not $path.Equals((Join-Path $Directory 'vendor\uxplay\mDNSResponder.exe'), [StringComparison]::OrdinalIgnoreCase)) {
            throw "Close iDock and its AirPlay/control processes before continuing (PID $($process.Id))."
        }
    }
}

function Invoke-iDockInstallerFirewall {
    param([string]$Operation, [string]$Directory, [int]$CallerProcessId, [string]$OriginalUninstallerPath, [bool]$PublicWirelessAllowed = $false)
    if (-not [Environment]::Is64BitProcess) { throw 'The installer requires 64-bit Windows PowerShell.' }
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Administrator permission is required by Setup.' }
    $directory = Assert-iDockInstallerDirectory $Directory
    $verifiedUninstaller = $null
    if ($Operation -in @('PreflightUninstall', 'Uninstall')) {
        if ($CallerProcessId -le 0) { throw 'The uninstall process identity is required.' }
        $caller = Get-Process -Id $CallerProcessId -ErrorAction Stop
        try {
            if (-not $caller.Path) { throw 'Cannot verify the uninstall process executable.' }
            $originalHash = (Get-FileHash -LiteralPath $OriginalUninstallerPath -Algorithm SHA256).Hash
            $callerHash = (Get-FileHash -LiteralPath $caller.Path -Algorithm SHA256).Hash
            $verifiedUninstaller = Assert-iDockUninstallerIdentity $directory $OriginalUninstallerPath $originalHash $callerHash
        }
        finally { $caller.Dispose() }
    }
    $processes = @(Get-Process)
    try { Assert-iDockInstallerProcesses $directory $processes $CallerProcessId $verifiedUninstaller }
    finally { foreach ($process in $processes) { $process.Dispose() } }
    # Uninstall readiness is independent of Firewall ownership. Modified rules
    # should be preserved, not prevent safe removal of an idle application.
    if ($Operation -eq 'PreflightUninstall') { Write-Output 'iDock is closed and ready to uninstall.'; return }
    $policy = New-Object -ComObject HNetCfg.FwPolicy2
    $definitions = @(Get-iDockFirewallDefinitions $directory -AllowPublicWireless:($PublicWirelessAllowed -or $Operation -eq 'Uninstall'))
    $removeDefinitions = @()
    if (-not $PublicWirelessAllowed -and $Operation -ne 'Uninstall') { $removeDefinitions = @(Get-iDockPublicWirelessDefinitions $directory) }
    Invoke-iDockFirewallChanges $policy $definitions $Operation $removeDefinitions
    Write-Output "iDock Firewall $Operation completed. Public wireless option: $PublicWirelessAllowed. Bonjour, Bluetooth, network trust and other Firewall rules were not modified."
}

if ($MyInvocation.InvocationName -ne '.') {
    try { Invoke-iDockInstallerFirewall $Mode $InstallDirectory $InstallerProcessId $UninstallerPath ([bool]$AllowPublicWireless) }
    catch { Write-Error $_ -ErrorAction Continue; exit 1 }
}
