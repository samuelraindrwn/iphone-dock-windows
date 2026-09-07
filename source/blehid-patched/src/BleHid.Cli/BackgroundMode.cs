using System.Diagnostics;
using System.Runtime.InteropServices;
using BleHid.Core;
using Microsoft.Win32;

namespace BleHid.Cli;

/// <summary>
/// Runs the peripheral detached from any console so the GATT attribute table stays put.
/// Hosts only reconnect reliably to a peripheral that never restarted.
/// </summary>
internal static class BackgroundMode
{
    private const string MutexName = SingleInstance.MutexName;
    private const string StopEventName = SingleInstance.StopEventName;
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "BleHid";

    public static async Task<int> RunAsync(bool requireEncryption)
    {
        using var instance = new Mutex(true, MutexName, out var isOnlyInstance);
        if (!isOnlyInstance)
        {
            Console.Error.WriteLine("BleHid is already running in the background.");
            return 1;
        }

        var logPath = LogPath();
        if (File.Exists(logPath) && new FileInfo(logPath).Length > 5 * 1024 * 1024) File.Delete(logPath);

        using var writer = new StreamWriter(logPath, append: true) { AutoFlush = true };
        var gate = new object();
        void Log(string message)
        {
            lock (gate) writer.WriteLine($"{DateTime.Now:HH:mm:ss} {message}");
        }

        FreeConsole();

        Log($"--- background start (pid {Environment.ProcessId}) ---");

        await using var peripheral = new BleHidPeripheral(requireEncryption);
        peripheral.Log += Log;

        try
        {
            await peripheral.StartAsync();
        }
        catch (Exception ex)
        {
            Log($"startup failed: {ex.Message}");
            return 1;
        }

        Log($"advertising: {peripheral.AdvertisementStatus}");

        // Start on the local target so input keeps working normally until a host is chosen.
        peripheral.SelectLocal();

        using var stopEvent = new EventWaitHandle(false, EventResetMode.ManualReset, StopEventName);
        // A named event that still exists ignores the initial state above, so the stop request that
        // retired the previous owner would fire this one immediately.
        stopEvent.Reset();
        using var cancellation = new CancellationTokenSource();
        var stopRegistration = ThreadPool.RegisterWaitForSingleObject(
            stopEvent, (_, _) => cancellation.Cancel(), null, Timeout.Infinite, executeOnlyOnce: true);

        try
        {
            await CaptureSession.RunAsync(peripheral, Log, verbose: false, mouseIntervalMs: 10,
                stopEndsSession: false, cancellation.Token);
        }
        catch (Exception ex)
        {
            Log($"capture failed: {ex}");
        }

        stopRegistration.Unregister(null);
        await peripheral.DisposeAsync();
        Log("--- background stop ---");
        return 0;
    }

    public static int Stop()
    {
        try
        {
            using var stopEvent = EventWaitHandle.OpenExisting(StopEventName);
            stopEvent.Set();
            Console.WriteLine("Stop signalled.");
            return 0;
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            Console.WriteLine("No background instance is running.");
            return 1;
        }
    }

    public static int InstallAutoStart(bool install)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key is null)
        {
            Console.WriteLine("Could not open the Run key.");
            return 1;
        }

        if (!install)
        {
            key.DeleteValue(RunValue, throwOnMissingValue: false);
            Console.WriteLine("Auto-start removed.");
            return 0;
        }

        var exe = Environment.ProcessPath;
        if (exe is null || exe.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Run this from the published executable, not via 'dotnet run'.");
            return 1;
        }

        key.SetValue(RunValue, $"\"{exe}\" --background");
        Console.WriteLine($"Auto-start installed: \"{exe}\" --background");
        return 0;
    }

    public static void ReportStatus()
    {
        using var _ = new Mutex(true, MutexName, out var isOnlyInstance);
        Console.WriteLine(isOnlyInstance ? "Background: not running" : "Background: running");

        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        Console.WriteLine($"Auto-start: {key?.GetValue(RunValue) ?? "not installed"}");
        Console.WriteLine($"Log file  : {LogPath()}");
    }

    private static string LogPath() => AppPaths.InLogs("blehid.log");

    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();
}
