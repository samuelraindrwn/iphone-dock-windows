# Stage 2 probe: build a complete HID-over-GATT (HOGP) service on the in-box Windows BLE stack.
# Verifies each characteristic + the Report Reference (0x2908) descriptor, which is the usual blocker.

Add-Type -AssemblyName System.Runtime.WindowsRuntime

$asTaskGeneric = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
    $_.Name -eq 'AsTask' -and
    $_.GetParameters().Count -eq 1 -and
    $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'
})[0]

function Await($op, [Type]$resultType) {
    $netTask = $asTaskGeneric.MakeGenericMethod($resultType).Invoke($null, @($op))
    if (-not $netTask.Wait(15000)) { throw 'WinRT operation timed out.' }
    $netTask.Result
}

$null = [Windows.Security.Cryptography.CryptographicBuffer, Windows.Security, ContentType = WindowsRuntime]
function Buf([byte[]]$bytes) {
    [Windows.Security.Cryptography.CryptographicBuffer]::CreateFromByteArray($bytes)
}

$GattNs = 'Windows.Devices.Bluetooth.GenericAttributeProfile'
$Props  = "$GattNs.GattCharacteristicProperties" -as [type]
$Prot   = "$GattNs.GattProtectionLevel" -as [type]
$CharRes = "$GattNs.GattLocalCharacteristicResult" -as [type]
$DescRes = "$GattNs.GattLocalDescriptorResult" -as [type]
$SvcRes  = "$GattNs.GattServiceProviderResult" -as [type]
$providerType = [Windows.Devices.Bluetooth.GenericAttributeProfile.GattServiceProvider, Windows.Devices, ContentType = WindowsRuntime]

function Sig([int]$short) { [guid]::new(('{0:x8}-0000-1000-8000-00805f9b34fb' -f $short)) }

function Report($label, $status, $ok) {
    $color = if ($ok) { 'Green' } else { 'Red' }
    Write-Host ("  {0,-34} -> {1}" -f $label, $status) -ForegroundColor $color
}

# Composite report descriptor: keyboard = Report ID 1, mouse = Report ID 2
$reportMap = [byte[]]@(
    0x05,0x01, 0x09,0x06, 0xA1,0x01, 0x85,0x01,
      0x05,0x07, 0x19,0xE0, 0x29,0xE7, 0x15,0x00, 0x25,0x01,
      0x75,0x01, 0x95,0x08, 0x81,0x02,
      0x95,0x01, 0x75,0x08, 0x81,0x01,
      0x95,0x06, 0x75,0x08, 0x15,0x00, 0x25,0x65,
      0x05,0x07, 0x19,0x00, 0x29,0x65, 0x81,0x00,
    0xC0,
    0x05,0x01, 0x09,0x02, 0xA1,0x01, 0x85,0x02,
      0x09,0x01, 0xA1,0x00,
        0x05,0x09, 0x19,0x01, 0x29,0x03, 0x15,0x00, 0x25,0x01,
        0x95,0x03, 0x75,0x01, 0x81,0x02,
        0x95,0x01, 0x75,0x05, 0x81,0x01,
        0x05,0x01, 0x09,0x30, 0x09,0x31, 0x09,0x38,
        0x15,0x81, 0x25,0x7F, 0x75,0x08, 0x95,0x03, 0x81,0x06,
      0xC0,
    0xC0
)

Write-Host "`n=== Creating HID service (0x1812) ===" -ForegroundColor Cyan
$svcResult = Await ($providerType::CreateAsync((Sig 0x1812))) $SvcRes
Report 'GattServiceProvider' $svcResult.Error ($svcResult.Error -eq 'Success')
if ($svcResult.Error -ne 'Success') { return }
$service = $svcResult.ServiceProvider.Service

function New-Char($label, $uuid, $props, $static, $readProt) {
    $p = New-Object "$GattNs.GattLocalCharacteristicParameters"
    $p.CharacteristicProperties = $props
    if ($null -ne $static) { $p.StaticValue = Buf $static }
    if ($readProt) { $p.ReadProtectionLevel = $readProt }
    try {
        $r = Await ($service.CreateCharacteristicAsync($uuid, $p)) $CharRes
        Report $label $r.Error ($r.Error -eq 'Success')
        if ($r.Error -eq 'Success') { return $r.Characteristic }
    }
    catch { Report $label ("EXCEPTION: " + $_.Exception.Message) $false }
    return $null
}

$enc  = $Prot::EncryptionRequired
$read = $Props::Read
$wwr  = $Props::WriteWithoutResponse
$rw   = $Props::Read -bor $Props::Write
$rn   = $Props::Read -bor $Props::Notify

Write-Host "`n=== Characteristics ===" -ForegroundColor Cyan
# bcdHID 1.11, country 0, flags = RemoteWake | NormallyConnectable
$null = New-Char 'HID Information  (0x2A4A)' (Sig 0x2A4A) $read ([byte[]]@(0x11,0x01,0x00,0x03)) $enc
$null = New-Char 'Report Map       (0x2A4B)' (Sig 0x2A4B) $read $reportMap $enc
$null = New-Char 'HID Control Point(0x2A4C)' (Sig 0x2A4C) $wwr  $null      $null
$null = New-Char 'Protocol Mode    (0x2A4E)' (Sig 0x2A4E) ($read -bor $wwr) ([byte[]]@(0x01)) $enc
$kbdReport   = New-Char 'Keyboard Input Report (0x2A4D)' (Sig 0x2A4D) $rn ([byte[]]@(0,0,0,0,0,0,0,0)) $enc
$mouseReport = New-Char 'Mouse Input Report    (0x2A4D)' (Sig 0x2A4D) $rn ([byte[]]@(0,0,0,0)) $enc
$ledReport   = New-Char 'Keyboard LED Output   (0x2A4D)' (Sig 0x2A4D) ($rw -bor $wwr) ([byte[]]@(0)) $enc

Write-Host "`n=== Report Reference descriptors (0x2908) - the critical test ===" -ForegroundColor Cyan
function New-ReportRef($label, $char, [byte]$id, [byte]$type) {
    if ($null -eq $char) { Report $label 'SKIPPED (no characteristic)' $false; return }
    $dp = New-Object "$GattNs.GattLocalDescriptorParameters"
    $dp.StaticValue = Buf ([byte[]]@($id, $type))
    $dp.ReadProtectionLevel = $enc
    try {
        $r = Await ($char.CreateDescriptorAsync((Sig 0x2908), $dp)) $DescRes
        Report $label $r.Error ($r.Error -eq 'Success')
    }
    catch { Report $label ("EXCEPTION: " + $_.Exception.Message) $false }
}

New-ReportRef 'Keyboard input  (id=1,type=Input)'  $kbdReport   1 1
New-ReportRef 'Mouse input     (id=2,type=Input)'  $mouseReport 2 1
New-ReportRef 'Keyboard LED    (id=1,type=Output)' $ledReport   1 2

Write-Host "`n=== Advertising ===" -ForegroundColor Cyan
try {
    $ap = New-Object "$GattNs.GattServiceProviderAdvertisingParameters"
    $ap.IsConnectable = $true
    $ap.IsDiscoverable = $true
    $svcResult.ServiceProvider.StartAdvertising($ap)
    Report 'AdvertisementStatus' $svcResult.ServiceProvider.AdvertisementStatus $true
    Write-Host "`nAdvertising as '$env:COMPUTERNAME'. Open Bluetooth settings on a phone/tablet and look for it." -ForegroundColor Yellow
    Write-Host 'Press Enter to stop...' -ForegroundColor Yellow
    [void](Read-Host)
    $svcResult.ServiceProvider.StopAdvertising()
}
catch { Report 'Advertising' ("EXCEPTION: " + $_.Exception.Message) $false }
