#Requires -Version 5.1
# Hardware-free checks. COM rule objects are tested in memory only; no real
# Firewall policy, installer, service or radio is modified or opened.
$ErrorActionPreference = 'Stop'
$WarningPreference = 'SilentlyContinue' # Expected preservation warnings are exercised by the mocks.
. (Join-Path $PSScriptRoot 'Configure-Firewall.ps1')

class iDockTestFirewallRules : System.Collections.IEnumerable {
    [System.Collections.ArrayList]$Items = [System.Collections.ArrayList]::new()
    [int]$AddCalls = 0
    [int]$RemoveCalls = 0
    [int]$FailOnAdd = 0
    [int]$FailOnRemove = 0
    [string]$MutateOnFailedRemove = ''
    [System.Collections.IEnumerator] GetEnumerator() { return $this.Items.GetEnumerator() }
    [void] Add([object]$rule) {
        $this.AddCalls++
        if ($this.FailOnAdd -eq $this.AddCalls) { throw 'Simulated add failure' }
        $null = $this.Items.Add($rule)
    }
    [void] Remove([string]$name) {
        $this.RemoveCalls++
        if ($this.FailOnRemove -eq $this.RemoveCalls) {
            foreach ($item in $this.Items) {
                if ($item.Name -eq $this.MutateOnFailedRemove) { $item.RemoteAddresses = '*' }
            }
            throw 'Simulated remove failure'
        }
        for ($index = $this.Items.Count - 1; $index -ge 0; $index--) {
            if ($this.Items[$index].Name -eq $name) { $this.Items.RemoveAt($index) }
        }
    }
}

function New-TestPolicy { return [pscustomobject]@{ Rules = [iDockTestFirewallRules]::new() } }
function New-iDockFirewallRuleObject {
    $properties = [ordered]@{}
    foreach ($property in $script:definitions[0].PSObject.Properties.Name) { $properties[$property] = $null }
    return [pscustomobject]$properties
}
function Copy-TestDefinition($definition) {
    $properties = [ordered]@{}
    foreach ($property in $definition.PSObject.Properties.Name) { $properties[$property] = $definition.$property }
    return [pscustomobject]$properties
}
$script:count = 0
function Assert-Installer($condition, [string]$message) {
    if (-not $condition) { throw "Installer test failed: $message" }
    $script:count++
}
function Assert-InstallerThrows([scriptblock]$action, [string]$message) {
    $threw = $false
    try { & $action } catch { $threw = $true }
    Assert-Installer $threw $message
}

$script:definitions = @(Get-iDockFirewallDefinitions 'C:\Program Files\iDock')
Assert-Installer ($definitions.Count -eq 2) 'two receiver rules only'
foreach ($definition in $definitions) {
    Assert-Installer ($definition.Profiles -eq 2) 'private profile only'
    Assert-Installer ($definition.RemoteAddresses -eq 'LocalSubnet') 'local subnet only'
    Assert-Installer ($definition.ApplicationName -eq 'C:\Program Files\iDock\vendor\uxplay\uxplay-windows.exe') 'receiver only'
    Assert-Installer (-not $definition.EdgeTraversal) 'no edge traversal'
    Assert-Installer (Test-iDockOwnedFirewallRule (Copy-TestDefinition $definition) $definition) 'equal values, distinct wrapper'
    foreach ($property in $definition.PSObject.Properties.Name) {
        $changed = Copy-TestDefinition $definition
        $changed.$property = 'changed'
        Assert-Installer (-not (Test-iDockOwnedFirewallRule $changed $definition)) "reject changed $property"
    }
    $caseOnly = Copy-TestDefinition $definition
    $caseOnly.ApplicationName = $caseOnly.ApplicationName.ToLowerInvariant()
    Assert-Installer (Test-iDockOwnedFirewallRule $caseOnly $definition) 'Windows path case normalization'
}

$policy = New-TestPolicy
Invoke-iDockFirewallChanges $policy $definitions 'Preflight'
Assert-Installer ($policy.Rules.AddCalls -eq 0 -and $policy.Rules.RemoveCalls -eq 0) 'preflight is read-only'
Invoke-iDockFirewallChanges $policy $definitions 'Install'
Assert-Installer ($policy.Rules.Items.Count -eq 2) 'fresh install creates rules'
Invoke-iDockFirewallChanges $policy $definitions 'Install'
Assert-Installer ($policy.Rules.AddCalls -eq 2) 'upgrade reuses exact owned rules'
Invoke-iDockFirewallChanges $policy $definitions 'Uninstall'
Assert-Installer ($policy.Rules.Items.Count -eq 0) 'uninstall removes owned rules'
Invoke-iDockFirewallChanges $policy $definitions 'Uninstall'
Assert-Installer ($policy.Rules.RemoveCalls -eq 2) 'repeat uninstall does not remove anything else'

foreach ($property in $definitions[0].PSObject.Properties.Name | Where-Object { $_ -ne 'Name' }) {
    $policy = New-TestPolicy
    $foreign = Copy-TestDefinition $definitions[0]
    $foreign.$property = 'administrator change'
    $policy.Rules.Add($foreign)
    Assert-InstallerThrows { Invoke-iDockFirewallChanges $policy $definitions 'Install' } "collision $property fails before writing"
    Assert-Installer ($policy.Rules.AddCalls -eq 1 -and $policy.Rules.RemoveCalls -eq 0) 'collision causes no changes'
    Invoke-iDockFirewallChanges $policy $definitions 'Uninstall' -WarningAction SilentlyContinue
    Assert-Installer ($policy.Rules.Items.Count -eq 1) 'modified rule preserved during uninstall'
}

$policy = New-TestPolicy
$policy.Rules.Add((Copy-TestDefinition $definitions[0]))
$policy.Rules.Add((Copy-TestDefinition $definitions[0]))
Assert-InstallerThrows { Invoke-iDockFirewallChanges $policy $definitions 'Install' } 'ambiguous duplicate rule names'
Invoke-iDockFirewallChanges $policy $definitions 'Uninstall' -WarningAction SilentlyContinue
Assert-Installer ($policy.Rules.Items.Count -eq 2) 'ambiguous rules preserved'

$policy = New-TestPolicy
$policy.Rules.FailOnAdd = 2
Assert-InstallerThrows { Invoke-iDockFirewallChanges $policy $definitions 'Install' } 'failed second add propagates'
Assert-Installer ($policy.Rules.Items.Count -eq 0 -and $policy.Rules.RemoveCalls -eq 1) 'rollback only first newly created rule'

$policy = New-TestPolicy
$policy.Rules.Add((Copy-TestDefinition $definitions[0]))
$policy.Rules.FailOnAdd = 2
Assert-InstallerThrows { Invoke-iDockFirewallChanges $policy $definitions 'Install' } 'failed upgrade add propagates'
Assert-Installer ($policy.Rules.Items.Count -eq 1 -and $policy.Rules.RemoveCalls -eq 0) 'pre-existing rule not rolled back'

$policy = New-TestPolicy
$unrelated = Copy-TestDefinition $definitions[0]
$unrelated.Name = 'Another application'
$policy.Rules.Add($unrelated)
Invoke-iDockFirewallChanges $policy $definitions 'Install'
Invoke-iDockFirewallChanges $policy $definitions 'Uninstall'
Assert-Installer ($policy.Rules.Items.Count -eq 1 -and $policy.Rules.Items[0].Name -eq 'Another application') 'unrelated rules survive entire lifecycle'

$publicDefinitions = @(Get-iDockPublicWirelessDefinitions 'C:\Program Files\iDock')
$allDefinitions = @(Get-iDockFirewallDefinitions 'C:\Program Files\iDock' -AllowPublicWireless)
Assert-Installer ($allDefinitions.Count -eq 4 -and $publicDefinitions.Count -eq 2) 'opt-in adds exactly two separate rules'
for ($index = 0; $index -lt 2; $index++) {
    Assert-Installer (Test-iDockOwnedFirewallRule $allDefinitions[$index] $definitions[$index]) 'v0.5.0 private definitions remain exact during opt-in'
}
foreach ($definition in $publicDefinitions) {
    Assert-Installer ($definition.Profiles -eq 4) 'public opt-in only uses Public profile'
    Assert-Installer ($definition.InterfaceTypes -ceq 'Wireless') 'public opt-in excludes wired interfaces'
    Assert-Installer ($definition.RemoteAddresses -ceq 'LocalSubnet' -and $definition.LocalAddresses -ceq '*') 'public local subnet scope without user-specific addresses'
    Assert-Installer ($definition.ApplicationName -ceq 'C:\Program Files\iDock\vendor\uxplay\uxplay-windows.exe') 'public receiver-only application scope'
    Assert-Installer (-not $definition.EdgeTraversal -and $definition.EdgeTraversalOptions -eq 0) 'public rules have no edge traversal'
    Assert-Installer ($definition.Name -match '\.PublicWireless\.v1$') 'public family has distinct ownership names'
    foreach ($property in $definition.PSObject.Properties.Name) {
        $modified = Copy-TestDefinition $definition
        $modified.$property = 'administrator change'
        Assert-Installer (-not (Test-iDockOwnedFirewallRule $modified $definition)) "public ownership compares full INetFwRule3 $property"
    }
}
foreach ($definition in $allDefinitions) {
    # No policy object is created or Rules.Add called for these COM objects.
    $memoryRule = New-Object -ComObject HNetCfg.FWRule
    foreach ($property in $definition.PSObject.Properties.Name) {
        if ($null -ne $definition.$property) { $memoryRule.$property = $definition.$property }
    }
    Assert-Installer (Test-iDockOwnedFirewallRule $memoryRule $definition) 'real in-memory COM defaults and selected scope roundtrip'
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($memoryRule)
}

$policy = New-TestPolicy
Invoke-iDockFirewallChanges $policy $definitions 'Preflight' $publicDefinitions
Assert-Installer ($policy.Rules.AddCalls -eq 0 -and $policy.Rules.RemoveCalls -eq 0) 'default preflight validates both families read-only'
Invoke-iDockFirewallChanges $policy $definitions 'Install' $publicDefinitions
Assert-Installer ($policy.Rules.Items.Count -eq 2 -and $policy.Rules.RemoveCalls -eq 0) 'unchecked fresh install remains private-only'
$privateBefore = @($policy.Rules.Items)
Invoke-iDockFirewallChanges $policy $allDefinitions 'Install'
Assert-Installer ($policy.Rules.Items.Count -eq 4 -and $policy.Rules.AddCalls -eq 4) 'v0.5.0 upgrade opt-in adds only public pair'
Assert-Installer ([object]::ReferenceEquals($policy.Rules.Items[0], $privateBefore[0]) -and [object]::ReferenceEquals($policy.Rules.Items[1], $privateBefore[1])) 'upgrade preserves existing private objects'
Invoke-iDockFirewallChanges $policy $allDefinitions 'Install'
Assert-Installer ($policy.Rules.AddCalls -eq 4 -and $policy.Rules.RemoveCalls -eq 0) 'opt-in repeat is idempotent'
Invoke-iDockFirewallChanges $policy $definitions 'Install' $publicDefinitions
Assert-Installer ($policy.Rules.Items.Count -eq 2 -and $policy.Rules.RemoveCalls -eq 2) 'deselection actively revokes public pair'
Assert-Installer ([object]::ReferenceEquals($policy.Rules.Items[0], $privateBefore[0]) -and [object]::ReferenceEquals($policy.Rules.Items[1], $privateBefore[1])) 'deselection never recreates private rules'
Invoke-iDockFirewallChanges $policy $definitions 'Install' $publicDefinitions
Assert-Installer ($policy.Rules.RemoveCalls -eq 2) 'repeat deselection makes no unnecessary removals'

$policy = New-TestPolicy
Invoke-iDockFirewallChanges $policy $allDefinitions 'Install'
Assert-Installer ($policy.Rules.Items.Count -eq 4) 'fresh opt-in creates all four rules'
Invoke-iDockFirewallChanges $policy $allDefinitions 'Uninstall'
Assert-Installer ($policy.Rules.Items.Count -eq 0 -and $policy.Rules.RemoveCalls -eq 4) 'uninstall considers both families independent of task selection'

foreach ($property in $publicDefinitions[0].PSObject.Properties.Name | Where-Object { $_ -ne 'Name' }) {
    $policy = New-TestPolicy
    $modified = Copy-TestDefinition $publicDefinitions[0]
    $modified.$property = 'administrator change'
    $null = $policy.Rules.Items.Add($modified)
    Assert-InstallerThrows { Invoke-iDockFirewallChanges $policy $allDefinitions 'Preflight' } "opt-in collision $property fails preflight"
    Assert-InstallerThrows { Invoke-iDockFirewallChanges $policy $definitions 'Preflight' $publicDefinitions } "deselection collision $property fails preflight"
    Assert-InstallerThrows { Invoke-iDockFirewallChanges $policy $definitions 'Install' $publicDefinitions } "deselection never overwrites modified $property"
    Assert-Installer ($policy.Rules.AddCalls -eq 0 -and $policy.Rules.RemoveCalls -eq 0) 'modified public rule causes no writes in either plan'
}
$policy = New-TestPolicy
$null = $policy.Rules.Items.Add((Copy-TestDefinition $publicDefinitions[0]))
$null = $policy.Rules.Items.Add((Copy-TestDefinition $publicDefinitions[0]))
Assert-InstallerThrows { Invoke-iDockFirewallChanges $policy $definitions 'Preflight' $publicDefinitions } 'duplicate public names block deselection before changes'
Invoke-iDockFirewallChanges $policy $allDefinitions 'Uninstall'
Assert-Installer ($policy.Rules.Items.Count -eq 2 -and $policy.Rules.RemoveCalls -eq 0) 'uninstall preserves ambiguous public names'

$policy = New-TestPolicy
foreach ($definition in $allDefinitions) { $null = $policy.Rules.Items.Add((Copy-TestDefinition $definition)) }
$policy.Rules.Items[2].RemoteAddresses = '*'
Invoke-iDockFirewallChanges $policy $allDefinitions 'Uninstall'
Assert-Installer ($policy.Rules.Items.Count -eq 1 -and $policy.Rules.Items[0].Name -eq $publicDefinitions[0].Name -and $policy.Rules.Items[0].RemoteAddresses -eq '*') 'uninstall removes exact rules but preserves modified public rule'

$policy = New-TestPolicy
foreach ($definition in $definitions) { $null = $policy.Rules.Items.Add((Copy-TestDefinition $definition)) }
$privateBefore = @($policy.Rules.Items)
$policy.Rules.FailOnAdd = 2
Assert-InstallerThrows { Invoke-iDockFirewallChanges $policy $allDefinitions 'Install' } 'partial opt-in upgrade failure is reported'
Assert-Installer ($policy.Rules.Items.Count -eq 2 -and $policy.Rules.RemoveCalls -eq 1) 'failed public pair removes only its newly created first rule'
Assert-Installer ([object]::ReferenceEquals($policy.Rules.Items[0], $privateBefore[0]) -and [object]::ReferenceEquals($policy.Rules.Items[1], $privateBefore[1])) 'failed opt-in leaves preexisting private objects untouched'

$policy = New-TestPolicy
$policy.Rules.FailOnAdd = 4
Assert-InstallerThrows { Invoke-iDockFirewallChanges $policy $allDefinitions 'Install' } 'fresh opt-in fourth add failure reported'
Assert-Installer ($policy.Rules.Items.Count -eq 0 -and $policy.Rules.RemoveCalls -eq 3) 'fresh failed opt-in rolls back only newly created rules'

$policy = New-TestPolicy
foreach ($definition in $allDefinitions) { $null = $policy.Rules.Items.Add((Copy-TestDefinition $definition)) }
$privateBefore = @($policy.Rules.Items[0], $policy.Rules.Items[1])
$policy.Rules.FailOnRemove = 2
Assert-InstallerThrows { Invoke-iDockFirewallChanges $policy $definitions 'Install' $publicDefinitions } 'partial deselection failure is reported'
Assert-Installer ($policy.Rules.Items.Count -eq 4 -and $policy.Rules.AddCalls -eq 1) 'partial deselection restores exactly the removed public rule'
foreach ($definition in $allDefinitions) {
    $restored = @(Get-iDockFirewallMatches $policy $definition.Name)
    Assert-Installer ($restored.Count -eq 1 -and (Test-iDockOwnedFirewallRule $restored[0] $definition)) 'deselection rollback preserves exact starting scopes'
}
Assert-Installer ([object]::ReferenceEquals($policy.Rules.Items[0], $privateBefore[0]) -and [object]::ReferenceEquals($policy.Rules.Items[1], $privateBefore[1])) 'deselection rollback never touches preexisting private objects'

$policy = New-TestPolicy
foreach ($definition in $publicDefinitions) { $null = $policy.Rules.Items.Add((Copy-TestDefinition $definition)) }
$policy.Rules.FailOnRemove = 2
Assert-InstallerThrows { Invoke-iDockFirewallChanges $policy $definitions 'Install' $publicDefinitions } 'failed mixed create/revoke plan is reported'
Assert-Installer ($policy.Rules.Items.Count -eq 2) 'mixed rollback restores original rule count'
foreach ($definition in $publicDefinitions) {
    $restored = @(Get-iDockFirewallMatches $policy $definition.Name)
    Assert-Installer ($restored.Count -eq 1 -and (Test-iDockOwnedFirewallRule $restored[0] $definition)) 'mixed rollback restores public scope without retaining new private rules'
}

$policy = New-TestPolicy
foreach ($definition in $allDefinitions) { $null = $policy.Rules.Items.Add((Copy-TestDefinition $definition)) }
$policy.Rules.FailOnRemove = 2
$policy.Rules.MutateOnFailedRemove = $publicDefinitions[1].Name
Assert-InstallerThrows { Invoke-iDockFirewallChanges $policy $definitions 'Install' $publicDefinitions } 'concurrently modified deselection failure is reported'
$changed = @(Get-iDockFirewallMatches $policy $publicDefinitions[1].Name)
Assert-Installer ($changed.Count -eq 1 -and $changed[0].RemoteAddresses -eq '*') 'rollback never overwrites concurrent administrator scope change'
$restored = @(Get-iDockFirewallMatches $policy $publicDefinitions[0].Name)
Assert-Installer ($restored.Count -eq 1 -and (Test-iDockOwnedFirewallRule $restored[0] $publicDefinitions[0])) 'rollback still restores independently removed exact rule'

$testDirectory = 'C:\Program Files\iDock'
Assert-iDockInstallerProcesses $testDirectory @() 100
Assert-Installer $true 'empty process inventory is idle'
foreach ($engine in @('iDock', 'BleHid.Cli', 'uxplay-windows', 'uxplay-bluetooth-beacon')) {
    Assert-InstallerThrows { Assert-iDockInstallerProcesses $testDirectory @([pscustomobject]@{ Id = 42; ProcessName = $engine; Path = $null }) 100 } "unknown active $engine fails closed"
    Assert-InstallerThrows { Assert-iDockInstallerProcesses $testDirectory @([pscustomobject]@{ Id = 42; ProcessName = $engine; Path = "$testDirectory\$engine.exe" }) 100 } "active $engine blocks uninstall"
}
Assert-iDockInstallerProcesses $testDirectory @([pscustomobject]@{ Id = 100; ProcessName = 'unins000'; Path = "$testDirectory\unins000.exe" }) 100
Assert-Installer $true 'only caller PID is exempt'
Assert-InstallerThrows { Assert-iDockInstallerProcesses $testDirectory @([pscustomobject]@{ Id = 101; ProcessName = 'unins000'; Path = "$testDirectory\unins000.exe" }) 100 } 'another similarly named process is not exempt'
$testHash = 'a' * 64
$verifiedOriginal = Assert-iDockUninstallerIdentity $testDirectory "$testDirectory\unins000.exe" $testHash $testHash
Assert-Installer ($verifiedOriginal -eq "$testDirectory\unins000.exe") 'same-byte original and temporary uninstall image identity'
Assert-iDockInstallerProcesses $testDirectory @(
    [pscustomobject]@{ Id = 101; ProcessName = 'unins000'; Path = "$testDirectory\unins000.exe" },
    [pscustomobject]@{ Id = 102; ProcessName = 'unins000'; Path = "$testDirectory\unins000.exe" },
    [pscustomobject]@{ Id = 100; ProcessName = '_unins'; Path = 'C:\Temp\setup-uninstall.tmp\_unins.tmp' }
) 100 $verifiedOriginal
Assert-Installer $true 'verified original UAC and first-phase stubs may wait for temporary second phase'
Assert-InstallerThrows { Assert-iDockInstallerProcesses $testDirectory @([pscustomobject]@{ Id = 103; ProcessName = 'unins001'; Path = "$testDirectory\unins001.exe" }) 100 $verifiedOriginal } 'different uninstaller image is not exempt'
Assert-InstallerThrows { Assert-iDockUninstallerIdentity $testDirectory "$testDirectory\unins000.exe" $testHash ('b' * 64) } 'uninstaller image hash mismatch is rejected'
Assert-InstallerThrows { Assert-iDockUninstallerIdentity $testDirectory "$testDirectory\unins000.exe" '' '' } 'empty hashes never establish identity'
Assert-InstallerThrows { Assert-iDockUninstallerIdentity $testDirectory "$testDirectory\iDock.exe" $testHash $testHash } 'application binary cannot impersonate original uninstaller'
Assert-InstallerThrows { Assert-iDockUninstallerIdentity $testDirectory 'C:\Other\unins000.exe' $testHash $testHash } 'outside-directory uninstaller not exempt'
Assert-InstallerThrows { Assert-iDockUninstallerIdentity $testDirectory "$testDirectory\nested\unins000.exe" $testHash $testHash } 'nested same-name uninstaller not exempt'
Assert-iDockInstallerProcesses $testDirectory @([pscustomobject]@{ Id = 42; ProcessName = 'mDNSResponder'; Path = "$testDirectory\vendor\uxplay\mDNSResponder.exe" }) 100
Assert-Installer $true 'shared Bonjour stays running'
Assert-InstallerThrows { Assert-iDockInstallerProcesses $testDirectory @([pscustomobject]@{ Id = 42; ProcessName = 'mDNSResponder'; Path = "$testDirectory\other\mDNSResponder.exe" }) 100 } 'unrelated same-name Bonjour is not exempt'
Assert-iDockInstallerProcesses $testDirectory @([pscustomobject]@{ Id = 42; ProcessName = 'notepad'; Path = 'C:\Windows\notepad.exe' }) 100
Assert-Installer $true 'unrelated process remains untouched'

. (Join-Path $PSScriptRoot '..\scripts\package-portable.ps1')
foreach ($path in @('iDock.exe', 'System.Transactions.Local.dll', 'docs/INSTALL.md', 'installer/installed.mode', 'source/iDock/Assets/iDock.ico', 'source/blehid-patched/.env.example')) {
    Assert-iDockPortableRelativePath $path
    Assert-Installer $true "portable public path allowed: $path"
}
foreach ($path in @('installed.mode', 'data/hosts.json', 'logs/idock.log', '.git/config', 'source/obj/project.assets.json', 'source/bin/iDock.dll', 'validation/report.json', 'Repair-iDockRename.local.ps1', '.env', '.env.production', 'private.pfx', 'C:\secret.txt', '../secret.txt', 'vendor/../secret.txt', 'capture.dmp')) {
    Assert-InstallerThrows { Assert-iDockPortableRelativePath $path } "portable private path rejected: $path"
}
Assert-InstallerThrows { Assert-iDockPortableRelativePath 'docs/example.md' $true } 'portable reparse path rejected'

$iss = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'iDock.iss') -Raw
$helper = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Configure-Firewall.ps1') -Raw
$build = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\scripts\build-installer.ps1') -Raw
Assert-Installer ($iss -match 'PrivilegesRequired=admin') 'UAC declaration'
Assert-Installer ($iss -match 'ArchitecturesInstallIn64BitMode=x64os') 'x64 installation without an ARM64 support promise'
Assert-Installer ($iss -match 'MinVersion=10\.0\.19041') 'minimum Windows version'
Assert-Installer ($iss -match 'runasoriginaluser') 'app starts unelevated after setup'
Assert-Installer ($iss -match 'SetupMutex=Global\\iDockInstaller-') 'serialized product setup'
Assert-Installer ($iss -match 'function InitializeUninstall\(\): Boolean') 'uninstall preflight before changes'
Assert-Installer ($iss -match 'PreflightUninstall') 'uninstall preflight independent of rule changes'
Assert-Installer ($iss -match 'InstallerProcessId.*GetCurrentProcessId') 'exact uninstaller PID passed'
Assert-Installer ($iss -match 'ExecAndLogOutput') 'helper failure details recorded in setup log'
Assert-Installer ($iss -match 'Name: publicwifi;.*ALL Public Wi-Fi networks.*Flags: unchecked') 'public wireless consent is explicit and unchecked by default'
Assert-Installer ($iss -match 'UsePreviousTasks=yes') 'upgrades display prior task choice for review instead of silently resetting consent'
Assert-Installer ($iss -match "WizardIsTaskSelected\('publicwifi'\)" -and $iss -match "Args := Args \+ ' -AllowPublicWireless'") 'selected task explicitly passes opt-in switch'
Assert-Installer ($iss -match "if \(Mode = 'Preflight'\) or \(Mode = 'Install'\) then") 'uninstaller never depends on setup task selection'
Assert-Installer ($helper -match "PublicWirelessAllowed -or \$" + "Operation -eq 'Uninstall'") 'uninstaller always enumerates both families'
Assert-Installer ($helper -notmatch 'LocalRepair|(?<![\d.])(?:\d{1,3}\.){3}\d{1,3}(?![\d.])') 'public installer has no local repair identity or hardcoded IPv4 network'
Assert-Installer ($iss -match 'mDNSResponder\.exe"; DestDir:.*onlyifdoesntexist uninsneveruninstall') 'shared Bonjour retained, never replaced'
Assert-Installer ($iss -match 'installed\.mode"; DestDir:.*uninsneveruninstall') 'retained folder can be reinstalled'
Assert-Installer ($iss -notmatch '\[UninstallDelete\]|\[InstallDelete\]') 'no broad file deletions'
Assert-Installer ($helper -notmatch '(?im)^\s*(Stop-Service|Start-Service|Set-Service|New-Service|Remove-Service|Set-NetFirewallProfile|Set-NetConnectionProfile|Disable-NetAdapter|Remove-Item)\b') 'no global service/radio/profile/deletion operations'
Assert-Installer ($build -match 'SignatureCertificate|SignerCertificate') 'compiler signature validation'
Assert-Installer ($build -match "sha256") 'compiler checksum validation'
Assert-Installer ($build -match '/PORTABLE=1' -and $build -match '/CURRENTUSER') 'compiler only portable current-user'
Assert-Installer ($build -match 'FreshMachineInstallTested = \$false') 'does not invent real installer validation'
Assert-Installer ($build -match 'SelfContained = \$true') 'self-contained build requested'
Assert-Installer ($build -match 'vendor\\blehid\\coreclr.dll') 'backend runtime required'
Write-Host "Installer safety checks passed: $script:count (hardware-free; no installer or system changes)."
