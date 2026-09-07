using System.Collections.Concurrent;
using System.Runtime.Versioning;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Foundation;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;

namespace BleHid.Core;

public sealed record PeripheralDiagnostics(string Step, string Detail, bool Ok);

/// <summary>A host that has subscribed to input reports and can be targeted individually.</summary>
public sealed record HostTarget(string DeviceId, string Address, string? Name)
{
    public string Display => Name is { Length: > 0 } ? $"{Name} [{Address}]" : Address;
}

/// <summary>
/// Exposes this PC as a BLE HID keyboard + mouse (HID over GATT) using the in-box Windows stack.
/// </summary>
public sealed class BleHidPeripheral : IAsyncDisposable
{
    private readonly List<PeripheralDiagnostics> _diagnostics = [];
    private readonly GattProtectionLevel _protection;
    private readonly ConcurrentDictionary<string, string> _hostNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, BluetoothLEDevice> _hostDevices = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, int> _connectionIntervalsMs = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _connectionParameterGate = new(1, 1);
    private readonly PointerPacingOverrides _pointerPacing =
        PointerPacingOverrides.Load(AppPaths.InRoot("pointer-pacing.json"));
    private int _pacingWarningLogged;
    private string? _selectedHostId;
    private bool _localOnly;
    private bool _warnedMissingHost;
    private bool _everAdvertised;
    private bool _startAttempted;
    private bool _advertisingAttempted;
    private GattServiceProviderAdvertisementStatus _lastAdvertisementStatus;
    private TaskCompletionSource<bool>? _advertisingStarted;
    private GattServiceProvider? _provider;
    private GattServiceProvider? _batteryProvider;
    private GattLocalCharacteristic? _keyboardInput;
    private GattLocalCharacteristic? _mouseInput;
    private byte[] _lastKeyboardReport = new byte[HidReports.KeyboardReportLength];
    private byte[] _lastMouseReport = new byte[HidReports.MouseReportLength];
    private byte _protocolMode = 0x01; // 0x00 = boot, 0x01 = report

    [SupportedOSPlatformGuard("windows10.0.22000.0")]
    private static bool CanReadConnectionParameters =>
        OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000);

    public IReadOnlyList<PeripheralDiagnostics> Diagnostics => _diagnostics;
    public bool HasStartedSuccessfully { get; private set; }
    public GattServiceProviderAdvertisementStatus AdvertisementStatus =>
        _provider?.AdvertisementStatus ?? _lastAdvertisementStatus;

    public int SubscribedKeyboardClients => _keyboardInput?.SubscribedClients.Count ?? 0;
    public int SubscribedMouseClients => _mouseInput?.SubscribedClients.Count ?? 0;

    public int MouseReportIntervalMs(int configuredIntervalMs)
    {
        if (_pointerPacing.Warning is { } warning && Interlocked.Exchange(ref _pacingWarningLogged, 1) == 0)
            Log?.Invoke($"[link] {warning}");

        var clients = _mouseInput?.SubscribedClients ?? [];
        return clients
            .Where(client => _selectedHostId is null || string.Equals(
                client.Session.DeviceId.Id, _selectedHostId, StringComparison.OrdinalIgnoreCase))
            .Select(client => Math.Max(
                _connectionIntervalsMs.GetValueOrDefault(client.Session.DeviceId.Id),
                _pointerPacing.MinimumIntervalMs(
                    _hostNames.GetValueOrDefault(client.Session.DeviceId.Id), configuredIntervalMs)))
            .DefaultIfEmpty(_pointerPacing.MinimumIntervalMs(null, configuredIntervalMs))
            .Max();
    }

    /// <summary>Null means reports are broadcast to every subscribed host.</summary>
    public string? SelectedHostId => _selectedHostId;

    /// <summary>True when input should stay on this PC instead of going to any host.</summary>
    public bool IsLocalTarget => _localOnly;

    public string SelectedHostDisplay => _localOnly
        ? LocalDisplay
        : _selectedHostId is null
            ? "all hosts"
            : Hosts().FirstOrDefault(h => h.DeviceId == _selectedHostId)?.Display ?? "(disconnected host)";

    private const string LocalDisplay = "this PC (input stays local)";

    public event Action<string>? Log;

    /// <summary>Raised on every target change so a running capture can re-evaluate pass-through.</summary>
    public event Action? TargetChanged;

    /// <summary>HOGP mandates encryption, but hosts differ in how they bond with a Windows peripheral.</summary>
    public BleHidPeripheral(bool requireEncryption = true) =>
        _protection = requireEncryption
            ? GattProtectionLevel.EncryptionRequired
            : GattProtectionLevel.Plain;

    private void Record(string step, string detail, bool ok)
    {
        _diagnostics.Add(new PeripheralDiagnostics(step, detail, ok));
        Log?.Invoke($"[{(ok ? " ok " : "FAIL")}] {step}: {detail}");
    }

    public async Task StartAsync()
    {
        if (_startAttempted)
            throw new InvalidOperationException("Use a new peripheral instance for each startup attempt.");
        _startAttempted = true;
        try
        {
            await StartCoreAsync();
        }
        catch
        {
            // Failed startup must release advertising resources before a retry or diagnostics.
            await DisposeAsync();
            throw;
        }
    }

    private async Task StartCoreAsync()
    {
        var adapter = await BluetoothAdapter.GetDefaultAsync()
            ?? throw new InvalidOperationException("No Bluetooth adapter found.");

        Record("Adapter", $"LE={adapter.IsLowEnergySupported}, Peripheral={adapter.IsPeripheralRoleSupported}",
            adapter.IsLowEnergySupported && adapter.IsPeripheralRoleSupported);

        Record("Protection level", _protection.ToString(), true);

        if (!adapter.IsPeripheralRoleSupported)
            throw new NotSupportedException("This Bluetooth radio does not support the LE peripheral role.");

        var serviceResult = await GattServiceProvider.CreateAsync(HidDescriptors.HidService);
        Record("HID service 0x1812", serviceResult.Error.ToString(), serviceResult.Error == BluetoothError.Success);
        if (serviceResult.Error != BluetoothError.Success)
            throw new InvalidOperationException($"Could not create HID service: {serviceResult.Error}");

        _provider = serviceResult.ServiceProvider;
        var service = _provider.Service;

        await CreateReadableCharacteristicAsync("HID Information", HidDescriptors.HidInformation,
            HidDescriptors.HidInformationValue, service);

        await CreateReadableCharacteristicAsync("Report Map", HidDescriptors.ReportMap,
            HidDescriptors.ReportMapValue, service);

        await CreateControlPointAsync(service);
        await CreateProtocolModeAsync(service);

        _keyboardInput = await CreateInputReportAsync("Keyboard input report", service,
            HidDescriptors.KeyboardReportId, () => _lastKeyboardReport);

        _mouseInput = await CreateInputReportAsync("Mouse input report", service,
            HidDescriptors.MouseReportId, () => _lastMouseReport);

        await CreateBatteryServiceAsync();

        _advertisingStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _provider.AdvertisementStatusChanged += OnAdvertisementStatusChanged;

        _advertisingAttempted = true;
        _provider.StartAdvertising(new GattServiceProviderAdvertisingParameters
        {
            IsConnectable = true,
            IsDiscoverable = true
        });

        // The provider reports Aborted until the radio actually begins advertising,
        // so wait for the Started transition rather than reading the status here.
        var started = await Task.WhenAny(_advertisingStarted.Task, Task.Delay(TimeSpan.FromSeconds(10)))
            == _advertisingStarted.Task;

        _lastAdvertisementStatus = _provider.AdvertisementStatus;
        started = started && _lastAdvertisementStatus == GattServiceProviderAdvertisementStatus.Started;
        Record("StartAdvertising", _lastAdvertisementStatus.ToString(), started);
        if (!started)
            throw new InvalidOperationException(
                $"BLE HID advertising did not reach Started within 10 seconds (status: {_lastAdvertisementStatus}). " +
                "Keyboard and mouse capture will not start. Run --diagnose for details.");

        HasStartedSuccessfully = true;
    }

    private void OnAdvertisementStatusChanged(GattServiceProvider sender,
        GattServiceProviderAdvertisementStatusChangedEventArgs args)
    {
        _lastAdvertisementStatus = args.Status;
        // Give an initial Aborted transition a bounded chance to recover, but never treat it as ready.
        var note = args.Status == GattServiceProviderAdvertisementStatus.Aborted
            ? _everAdvertised ? " -- advertising stopped; hosts can no longer see this PC"
                              : " (not ready; waiting for Started until startup timeout)"
            : "";

        Log?.Invoke($"[adv ] status -> {args.Status} (error: {args.Error}){note}");

        if (args.Status == GattServiceProviderAdvertisementStatus.Started)
        {
            _everAdvertised = true;
            _advertisingStarted?.TrySetResult(true);
        }
    }

    /// <summary>Serves the value from a handler rather than StaticValue so host reads are observable.</summary>
    private async Task CreateReadableCharacteristicAsync(string name, Guid uuid,
        byte[] value, GattLocalService service)
    {
        var parameters = new GattLocalCharacteristicParameters
        {
            CharacteristicProperties = GattCharacteristicProperties.Read,
            ReadProtectionLevel = _protection
        };

        var result = await service.CreateCharacteristicAsync(uuid, parameters);
        Record(name, $"{result.Error} ({value.Length} bytes)", result.Error == BluetoothError.Success);
        if (result.Error != BluetoothError.Success) return;

        result.Characteristic.ReadRequested += async (_, args) =>
        {
            using var deferral = args.GetDeferral();
            var request = await args.GetRequestAsync();
            Log?.Invoke($"[read] host read {name}");
            request.RespondWithValue(CryptographicBuffer.CreateFromByteArray(value));
        };
    }

    private async Task CreateBatteryServiceAsync()
    {
        var result = await GattServiceProvider.CreateAsync(HidDescriptors.BatteryService);
        Record("Battery service 0x180F", result.Error.ToString(), result.Error == BluetoothError.Success);
        if (result.Error != BluetoothError.Success) return;

        _batteryProvider = result.ServiceProvider;

        var parameters = new GattLocalCharacteristicParameters
        {
            CharacteristicProperties = GattCharacteristicProperties.Read | GattCharacteristicProperties.Notify,
            ReadProtectionLevel = GattProtectionLevel.Plain
        };

        var levelResult = await _batteryProvider.Service.CreateCharacteristicAsync(
            HidDescriptors.BatteryLevel, parameters);

        Record("Battery Level 0x2A19", levelResult.Error.ToString(),
            levelResult.Error == BluetoothError.Success);
        if (levelResult.Error != BluetoothError.Success) return;

        levelResult.Characteristic.ReadRequested += async (_, args) =>
        {
            using var deferral = args.GetDeferral();
            var request = await args.GetRequestAsync();
            Log?.Invoke("[read] host read Battery Level");
            request.RespondWithValue(CryptographicBuffer.CreateFromByteArray([100]));
        };
    }

    private async Task CreateControlPointAsync(GattLocalService service)
    {
        var parameters = new GattLocalCharacteristicParameters
        {
            CharacteristicProperties = GattCharacteristicProperties.WriteWithoutResponse,
            WriteProtectionLevel = _protection
        };

        var result = await service.CreateCharacteristicAsync(HidDescriptors.HidControlPoint, parameters);
        Record("HID Control Point", result.Error.ToString(), result.Error == BluetoothError.Success);
        if (result.Error != BluetoothError.Success) return;

        result.Characteristic.WriteRequested += async (_, args) =>
        {
            using var deferral = args.GetDeferral();
            var request = await args.GetRequestAsync();
            var command = ReadBytes(request.Value);
            Log?.Invoke($"[hid ] control point <- 0x{(command.Length > 0 ? command[0] : 0):X2}");
        };
    }

    private async Task CreateProtocolModeAsync(GattLocalService service)
    {
        var parameters = new GattLocalCharacteristicParameters
        {
            CharacteristicProperties = GattCharacteristicProperties.Read |
                                       GattCharacteristicProperties.WriteWithoutResponse,
            ReadProtectionLevel = _protection,
            WriteProtectionLevel = _protection
        };

        var result = await service.CreateCharacteristicAsync(HidDescriptors.ProtocolMode, parameters);
        Record("Protocol Mode", result.Error.ToString(), result.Error == BluetoothError.Success);
        if (result.Error != BluetoothError.Success) return;

        result.Characteristic.ReadRequested += async (_, args) =>
        {
            using var deferral = args.GetDeferral();
            var request = await args.GetRequestAsync();
            request.RespondWithValue(CryptographicBuffer.CreateFromByteArray([_protocolMode]));
        };

        result.Characteristic.WriteRequested += async (_, args) =>
        {
            using var deferral = args.GetDeferral();
            var request = await args.GetRequestAsync();
            var value = ReadBytes(request.Value);
            if (value.Length > 0) _protocolMode = value[0];
            Log?.Invoke($"[hid ] protocol mode -> {(_protocolMode == 0 ? "boot" : "report")}");
        };
    }

    private async Task<GattLocalCharacteristic?> CreateInputReportAsync(string name,
        GattLocalService service, byte reportId, Func<byte[]> currentValue)
    {
        var parameters = new GattLocalCharacteristicParameters
        {
            CharacteristicProperties = GattCharacteristicProperties.Read |
                                       GattCharacteristicProperties.Notify,
            ReadProtectionLevel = _protection
        };

        var result = await service.CreateCharacteristicAsync(HidDescriptors.Report, parameters);
        Record(name, result.Error.ToString(), result.Error == BluetoothError.Success);
        if (result.Error != BluetoothError.Success) return null;

        var characteristic = result.Characteristic;

        characteristic.ReadRequested += async (_, args) =>
        {
            using var deferral = args.GetDeferral();
            var request = await args.GetRequestAsync();
            Log?.Invoke($"[read] host read {name}");
            request.RespondWithValue(CryptographicBuffer.CreateFromByteArray(currentValue()));
        };

        characteristic.SubscribedClientsChanged += (sender, _) =>
        {
            Log?.Invoke($"[subs] {name}: {sender.SubscribedClients.Count} subscriber(s)");
            ReturnLocalIfTargetMissing(Hosts());
            _ = RefreshConnectionParametersAsync();
        };

        var descriptorParameters = new GattLocalDescriptorParameters
        {
            ReadProtectionLevel = _protection,
            StaticValue = CryptographicBuffer.CreateFromByteArray([reportId, HidDescriptors.ReportTypeInput])
        };

        var descriptorResult = await characteristic.CreateDescriptorAsync(
            HidDescriptors.ReportReference, descriptorParameters);

        Record($"{name} / Report Reference 0x2908",
            $"{descriptorResult.Error} (id={reportId}, type=Input)",
            descriptorResult.Error == BluetoothError.Success);

        return characteristic;
    }

    private async Task RefreshConnectionParametersAsync()
    {
        if (!CanReadConnectionParameters) return;

        await _connectionParameterGate.WaitAsync();
        try
        {
            foreach (var host in Hosts())
            {
                if (!_hostDevices.TryGetValue(host.DeviceId, out var device))
                {
                    device = await BluetoothLEDevice.FromIdAsync(host.DeviceId);
                    if (device is null) continue;
                    if (!string.IsNullOrWhiteSpace(device.Name))
                        _hostNames[host.DeviceId] = device.Name;
                    device.ConnectionParametersChanged += OnConnectionParametersChanged;
                    _hostDevices[host.DeviceId] = device;
                }

                UpdateConnectionInterval(device);
            }
        }
        catch (Exception ex)
        {
            Log?.Invoke($"[link] could not read connection interval: {ex.Message}");
        }
        finally
        {
            _connectionParameterGate.Release();
        }
    }

    [SupportedOSPlatform("windows10.0.22000.0")]
    private void OnConnectionParametersChanged(BluetoothLEDevice sender, object args) =>
        UpdateConnectionInterval(sender);

    [SupportedOSPlatform("windows10.0.22000.0")]
    private void UpdateConnectionInterval(BluetoothLEDevice device)
    {
        var parameters = device.GetConnectionParameters();
        if (parameters.ConnectionInterval == 0) return;

        var intervalMs = (int)Math.Ceiling(parameters.ConnectionInterval * 1.25);
        if (_connectionIntervalsMs.TryGetValue(device.DeviceId, out var previous) && previous == intervalMs)
            return;

        _connectionIntervalsMs[device.DeviceId] = intervalMs;
        Log?.Invoke($"[link] connection interval -> {intervalMs} ms");
    }

    public Task SendKeyboardAsync(KeyModifiers modifiers, params byte[] usages)
    {
        _lastKeyboardReport = HidReports.Keyboard(modifiers, usages);
        return NotifyAsync(_keyboardInput, _lastKeyboardReport);
    }

    public Task ReleaseKeysAsync()
    {
        _lastKeyboardReport = HidReports.KeyboardRelease();
        return NotifyAsync(_keyboardInput, _lastKeyboardReport);
    }

    public Task SendMouseAsync(MouseButtons buttons, int dx, int dy, int wheel)
    {
        _lastMouseReport = HidReports.Mouse(buttons, dx, dy, wheel);
        return NotifyAsync(_mouseInput, _lastMouseReport);
    }

    /// <summary>Subscribed hosts, de-duplicated across the keyboard and mouse reports.</summary>
    public IReadOnlyList<HostTarget> Hosts()
    {
        var ids = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var client in _keyboardInput?.SubscribedClients ?? []) ids.Add(client.Session.DeviceId.Id);
        foreach (var client in _mouseInput?.SubscribedClients ?? []) ids.Add(client.Session.DeviceId.Id);

        return ids.Select(id => new HostTarget(id, ShortAddress(id), _hostNames.GetValueOrDefault(id))).ToList();
    }

    /// <summary>Cycles this PC -> host1 -> ... -> hostN -> this PC. Safe to call from any thread.</summary>
    public string SelectNextHost()
    {
        var hosts = Hosts();
        _warnedMissingHost = false;

        if (hosts.Count == 0) return SelectLocal();

        // Broadcast is not in the rotation; it is a fallback reached through SelectAllHosts.
        if (_localOnly)
        {
            _localOnly = false;
            _selectedHostId = hosts[0].DeviceId;
            TargetChanged?.Invoke();
            return hosts[0].Display;
        }

        if (_selectedHostId is null) return SelectLocal();

        var index = -1;
        for (var i = 0; i < hosts.Count; i++)
            if (string.Equals(hosts[i].DeviceId, _selectedHostId, StringComparison.OrdinalIgnoreCase)) { index = i; break; }

        var next = index + 1;
        if (next >= hosts.Count) return SelectLocal();

        _selectedHostId = hosts[next].DeviceId;
        TargetChanged?.Invoke();
        return hosts[next].Display;
    }

    public string SelectLocal()
    {
        _localOnly = true;
        _selectedHostId = null;
        _warnedMissingHost = false;
        TargetChanged?.Invoke();
        return LocalDisplay;
    }

    internal static bool ShouldReturnLocal(bool localOnly, string? selectedHostId,
        IReadOnlyList<HostTarget> hosts) => !localOnly && (selectedHostId is null
            ? hosts.Count == 0
            : !hosts.Any(host => string.Equals(host.DeviceId, selectedHostId, StringComparison.OrdinalIgnoreCase)));

    internal bool ReturnLocalIfTargetMissing(IReadOnlyList<HostTarget> hosts)
    {
        if (!ShouldReturnLocal(_localOnly, _selectedHostId, hosts)) return false;
        SelectLocal(); // TargetChanged switches an active capture back to pass-through immediately.
        Log?.Invoke("[host] target disconnected - input returned to this PC");
        return true;
    }

    public void SelectAllHosts()
    {
        _localOnly = false;
        _selectedHostId = null;
        _warnedMissingHost = false;
        TargetChanged?.Invoke();
    }

    public bool SelectHost(int index)
    {
        var hosts = Hosts();
        if (index < 0 || index >= hosts.Count) return false;
        _localOnly = false;
        _selectedHostId = hosts[index].DeviceId;
        _warnedMissingHost = false;
        TargetChanged?.Invoke();
        return true;
    }

    /// <summary>Resolves friendly names for subscribed hosts so they can be shown while switching.</summary>
    public async Task RefreshHostNamesAsync()
    {
        foreach (var host in Hosts())
        {
            if (_hostNames.ContainsKey(host.DeviceId)) continue;
            try
            {
                using var device = await BluetoothLEDevice.FromIdAsync(host.DeviceId);
                if (!string.IsNullOrWhiteSpace(device?.Name)) _hostNames[host.DeviceId] = device.Name;
            }
            catch (Exception ex)
            {
                Log?.Invoke($"[host] could not resolve name for {host.Address}: {ex.Message}");
            }
        }
    }

    private static string ShortAddress(string deviceId)
    {
        var separator = deviceId.LastIndexOf('-');
        return separator >= 0 && separator < deviceId.Length - 1 ? deviceId[(separator + 1)..] : deviceId;
    }

    private async Task NotifyAsync(GattLocalCharacteristic? characteristic, byte[] payload)
    {
        if (_localOnly) return;
        if (characteristic is null || characteristic.SubscribedClients.Count == 0) return;

        var buffer = CryptographicBuffer.CreateFromByteArray(payload);
        var target = _selectedHostId;
        if (target is null)
        {
            await characteristic.NotifyValueAsync(buffer);
            return;
        }

        foreach (var client in characteristic.SubscribedClients)
        {
            if (!string.Equals(client.Session.DeviceId.Id, target, StringComparison.OrdinalIgnoreCase)) continue;
            await characteristic.NotifyValueAsync(buffer, client);
            return;
        }

        // Dropping is safer than falling back to broadcast: the input was meant for one host.
        if (!_warnedMissingHost)
        {
            _warnedMissingHost = true;
            Log?.Invoke("[host] selected host is no longer subscribed - reports are being dropped");
        }
    }

    private static byte[] ReadBytes(IBuffer buffer)
    {
        var bytes = new byte[buffer.Length];
        using var reader = DataReader.FromBuffer(buffer);
        reader.ReadBytes(bytes);
        return bytes;
    }

    public ValueTask DisposeAsync()
    {
        HasStartedSuccessfully = false;
        if (_provider is not null)
        {
            _lastAdvertisementStatus = _provider.AdvertisementStatus;
            _provider.AdvertisementStatusChanged -= OnAdvertisementStatusChanged;
            // Stop attempted advertising even when Aborted/Created. Merely creating a provider
            // does not make StopAdvertising valid (the battery provider is never advertised).
            try { if (_advertisingAttempted) _provider.StopAdvertising(); }
            catch (Exception ex) { Log?.Invoke($"[stop] HID advertising cleanup failed: {ex.Message}"); }
            _advertisingAttempted = false;
            _provider = null;
        }
        _batteryProvider = null;
        if (CanReadConnectionParameters)
        {
            foreach (var device in _hostDevices.Values)
            {
                device.ConnectionParametersChanged -= OnConnectionParametersChanged;
                device.Dispose();
            }
        }
        _hostDevices.Clear();
        _connectionIntervalsMs.Clear();
        return ValueTask.CompletedTask;
    }
}
