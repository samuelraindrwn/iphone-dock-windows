using System.IO;
using System.Windows;
using System.Windows.Threading;
using BleHid.App.Services;
using BleHid.Core;
using Wpf.Ui.Appearance;

namespace BleHid.App;

public partial class App : Application
{
    private static readonly string LogPath = AppPaths.InLogs("blehid-app.log");

    private Mutex? _instance;
    private EventWaitHandle? _stopRequest;
    private RegisteredWaitHandle? _stopRegistration;
    private bool _exiting;

    public new static App Current => (App)Application.Current;

    public TrayIconService? Tray { get; private set; }

    public bool IsExiting => _exiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        if (!TryTakeOwnership())
        {
            Shutdown();
            return;
        }

        // Lets `blehid --stop` shut this down the same way it shuts down background mode.
        _stopRequest = new EventWaitHandle(false, EventResetMode.ManualReset, SingleInstance.StopEventName);
        // A named event that still exists ignores the initial state above, so the stop request that
        // retired the previous owner would fire this one immediately.
        _stopRequest.Reset();
        _stopRegistration = ThreadPool.RegisterWaitForSingleObject(
            _stopRequest, (_, _) => Dispatcher.Invoke(ExitApplication), null, Timeout.Infinite,
            executeOnlyOnce: true);

        ApplicationThemeManager.ApplySystemTheme();
        DispatcherUnhandledException += OnDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Report(args.ExceptionObject as Exception);
        base.OnStartup(e);

        Tray = new TrayIconService(ShowMainWindow, ExitApplication);

        var startHidden = e.Args.Contains(StartupService.TrayArgument, StringComparer.OrdinalIgnoreCase);
        MainWindow = new MainWindow();
        if (!startHidden) MainWindow.Show();

        // Auto-start matters most for the hidden launch, where there is no window to press Start in.
        if (startHidden) _ = StartResidentAsync();
        else if (AppSettings.Instance.StartPeripheralOnLaunch) _ = PeripheralService.Instance.StartAsync();
    }

    // A tray-only launch has no UI to arm capture from, so the hotkeys have to work on their own.
    private static async Task StartResidentAsync()
    {
        await PeripheralService.Instance.StartAsync();
        await PeripheralService.Instance.StartCaptureAsync(resident: true);
    }

    /// <summary>
    /// The holder may be another copy of this app or the CLI's background service; either way it
    /// owns the radio and has to release it before this process can advertise.
    /// </summary>
    private bool TryTakeOwnership()
    {
        _instance = new Mutex(true, SingleInstance.MutexName, out var owned);
        if (owned) return true;

        var answer = MessageBox.Show(
            "BLE HID is already running, either in the notification area or as the command line "
            + "background service.\n\nStop it and continue?",
            "BLE HID", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return false;

        try
        {
            using var stop = EventWaitHandle.OpenExisting(SingleInstance.StopEventName);
            stop.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            MessageBox.Show(
                "The running instance is too old to accept a stop request. Exit it manually.",
                "BLE HID", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        for (var attempt = 0; attempt < 50 && !owned; attempt++)
        {
            Thread.Sleep(100);
            _instance.Dispose();
            _instance = new Mutex(true, SingleInstance.MutexName, out owned);
        }

        if (!owned)
            MessageBox.Show("The running instance did not stop in time.",
                "BLE HID", MessageBoxButton.OK, MessageBoxImage.Warning);

        return owned;
    }

    public void ShowMainWindow()
    {
        MainWindow ??= new MainWindow();
        MainWindow.Show();
        if (MainWindow.WindowState == WindowState.Minimized) MainWindow.WindowState = WindowState.Normal;
        MainWindow.Activate();
    }

    public async void ExitApplication()
    {
        if (_exiting) return;
        _exiting = true;
        await PeripheralService.Instance.StopAsync();
        Tray?.Dispose();
        // Shutdown closes windows, so it must not run inside a close that is still unwinding.
        await Dispatcher.InvokeAsync(Shutdown);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _stopRegistration?.Unregister(null);
        _stopRequest?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }

    // A XAML error on one page used to terminate the process with no output at all.
    private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Report(e.Exception);
        e.Handled = true;
        MessageBox.Show($"{e.Exception.Message}\n\nLogged to {LogPath}",
            "BLE HID", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static void Report(Exception? ex)
    {
        if (ex is null) return;
        try
        {
            // The tray build can run for weeks, so the log cannot grow without a bound.
            if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 5 * 1024 * 1024) File.Delete(LogPath);
            File.AppendAllText(LogPath, $"{DateTime.Now:s}  {ex}\n\n");
        }
        catch (IOException) { }
    }
}
