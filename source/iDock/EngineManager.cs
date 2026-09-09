using System.Diagnostics;
using System.IO;

namespace iDock;

internal sealed class EngineManager : IDisposable
{
    private ProcessJob? mirror;
    private ProcessJob? control;
    private Process? diagnostic;
    private readonly string root;
    private readonly MirrorWindowLifecycle mirrorLifecycle = new();
    public EngineManager(string root) => this.root = root;
    // Hardware-free ownership fixtures use real, isolated jobs, never a BLE peripheral.
    internal EngineManager(string root, ProcessJob mirror, ProcessJob control) : this(root)
    { this.mirror = mirror; this.control = control; }
    public bool MirrorRunning => mirror?.IsRunning == true;
    public bool ControlRunning => control?.IsRunning == true;
    public string BleLogPath => Path.Combine(UserStorage.Root, "data", "blehid", "logs", "blehid.log");

    public static bool HasExistingControl()
    {
        if (!Mutex.TryOpenExisting(@"Local\BleHid.Peripheral", out var mutex)) return false;
        mutex.Dispose(); return true;
    }
    private string Exe(string folder, string name)
    {
        var result = Path.Combine(root, "vendor", folder, name);
        if (!File.Exists(result)) throw UiText.TagException(
            new FileNotFoundException(UiText.T("Engine.MissingComponent", ProductInfo.DisplayName), result),
            "Engine.MissingComponent", ProductInfo.DisplayName);
        return result;
    }
    internal static ProcessStartInfo StartInfo(string exe, params string[] args)
    {
        var result = new ProcessStartInfo(exe)
        {
            WorkingDirectory = Path.GetDirectoryName(exe)!, UseShellExecute = false,
            CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden
        };
        foreach (var arg in args) result.ArgumentList.Add(arg);
        return result;
    }
    public void StartMirror()
    {
        if (MirrorRunning) return;
        var exe = Exe("uxplay", "uxplay-windows.exe");
        foreach (var existing in Process.GetProcessesByName("uxplay-windows"))
        {
            using (existing)
            {
                string? path = null;
                try { path = existing.MainModule?.FileName; } catch { }
                if (string.Equals(path, exe, StringComparison.OrdinalIgnoreCase))
                    throw UiText.TagException(new InvalidOperationException(UiText.T("Engine.MirrorAlreadyRunning")), "Engine.MirrorAlreadyRunning");
            }
        }
        mirror?.Dispose();
        var info = StartInfo(exe);
        info.WindowStyle = ProcessWindowStyle.Normal; // This is the interactive receiver the user just opened.
        mirror = new ProcessJob(info);
        mirrorLifecycle.Begin();
    }
    internal MirrorLifecycleEvent PollMirrorLifecycle()
    {
        if (mirror is null) return MirrorLifecycleEvent.None;
        var session = mirrorLifecycle.Generation;
        var snapshot = mirrorLifecycle.Capture(mirror.ReadIsRunning, mirror.ContainsProcess);
        return mirrorLifecycle.Observe(session, snapshot,
            TimeSpan.FromSeconds((double)Stopwatch.GetTimestamp() / Stopwatch.Frequency));
    }
    internal MirrorWindowSnapshot CaptureVideoWindows() => mirror is { } owned
        ? mirrorLifecycle.Capture(owned.ReadIsRunning, owned.ContainsProcess)
        : new(true, false, []);
    internal Func<uint, bool> GetMirrorOwnershipCheck()
    {
        var owned = mirror;
        return processId =>
        {
            if (owned is null || !ReferenceEquals(mirror, owned)) return false;
            try { return owned.IsRunning && owned.ContainsProcess(processId); }
            catch (ObjectDisposedException) { return false; } // Session ended during an asynchronous capture.
            catch (System.ComponentModel.Win32Exception) { return false; }
        };
    }
    public void StartControl(string? screenshotEventName = null)
    {
        if (ControlRunning) return;
        if (HasExistingControl()) throw UiText.TagException(new InvalidOperationException(UiText.T("Engine.ControlInUse")), "Engine.ControlInUse");
        control?.Dispose();
        var info = StartInfo(Exe("blehid", "BleHid.Cli.exe"), "--background");
        // Do not inherit a stale endpoint from another launcher/session.
        info.Environment.Remove("BLEHID_SCREENSHOT_EVENT");
        if (screenshotEventName is not null) info.Environment["BLEHID_SCREENSHOT_EVENT"] = screenshotEventName;
        control = new ProcessJob(info);
    }
    public void StopControl()
    {
        // Only our HID job. Keep receiver, video observation and diagnostics intact.
        var owned = control; control = null;
        owned?.Dispose();
    }
    public async Task<(int ExitCode, string Report)> DiagnoseAsync()
    {
        if (ControlRunning || HasExistingControl())
            throw UiText.TagException(new InvalidOperationException(UiText.T("Engine.StopControlBeforeDiagnostic")), "Engine.StopControlBeforeDiagnostic");
        var info = StartInfo(Exe("blehid", "BleHid.Cli.exe"), "--diagnose");
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        using var process = Process.Start(info) ?? throw UiText.TagException(new InvalidOperationException(UiText.T("Engine.DiagnosticStartFailed")), "Engine.DiagnosticStartFailed");
        diagnostic = process;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(50));
        var timedOut = false;
        try
        {
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException)
            { timedOut = true; if (!process.HasExited) process.Kill(); await process.WaitForExitAsync(); }
            var report = await stdout + Environment.NewLine + await stderr;
            if (timedOut) report += "\n" + UiText.T("Engine.DiagnosticTimeout");
            return (timedOut ? -1 : process.ExitCode, report);
        }
        finally { diagnostic = null; }
    }
    public Task StopAsync()
    {
        // The upstream stop event is global. Closing our job is scoped to this
        // session even if another peripheral wins the startup mutex race.
        Dispose();
        return Task.CompletedTask;
    }
    public void Dispose()
    {
        mirrorLifecycle.Reset();
        // Clear references before disposing so a queued refresh cannot inspect an
        // ended job. Always attempt all owned cleanup even if one disposal fails.
        var ownedControl = control; control = null;
        var ownedMirror = mirror; mirror = null;
        try { ownedControl?.Dispose(); }
        finally
        {
            try { ownedMirror?.Dispose(); }
            finally
            {
                try { if (diagnostic is { HasExited: false }) diagnostic.Kill(); }
                catch (InvalidOperationException) { }
            }
        }
    }
}
