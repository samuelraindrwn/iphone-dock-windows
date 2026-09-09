using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;

namespace iDock;

public partial class MainWindow
{
    private ScreenshotHotkey? screenshotHotkey;
    private ScreenshotRequestChannel? screenshotChannel;
    private CancellationTokenSource? screenshotCancellation;
    private bool screenshotBusy;
    private string screenshotStatusKey = "Screenshot.Ready";
    private object[] screenshotStatusArgs = [];

    private void InitializeScreenshotShortcut()
    {
        if (closing || closed || previewMode) return;
        var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        screenshotHotkey = new ScreenshotHotkey(source, OnScreenshotShortcut);
        if (!screenshotHotkey.Registered)
        { SetScreenshotStatus("Screenshot.HotkeyUnavailable"); AppendT("Screenshot.HotkeyUnavailable"); }
    }

    private void StartScreenshotChannel()
    {
        StopScreenshotChannel();
        try
        {
            ScreenshotRequestChannel? channel = null;
            channel = new ScreenshotRequestChannel(() =>
            {
                if (Dispatcher.HasShutdownStarted) return;
                _ = Dispatcher.BeginInvoke(new Action(() =>
                {
                    // A queued request cannot survive disabling/restarting its control session.
                    if (!closing && !closed && ReferenceEquals(screenshotChannel, channel) && engines.ControlRunning)
                        OnScreenshotShortcut();
                }));
            });
            screenshotChannel = channel;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or WaitHandleCannotBeOpenedException)
        { AppendT("Screenshot.ChannelUnavailable", ex); }
    }

    private void StopScreenshotChannel()
    {
        var owned = screenshotChannel; screenshotChannel = null;
        owned?.Dispose();
    }

    private void OnScreenshotShortcut()
    {
        if (!RejectScreenshotWhileRecording()) _ = CapturePhoneScreenshotAsync();
    }

    internal bool RejectScreenshotWhileRecording()
    {
        // RegisterHotKey can intercept S before WPF's focused recorder sees it.
        // Recording a reserved combination is not consent to save a phone image.
        foreach (var dialog in OwnedWindows.OfType<HotkeyWindow>())
        {
            if (!dialog.IsRecording) continue;
            dialog.ProcessRecordingKey(System.Windows.Input.Key.S,
                System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Alt);
            return true;
        }
        return false;
    }

    internal static ReceiverWindow SelectScreenshotWindow(MirrorWindowSnapshot snapshot)
    {
        if (!snapshot.ReadSucceeded || !snapshot.ReceiverRunning || snapshot.Windows.Count == 0)
            throw UiText.TagException(new InvalidOperationException(), "Screenshot.NoVideo");
        if (snapshot.Windows.Count != 1)
            throw UiText.TagException(new InvalidOperationException(), "Screenshot.Ambiguous");
        var window = snapshot.Windows[0];
        if (!MirrorWindowLifecycle.IsVideoWindow(window) || !window.Visible || window.Minimized)
            throw UiText.TagException(new InvalidOperationException(), "Screenshot.NoVideo");
        return window;
    }

    private async Task CapturePhoneScreenshotAsync()
    {
        // Screenshot work must not lock out Disable control or the emergency shortcut.
        if (closing || closed || endingSession || screenshotBusy || previewMode) return;
        screenshotBusy = true;
        // Capture bounds frame waiting itself. A slow disk must not be mislabeled
        // as a video timeout; cancellation still prevents publication after stop.
        using var cancellation = new CancellationTokenSource();
        screenshotCancellation = cancellation;
        Func<uint, bool>? captureOwnership = null;
        uint captureProcessId = 0;
        SetButtons();
        try
        {
            var target = SelectScreenshotWindow(engines.CaptureVideoWindows());
            var ownsProcess = engines.GetMirrorOwnershipCheck();
            captureOwnership = ownsProcess;
            captureProcessId = target.ProcessId;
            SetScreenshotStatus("Screenshot.Capturing");
            var image = await ScreenshotCapture.CaptureAsync(target, ownsProcess, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (closing || closed || !ownsProcess(target.ProcessId)) return;
            var path = await Task.Run(() => ScreenshotStorage.Save(image, ScreenshotStorage.DefaultDirectory, cancellation.Token), cancellation.Token);
            if (closing || closed || !ownsProcess(target.ProcessId)) return;
            SetScreenshotStatus("Screenshot.Saved", path);
            AppendT("Screenshot.Saved", path);
        }
        catch (OperationCanceledException) when (closing || closed ||
            (captureOwnership is not null && !captureOwnership(captureProcessId)))
        { if (!closing && !closed) SetScreenshotStatus("Screenshot.Ready"); }
        catch (OperationCanceledException)
        { SetScreenshotStatus("Screenshot.Failed", UiText.TagException(new TimeoutException(), "Screenshot.Timeout")); AppendT("Screenshot.Timeout"); }
        catch (Exception ex)
        { if (!closing && !closed) { SetScreenshotStatus("Screenshot.Failed", ex); AppendT("Screenshot.Failed", ex); } }
        finally
        {
            screenshotCancellation = null;
            screenshotBusy = false;
            if (!closing && !closed) SetButtons();
        }
    }

    private async void Screenshot_Click(object sender, RoutedEventArgs e) => await CapturePhoneScreenshotAsync();

    private void OpenScreenshots_Click(object sender, RoutedEventArgs e)
    {
        if (previewMode || closing || closed) return;
        try
        {
            Directory.CreateDirectory(ScreenshotStorage.DefaultDirectory);
            Process.Start(new ProcessStartInfo(ScreenshotStorage.DefaultDirectory) { UseShellExecute = true });
        }
        catch (Exception ex) { SetScreenshotStatus("Screenshot.Failed", ex); }
    }

    private void SetScreenshotStatus(string key, params object[] args)
    { screenshotStatusKey = key; screenshotStatusArgs = args; RenderScreenshotStatus(); }

    private void RenderScreenshotStatus() => ScreenshotStatus.Text = UiText.T(screenshotStatusKey, ResolveArguments(screenshotStatusArgs));
}
