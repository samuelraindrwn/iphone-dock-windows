using System.IO;
using System.Text.Json;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace iDock;

/// <summary>
/// Hardware-free checks for the configurable switch hotkey. No hooks are installed and no
/// key is ever sent; only validation, persistence, and the fixed release binding are exercised.
/// </summary>
internal static class HotkeyVerification
{
    internal static void Run(Action<bool, string> check)
    {
        var directory = Path.Combine(Path.GetTempPath(), "idock-hotkey-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            CheckDefaults(check);
            CheckValidation(check);
            CheckPersistence(check, directory);
            CheckDescriptions(check);
            CheckRecording(check, directory);
            CheckWindow(check, directory);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static void CheckDefaults(Action<bool, string> check)
    {
        check(HotkeySettings.DefaultSwitch == new HotkeyConfiguration(
                HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x44),
            "The default switch hotkey is Ctrl + Alt + D");
        check(HotkeySettings.Release == new HotkeyConfiguration(
                HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x51),
            "The release hotkey is Ctrl + Alt + Q");
        check(HotkeySettings.Screenshot == new HotkeyConfiguration(
                HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x53),
            "The fixed screenshot hotkey is Ctrl + Alt + S");
        check(HotkeySettings.IsValid(HotkeySettings.DefaultSwitch)
            && !HotkeySettings.ConflictsWithRelease(HotkeySettings.DefaultSwitch),
            "The default switch hotkey is valid and distinct from the release hotkey");
    }

    private static void CheckValidation(Action<bool, string> check)
    {
        check(!HotkeySettings.IsValid(HotkeyModifiers.None, 0x44),
            "A hotkey without a modifier is rejected so ordinary typing still reaches the device");
        check(!HotkeySettings.IsValid((HotkeyModifiers)9, 0x44)
            && !HotkeySettings.IsValid((HotkeyModifiers)(-1), 0x44),
            "Unknown modifier flags are rejected by the direct validation API");
        check(!HotkeySettings.IsValid(HotkeyModifiers.Shift, 0x44)
            && !HotkeySettings.IsValid(HotkeyModifiers.Shift, 0x09),
            "Shift alone cannot turn ordinary typing or navigation into a global switch shortcut");
        check(!HotkeySettings.IsValid(HotkeyModifiers.Control, 0x1B),
            "Escape remains available to cancel shortcut recording and cannot be assigned");
        foreach (var modifierKey in new[] { 0x10, 0x11, 0x12, 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0x5B, 0x5C })
            check(!HotkeySettings.IsValid(HotkeyModifiers.Control, modifierKey),
                "A modifier key cannot be the trigger key of its own combination");
        check(!HotkeySettings.IsValid(HotkeyModifiers.Control, 0x14)
            && !HotkeySettings.IsValid(HotkeyModifiers.Control, 0x90)
            && !HotkeySettings.IsValid(HotkeyModifiers.Control, 0x91),
            "Lock keys are not accepted as a hotkey trigger");
        check(!HotkeySettings.IsValid(HotkeyModifiers.Control, 0x2C),
            "A key the backend cannot send as HID is not offered as a hotkey");
        check(HotkeySettings.IsValid(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x70)
            && HotkeySettings.IsValid(HotkeyModifiers.Alt, 0x31)
            && HotkeySettings.IsValid(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x28),
            "Function, digit, and arrow keys are accepted with a modifier");
        check(HotkeySettings.ConflictsWithRelease(new HotkeyConfiguration(
                HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x51)),
            "Reassigning the release combination is detected as a conflict");
        check(!HotkeySettings.ConflictsWithRelease(new HotkeyConfiguration(HotkeyModifiers.Control, 0x51)),
            "A different modifier set on the same key is not the release combination");
        check(HotkeySettings.ConflictsWithRelease(new HotkeyConfiguration(
                HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift, 0x51)),
            "Ctrl + Alt + Shift + Q is also reserved because the emergency release takes priority");
        check(HotkeySettings.ConflictsWithScreenshot(HotkeySettings.Screenshot)
            && !HotkeySettings.ConflictsWithScreenshot(new HotkeyConfiguration(
                HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift, 0x53)),
            "The screenshot reservation matches exactly Ctrl + Alt + S");
    }

    private static void CheckPersistence(Action<bool, string> check, string directory)
    {
        var path = Path.Combine(directory, "hotkey-settings.json");
        check(HotkeySettings.Load(path) == HotkeySettings.DefaultSwitch && !File.Exists(path),
            "A missing hotkey file yields the default without creating a file");

        var custom = new HotkeyConfiguration(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x53);
        HotkeySettings.Save(path, custom);
        check(HotkeySettings.Load(path) == custom, "A saved hotkey round-trips through JSON");
        check(Directory.GetFiles(directory, ".hotkey-settings-*.tmp").Length == 0,
            "Hotkey settings replace atomically without leaving temporary files");

        var validJson = File.ReadAllText(path);
        File.WriteAllText(path, validJson, new UTF8Encoding(true));
        check(HotkeySettings.Load(path) == custom,
            "UTF-8 BOM settings from Windows PowerShell are read identically to the backend");
        File.WriteAllText(path, validJson, Encoding.Unicode);
        check(Throws<JsonException>(() => HotkeySettings.Load(path)),
            "UTF-16 settings are rejected rather than interpreted differently by launcher and backend");
        File.WriteAllBytes(path, Encoding.UTF8.GetBytes(validJson[..^1] + ",\"Unused\":\"")
            .Concat(new byte[] { 0xC3, 0x28, 0x22, 0x7D }).ToArray());
        check(Throws<JsonException>(() => HotkeySettings.Load(path)),
            "Malformed UTF-8 even in an unused property cannot produce a different active binding");
        HotkeySettings.Save(path, custom);

        var before = File.ReadAllText(path);
        check(Throws<ArgumentOutOfRangeException>(() => HotkeySettings.Save(path, HotkeySettings.Release))
            && File.ReadAllText(path) == before,
            "The release combination cannot be saved as the switch hotkey");
        check(Throws<ArgumentOutOfRangeException>(() =>
                HotkeySettings.Save(path, new HotkeyConfiguration(HotkeyModifiers.None, 0x44)))
            && File.ReadAllText(path) == before,
            "An invalid hotkey cannot overwrite a valid saved combination");
        check(Throws<ArgumentOutOfRangeException>(() => HotkeySettings.Save(path, HotkeySettings.Screenshot))
            && File.ReadAllText(path) == before,
            "The screenshot shortcut cannot overwrite the saved switch shortcut");
        check(Throws<ArgumentOutOfRangeException>(() => HotkeySettings.Save(path,
                new HotkeyConfiguration(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift, 0x51)))
            && File.ReadAllText(path) == before,
            "A shifted emergency-release combination cannot overwrite the saved switch shortcut");
        check(Throws<ArgumentOutOfRangeException>(() => HotkeySettings.Save(path,
                new HotkeyConfiguration((HotkeyModifiers)9, 0x44)))
            && File.ReadAllText(path) == before,
            "Unknown modifier bits cannot be persisted through the save API");

        foreach (var invalid in new[]
        {
            "{unfinished", "[]", "{}", "{\"SwitchTarget\":null}",
            "{\"SwitchTarget\":{}}",
            "{\"SwitchTarget\":{\"Modifiers\":1}}",
            "{\"SwitchTarget\":{\"VirtualKey\":68}}",
            "{\"SwitchTarget\":{\"Modifiers\":0,\"VirtualKey\":68}}",
            "{\"SwitchTarget\":{\"Modifiers\":3,\"VirtualKey\":17}}",
            "{\"SwitchTarget\":{\"Modifiers\":64,\"VirtualKey\":68}}",
            "{\"SwitchTarget\":{\"Modifiers\":-1,\"VirtualKey\":68}}",
            "{\"SwitchTarget\":{\"Modifiers\":4,\"VirtualKey\":68}}",
            "{\"SwitchTarget\":{\"Modifiers\":1,\"VirtualKey\":27}}",
            "{\"SwitchTarget\":{\"Modifiers\":7,\"VirtualKey\":81}}",
            "{\"SwitchTarget\":{\"Modifiers\":3,\"VirtualKey\":83}}",
            "{\"SwitchTarget\":{\"Modifiers\":3,\"VirtualKey\":81}}",
            "{\"SwitchTarget\":{\"Modifiers\":\"3\",\"VirtualKey\":68}}"
        })
        {
            File.WriteAllText(path, invalid);
            check(Throws<JsonException>(() => HotkeySettings.Load(path)) && File.ReadAllText(path) == invalid,
                "Invalid hotkey JSON is reported instead of silently overwritten");
        }

        File.WriteAllText(path, "{\"SwitchTarget\":{\"Modifiers\":3,\"VirtualKey\":68},\"Padding\":\""
            + new string('x', 4200) + "\"}");
        check(Throws<JsonException>(() => HotkeySettings.Load(path)),
            "An oversized hotkey file is rejected rather than parsed");

        HotkeySettings.Save(path, custom);
        before = File.ReadAllText(path);
        File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
        try
        {
            check(ThrowsSaveFailure(() => HotkeySettings.Save(path, HotkeySettings.DefaultSwitch))
                && File.ReadAllText(path) == before,
                "A read-only hotkey file rejects replacement without losing the saved combination");
            check(Directory.GetFiles(directory, ".hotkey-settings-*.tmp").Length == 0,
                "A failed read-only hotkey save cleans up its temporary file");
        }
        finally { File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly); }
    }

    private static void CheckRecording(Action<bool, string> check, string directory)
    {
        var path = Path.Combine(directory, "recording-hotkey.json");
        var active = new HotkeyConfiguration(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x4B);
        var configured = new HotkeyConfiguration(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x70);
        HotkeySettings.Save(path, configured);
        var window = new HotkeyWindow(path, controlRunning: true, activeHotkey: active);
        try
        {
            check(window.RestartNeeded && window.Current == configured
                && window.ActiveHotkeyText.Text.Contains("Ctrl + Alt + K", StringComparison.Ordinal),
                "The dialog distinguishes an active binding from a pending saved binding");
            var before = File.ReadAllText(path);
            window.BeginRecording(focus: false);
            check(window.IsRecording && !window.RecordButton.IsEnabled,
                "Recording is explicitly armed without installing a keyboard hook");
            check(window.ProcessRecordingKey(Key.LeftCtrl, ModifierKeys.Control) && window.IsRecording
                && File.ReadAllText(path) == before,
                "A modifier-only event waits for the rest of the combination");
            check(window.ProcessRecordingKey(Key.K, ModifierKeys.Control | ModifierKeys.Windows)
                && window.IsRecording && File.ReadAllText(path) == before
                && window.SwitchStatus.Text == UiText.T("Hotkey.WindowsUnsupported"),
                "A Windows-key combination is rejected rather than silently losing its Windows modifier");
            check(window.ProcessRecordingKey(Key.K, ModifierKeys.Shift)
                && window.IsRecording && File.ReadAllText(path) == before,
                "The recorder refuses Shift-only combinations without saving them");
            check(window.ProcessRecordingKey(Key.Q, ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift)
                && window.IsRecording && File.ReadAllText(path) == before
                && window.SwitchStatus.Text == UiText.T("Hotkey.ReservedRelease"),
                "The recorder protects shifted emergency release combinations");
            check(window.ProcessRecordingKey(Key.S, ModifierKeys.Control | ModifierKeys.Alt)
                && window.IsRecording && File.ReadAllText(path) == before
                && window.SwitchStatus.Text == UiText.T("Hotkey.ReservedScreenshot"),
                "The recorder protects the fixed screenshot shortcut");
            check(window.ProcessRecordingKey(Key.F2, ModifierKeys.Control | ModifierKeys.Shift, isRepeat: true)
                && window.IsRecording && File.ReadAllText(path) == before,
                "A key already repeating when recording begins cannot accidentally save a shortcut");
            check(window.ProcessRecordingKey(Key.F2, ModifierKeys.Control | ModifierKeys.Shift)
                && !window.IsRecording && window.RecordButton.IsEnabled && window.RestartNeeded
                && HotkeySettings.Load(path) == new HotkeyConfiguration(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x71),
                "A fresh function-key combination is recorded and marked pending for the running backend");
            before = File.ReadAllText(path);
            check(window.ProcessRecordingKey(Key.F2, ModifierKeys.Control | ModifierKeys.Shift, isRepeat: true)
                && File.ReadAllText(path) == before,
                "Repeats of an accepted key stay consumed after recording stops");
            check(window.ProcessRecordedKeyUp(Key.F2)
                && !window.ProcessRecordingKey(Key.F2, ModifierKeys.Control | ModifierKeys.Shift),
                "The accepted key-up is consumed once and then normal dialog input resumes");

            window.BeginRecording(focus: false);
            window.ProcessRecordingKey(Key.K, ModifierKeys.Control | ModifierKeys.Alt);
            check(window.Current == active && !window.RestartNeeded,
                "Saving the already active binding clears the pending-restart state");

            before = File.ReadAllText(path);
            window.BeginRecording(focus: false);
            check(window.ProcessRecordingKey(Key.Escape, ModifierKeys.None)
                && !window.IsRecording && File.ReadAllText(path) == before,
                "Escape cancels recording without changing the saved shortcut");
            check(window.ProcessRecordingKey(Key.Escape, ModifierKeys.None, isRepeat: true)
                && window.ProcessRecordedKeyUp(Key.Escape),
                "A held cancellation Escape cannot fall through to the dialog's Close action");

            window.BeginRecording(focus: false);
            window.CancelRecordingOnFocusLoss();
            check(!window.IsRecording && window.RecordButton.IsEnabled
                && !window.ProcessRecordingKey(Key.J, ModifierKeys.Control)
                && File.ReadAllText(path) == before,
                "Losing focus cancels recording so later typing cannot silently change settings");

            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
            try
            {
                window.BeginRecording(focus: false);
                window.ProcessRecordingKey(Key.F3, ModifierKeys.Control);
                check(window.Current == active && !window.RestartNeeded && File.ReadAllText(path) == before
                    && window.SwitchStatus.Text.Contains(UiText.T("Hotkey.SaveFailed", "").TrimEnd(), StringComparison.Ordinal),
                    "A failed save keeps both the displayed and persisted binding unchanged");
            }
            finally { File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly); }
        }
        finally { window.Close(); }
    }

    private static void CheckDescriptions(Action<bool, string> check)
    {
        check(HotkeySettings.Describe(HotkeySettings.DefaultSwitch) == "Ctrl + Alt + D",
            "The default switch hotkey reads as Ctrl + Alt + D");
        check(HotkeySettings.Describe(HotkeySettings.Release) == "Ctrl + Alt + Q",
            "The release hotkey reads as Ctrl + Alt + Q");
        check(HotkeySettings.Describe(new HotkeyConfiguration(
                HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift, 0x70)) == "Ctrl + Alt + Shift + F1",
            "Modifiers are described in a stable order");
        var indonesian = SelectAnd("id", () => HotkeySettings.Describe(HotkeySettings.DefaultSwitch));
        var english = SelectAnd("en", () => HotkeySettings.Describe(HotkeySettings.DefaultSwitch));
        check(indonesian == english,
            "Hotkey key names are identical in both languages so a language change cannot alter them");
    }

    /// <summary>
    /// Opens the real dialog offscreen. It is never shown modally and no key is recorded,
    /// so nothing here can steal focus or write to an installed configuration.
    /// </summary>
    private static void CheckWindow(Action<bool, string> check, string directory)
    {
        var path = Path.Combine(directory, "window-hotkey.json");
        var custom = new HotkeyConfiguration(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x4B);
        HotkeySettings.Save(path, custom);

        foreach (var language in new[] { "id", "en" })
        {
            var previous = UiText.CurrentLanguage;
            UiText.SelectLanguage(language);
            HotkeyWindow? window = null;
            try
            {
                window = new HotkeyWindow(path, controlRunning: false)
                {
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -20000, Top = -20000, ShowActivated = false, ShowInTaskbar = false
                };
                window.Show();
                Settle(window);

                check(window.Current == custom && window.SwitchHotkeyText.Text == "Ctrl + Alt + K",
                    $"The shortcut window shows the saved combination in {language}");
                check(window.ReleaseHotkeyText.Text == "Ctrl + Alt + Q",
                    $"The shortcut window shows the fixed release combination in {language}");
                check(window.ScreenshotHotkeyText.Text == "Ctrl + Alt + S",
                    $"The shortcut window shows the fixed screenshot combination in {language}");
                check(window.ActiveHotkeyText.Visibility == Visibility.Collapsed,
                    $"An inactive control session does not claim an active shortcut in {language}");
                check(window.ResizeMode != ResizeMode.NoResize,
                    $"The shortcut window can be resized in {language}");

                var scroll = window.HotkeyScrollViewer;
                // The window must stay usable when it is dragged down to its minimum, which is
                // where long translations and large system fonts run out of room.
                foreach (var size in new[] { new Size(window.MinWidth, window.MinHeight), new Size(560, 470) })
                {
                    window.Width = size.Width;
                    window.Height = size.Height;
                    scroll.ScrollToTop();
                    Settle(window);
                    var label = $"{size.Width:0}x{size.Height:0} in {language}";

                    check(scroll.ViewportWidth > 0 && scroll.ViewportHeight > 0
                        && scroll.HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled
                        && scroll.ExtentWidth <= scroll.ViewportWidth + 1,
                        $"The shortcut window never scrolls horizontally at {label}");
                    check(scroll.ExtentHeight <= scroll.ViewportHeight + 1 || scroll.ComputedVerticalScrollBarVisibility == Visibility.Visible,
                        $"Content taller than the shortcut window is reachable by scrolling at {label}");

                    // Record, Reset and Close must never scroll out of reach: they are the
                    // only way to change or dismiss the dialog.
                    var actions = new FrameworkElement[] { window.RecordButton, window.ResetButton, window.CloseButton };
                    check(actions.All(control => control.ActualWidth > 0 && control.ActualHeight > 0
                        && FitsHorizontally(control, window)),
                        $"Record, restore and close stay visible and unclipped at {label}");
                    check(FitsHorizontally(window.SwitchHotkeyText, window)
                        && FitsHorizontally(window.ReleaseHotkeyText, window),
                        $"Both shortcut labels stay within the window width at {label}");
                }
            }
            finally
            {
                window?.Close();
                UiText.SelectLanguage(previous);
            }
        }
    }

    private static void Settle(Window window)
    {
        window.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
        window.UpdateLayout();
    }

    private static bool FitsHorizontally(FrameworkElement element, Visual ancestor)
    {
        var bounds = element.TransformToAncestor(ancestor).TransformBounds(new Rect(element.RenderSize));
        return bounds.Left >= -1 && ancestor is FrameworkElement frame && bounds.Right <= frame.ActualWidth + 1;
    }

    private static string SelectAnd(string language, Func<string> action)
    {
        var previous = UiText.CurrentLanguage;
        UiText.SelectLanguage(language);
        try { return action(); }
        finally { UiText.SelectLanguage(previous); }
    }

    private static bool Throws<TException>(Action action) where TException : Exception
    {
        try { action(); return false; }
        catch (TException) { return true; }
    }

    private static bool ThrowsSaveFailure(Action action)
    {
        try { action(); return false; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return true; }
    }
}
