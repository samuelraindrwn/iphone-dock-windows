# Feasibility probe: can the in-box Windows BLE stack publish a HID (0x1812) GATT service?
# Requires Windows PowerShell 5.1 (WinRT projection).

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

function Section($t) { Write-Host "`n=== $t ===" -ForegroundColor Cyan }

Section 'Radio / adapter capabilities'

$adapterType = [Windows.Devices.Bluetooth.BluetoothAdapter, Windows.Devices, ContentType = WindowsRuntime]
$adapter = Await ($adapterType::GetDefaultAsync()) ([Windows.Devices.Bluetooth.BluetoothAdapter])

if ($null -eq $adapter) {
    Write-Host 'No Bluetooth adapter found. Cannot continue.' -ForegroundColor Red
    return
}

[PSCustomObject]@{
    BluetoothAddress        = ('{0:X12}' -f $adapter.BluetoothAddress)
    IsLowEnergySupported    = $adapter.IsLowEnergySupported
    IsCentralRoleSupported  = $adapter.IsCentralRoleSupported
    IsPeripheralRoleSupported = $adapter.IsPeripheralRoleSupported
    IsAdvertisementOffloadSupported = $adapter.IsAdvertisementOffloadSupported
    IsClassicSupported      = $adapter.IsClassicSupported
} | Format-List

if (-not $adapter.IsPeripheralRoleSupported) {
    Write-Host 'Peripheral role NOT supported by this radio/driver -> HID-over-GATT is impossible here.' -ForegroundColor Red
}

Section 'GattServiceProvider.CreateAsync tests'

$providerType = [Windows.Devices.Bluetooth.GenericAttributeProfile.GattServiceProvider, Windows.Devices, ContentType = WindowsRuntime]
$resultType   = [Windows.Devices.Bluetooth.GenericAttributeProfile.GattServiceProviderResult]

$candidates = [ordered]@{
    'Custom (control test)'      = [guid]'2F3A9B10-4C6D-4E1F-9A77-1B0C5D8E7F21'
    'HID 0x1812'                 = [guid]'00001812-0000-1000-8000-00805F9B34FB'
    'Battery 0x180F'             = [guid]'0000180F-0000-1000-8000-00805F9B34FB'
    'Device Information 0x180A'  = [guid]'0000180A-0000-1000-8000-00805F9B34FB'
    'Heart Rate 0x180D'          = [guid]'0000180D-0000-1000-8000-00805F9B34FB'
}

foreach ($name in $candidates.Keys) {
    $uuid = $candidates[$name]
    try {
        $res = Await ($providerType::CreateAsync($uuid)) $resultType
        $status = $res.Error
        $ok = ($status -eq 'Success' -and $null -ne $res.ServiceProvider)
        $color = if ($ok) { 'Green' } else { 'Yellow' }
        Write-Host ("{0,-28} -> {1}" -f $name, $status) -ForegroundColor $color
    }
    catch {
        Write-Host ("{0,-28} -> EXCEPTION: {1}" -f $name, $_.Exception.Message) -ForegroundColor Red
    }
}

Section 'Advertise the HID service (if creatable)'

try {
    $hid = Await ($providerType::CreateAsync([guid]'00001812-0000-1000-8000-00805F9B34FB')) $resultType
    if ($hid.Error -eq 'Success') {
        $params = New-Object Windows.Devices.Bluetooth.GenericAttributeProfile.GattServiceProviderAdvertisingParameters
        $params.IsConnectable = $true
        $params.IsDiscoverable = $true
        $hid.ServiceProvider.StartAdvertising($params)
        Start-Sleep -Milliseconds 500
        Write-Host ("Advertisement status: {0}" -f $hid.ServiceProvider.AdvertisementStatus) -ForegroundColor Green
        $hid.ServiceProvider.StopAdvertising()
    }
    else {
        Write-Host ("Cannot advertise; creation failed with: {0}" -f $hid.Error) -ForegroundColor Yellow
    }
}
catch {
    Write-Host ("Advertising failed: {0}" -f $_.Exception.Message) -ForegroundColor Red
}
