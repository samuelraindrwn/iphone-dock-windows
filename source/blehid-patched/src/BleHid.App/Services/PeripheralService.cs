using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using BleHid.Core;

namespace BleHid.App.Services;

/// <summary>
/// Owns the peripheral for the lifetime of the app so every page observes one shared state.
/// </summary>
public sealed class PeripheralService : INotifyPropertyChanged
{
    private const int MaxLogLines = 500;

    public static PeripheralService Instance { get; } = new();

    private BleHidPeripheral? _peripheral;
    private CancellationTokenSource? _captureCancellation;
    private readonly DispatcherTimer _poll;
    private readonly Dispatcher _dispatcher = Application.Current.Dispatcher;

    private PeripheralService()
    {
        _poll = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _poll.Tick += (_, _) => RefreshCounters();
    }

    public ObservableCollection<string> Log { get; } = [];
    public ObservableCollection<HostTarget> Hosts { get; } = [];
    public ObservableCollection<TargetOption> Targets { get; } = [];

    private bool _applyingSelection;
    private bool _refreshingHosts;
    private string _hostSignature = "";

    private bool _isRunning;
    public bool IsRunning { get => _isRunning; private set => Set(ref _isRunning, value); }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; private set => Set(ref _isBusy, value); }

    private bool _isCapturing;
    public bool IsCapturing { get => _isCapturing; private set => Set(ref _isCapturing, value); }

    private bool _requireEncryption = true;
    public bool RequireEncryption
    {
        get => _requireEncryption;
        set => Set(ref _requireEncryption, value);
    }

    private int _pointerIntervalMs = 10;
    public int PointerIntervalMs { get => _pointerIntervalMs; set => Set(ref _pointerIntervalMs, value); }

    private string _advertisementStatus = "Stopped";
    public string AdvertisementStatus { get => _advertisementStatus; private set => Set(ref _advertisementStatus, value); }

    private int _keyboardSubscribers;
    public int KeyboardSubscribers { get => _keyboardSubscribers; private set => Set(ref _keyboardSubscribers, value); }

    private int _mouseSubscribers;
    public int MouseSubscribers { get => _mouseSubscribers; private set => Set(ref _mouseSubscribers, value); }

    private string _target = "nothing yet";
    public string Target { get => _target; private set => Set(ref _target, value); }

    /// <summary>Capture needs a subscribed host, so it stays disabled until one appears.</summary>
    public bool CanCapture => IsRunning && KeyboardSubscribers > 0;

    public async Task StartAsync()
    {
        if (IsRunning || IsBusy) return;
        IsBusy = true;
        try
        {
            var peripheral = new BleHidPeripheral(RequireEncryption);
            peripheral.Log += Append;
            await peripheral.StartAsync();
            _peripheral = peripheral;
            IsRunning = true;
            // Without this the peripheral starts in broadcast mode and every report is duplicated
            // to all subscribed hosts, which is both surprising and slow.
            peripheral.SelectLocal();
            _poll.Start();
            RefreshCounters();
            await RefreshHostsAsync();
        }
        catch (Exception ex)
        {
            Append($"startup failed: {ex.Message}");
            _peripheral = null;
            IsRunning = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task StopAsync()
    {
        if (!IsRunning || IsBusy) return;
        IsBusy = true;
        try
        {
            await StopCaptureAsync();
            _poll.Stop();
            if (_peripheral is not null) await _peripheral.DisposeAsync();
            _peripheral = null;
            IsRunning = false;
            Hosts.Clear();
            Targets.Clear();
            _hostSignature = "";
            AdvertisementStatus = "Stopped";
            KeyboardSubscribers = MouseSubscribers = 0;
            Target = "nothing yet";
            Append("peripheral stopped");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RefreshHostsAsync()
    {
        if (_peripheral is null || _refreshingHosts) return;
        _refreshingHosts = true;
        try
        {
            await _peripheral.RefreshHostNamesAsync();
            if (_peripheral is null) return;
            Hosts.Clear();
            foreach (var host in _peripheral.Hosts()) Hosts.Add(host);
            _hostSignature = Signature(_peripheral);
            RebuildTargets();
            Target = _peripheral.SelectedHostDisplay;
        }
        finally
        {
            _refreshingHosts = false;
        }
    }

    public void Select(TargetOption option)
    {
        if (_peripheral is null || _applyingSelection) return;

        switch (option.Kind)
        {
            case TargetKind.Local:
                _peripheral.SelectLocal();
                break;
            default:
                // Indexes shift as hosts subscribe and drop, so the device id is the only stable handle.
                var index = IndexOf(option.DeviceId);
                if (index < 0)
                {
                    Append($"[host] {option.Title} is no longer subscribed");
                    _ = RefreshHostsAsync();
                    return;
                }

                _peripheral.SelectHost(index);
                break;
        }

        Target = _peripheral.SelectedHostDisplay;
        Append($"[host] -> {Target}");
        SyncSelection();
    }

    private int IndexOf(string? deviceId)
    {
        if (_peripheral is null || deviceId is null) return -1;
        var hosts = _peripheral.Hosts();
        for (var i = 0; i < hosts.Count; i++)
            if (string.Equals(hosts[i].DeviceId, deviceId, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    private static string Signature(BleHidPeripheral peripheral) =>
        string.Join('|', peripheral.Hosts().Select(h => h.DeviceId));

    private void RebuildTargets()
    {
        if (_peripheral is null) return;

        Targets.Clear();
        var hosts = _peripheral.Hosts();
        for (var i = 0; i < hosts.Count; i++)
        {
            Targets.Add(new TargetOption
            {
                Title = hosts[i].Name is { Length: > 0 } name ? name : hosts[i].Address,
                Detail = hosts[i].Address,
                Kind = TargetKind.Host,
                DeviceId = hosts[i].DeviceId
            });
        }

        Targets.Add(new TargetOption
        {
            Title = "This PC",
            Detail = "Capture stays armed but keystrokes are passed through locally.",
            Kind = TargetKind.Local
        });

        SyncSelection();
    }

    private void SyncSelection()
    {
        if (_peripheral is null) return;

        // Writing IsSelected re-enters through the RadioButton's Checked event, so suppress it.
        _applyingSelection = true;
        try
        {
            foreach (var option in Targets)
            {
                option.IsSelected = option.Kind switch
                {
                    TargetKind.Local => _peripheral.IsLocalTarget,
                    _ => !_peripheral.IsLocalTarget && _peripheral.SelectedHostId == option.DeviceId
                };
            }
        }
        finally
        {
            _applyingSelection = false;
        }
    }

    /// <param name="resident">
    /// Background behaviour, as in the CLI: arms before any host has subscribed and treats
    /// Ctrl+Alt+Q as "return input to this PC" rather than "end the session", because a hidden
    /// window leaves no way to switch it back on.
    /// </param>
    public async Task StartCaptureAsync(bool resident = false)
    {
        if (_peripheral is null || IsCapturing) return;
        if (!resident && !CanCapture) return;

        _captureCancellation = new CancellationTokenSource();
        IsCapturing = true;

        var peripheral = _peripheral;
        var token = _captureCancellation.Token;
        var interval = PointerIntervalMs;

        // Ctrl+Alt+Q ends the session, so the toggle has to follow the hotkey rather than the click.
        _ = Task.Run(async () =>
        {
            try
            {
                await CaptureSession.RunAsync(peripheral, Append, verbose: false, interval,
                    stopEndsSession: !resident, token);
            }
            catch (Exception ex)
            {
                Append($"capture error: {ex.Message}");
            }
            finally
            {
                await _dispatcher.InvokeAsync(() =>
                {
                    IsCapturing = false;
                    Target = peripheral.SelectedHostDisplay;
                    SyncSelection();
                });
            }
        });

        await Task.CompletedTask;
    }

    public async Task StopCaptureAsync()
    {
        if (!IsCapturing) return;
        _captureCancellation?.Cancel();
        // The hook thread unwinds asynchronously; the session's finally clause clears the flag.
        for (var i = 0; i < 50 && IsCapturing; i++) await Task.Delay(20);
    }

    private void RefreshCounters()
    {
        if (_peripheral is null) return;
        AdvertisementStatus = _peripheral.AdvertisementStatus.ToString();
        KeyboardSubscribers = _peripheral.SubscribedKeyboardClients;
        MouseSubscribers = _peripheral.SubscribedMouseClients;
        // Ctrl+D+C changes the target without going through the UI, so mirror it back.
        Target = _peripheral.SelectedHostDisplay;

        // Hosts subscribe long after the peripheral starts, so the list cannot be built only once.
        if (Signature(_peripheral) != _hostSignature) _ = RefreshHostsAsync();
        else SyncSelection();

        OnPropertyChanged(nameof(CanCapture));
    }

    private void Append(string message)
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.BeginInvoke(() => Append(message));
            return;
        }

        Log.Add($"{DateTime.Now:HH:mm:ss}  {message.TrimEnd()}");
        while (Log.Count > MaxLogLines) Log.RemoveAt(0);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(name);
        if (name is nameof(IsRunning) or nameof(KeyboardSubscribers)) OnPropertyChanged(nameof(CanCapture));
    }
}
