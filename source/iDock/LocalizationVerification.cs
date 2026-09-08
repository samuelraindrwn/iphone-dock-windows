using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace iDock;

/// <summary>Hardware-free localization checks. All mutable files are isolated test files.</summary>
internal static class LocalizationVerification
{
    private static readonly Regex Placeholder = new(@"(?<!\{)\{(\d+)(?:,-?\d+)?(?::[^{}]+)?\}(?!\})");

    internal static void Run(Action<bool, string> check)
    {
        var originalLanguage = UiText.CurrentLanguage;
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        var originalBleRoot = Environment.GetEnvironmentVariable("BLEHID_DATA_DIR");
        var directory = Path.Combine(Path.GetTempPath(), "idock-localization-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            CheckCatalog(check);
            CheckSettings(check, directory);
            CheckTrackedStates(check);
            CheckExceptions(check);
            CheckWindow(check, directory);
            check(ReferenceEquals(originalCulture, CultureInfo.CurrentCulture)
                && ReferenceEquals(originalUiCulture, CultureInfo.CurrentUICulture),
                "UI language changes never alter the process culture or UI culture");
            check(Environment.GetEnvironmentVariable("BLEHID_DATA_DIR") == originalBleRoot,
                "UI language changes leave the BLE data environment unchanged");
        }
        finally
        {
            UiText.SelectLanguage(originalLanguage);
            // Only this invocation's freshly created temporary directory is removed.
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static void CheckCatalog(Action<bool, string> check)
    {
        UiText.SelectLanguage("id");
        check(UiText.IsSupported("id") && UiText.IsSupported("en")
            && !UiText.IsSupported(null) && !UiText.IsSupported("EN") && !UiText.IsSupported("fr"),
            "Only explicit id/en language identifiers are supported");
        check(UiText.Entries.Count > 0 && UiText.Entries.All(entry => !string.IsNullOrWhiteSpace(entry.Key)
            && !string.IsNullOrWhiteSpace(entry.Value.Indonesian) && !string.IsNullOrWhiteSpace(entry.Value.English)),
            "Every catalog entry has a nonempty key and both translations");
        check(UiText.Entries.Values.All(entry => Signature(entry.Indonesian).SequenceEqual(Signature(entry.English))),
            "Every translation preserves the same formatting placeholders");
        check(UiText.Entries.Values.All(entry => Formats(entry.Indonesian) && Formats(entry.English)),
            "Both versions of every catalog entry have valid composite formatting");
        check(UiText.Entries is IDictionary<string, UiText.Translation> { IsReadOnly: true },
            "The published translation catalog is read-only");
        check(Throws<ArgumentOutOfRangeException>(() => UiText.SelectLanguage("fr")) && UiText.CurrentLanguage == "id",
            "Rejected language selection preserves the previous language");
        check(Throws<KeyNotFoundException>(() => UiText.T("Verification.MissingKey")),
            "An unknown translation key is reported instead of hidden by fallback");
        foreach (var language in new[] { "id", "en" })
        {
            UiText.SelectLanguage(language);
            var resources = UiText.GetResources();
            check(resources.Count == UiText.Entries.Count && UiText.Entries.Keys.All(key =>
                resources["Text." + key] is string text && text == UiText.ForLanguage(key, language)),
                $"All {language} WPF resource keys exactly match the selected catalog");
        }
        check(UiText.T("Guide.Title") == "Guide"
            && UiText.ForLanguage("Guide.Title", "id") == "Panduan" && UiText.CurrentLanguage == "en",
            "Explicit-language lookup does not change the current language");
        check(UiText.T("Control.Controlling", "QA Device {0}", "mouse").Contains("QA Device {0}", StringComparison.Ordinal),
            "Device names used as arguments are not interpreted as format strings");
        check(UiText.T("Control.ConnectedNotReady", 0.75).StartsWith("0.75 ", StringComparison.Ordinal),
            "Localized text formatting retains invariant numeric arguments");
    }

    private static void CheckSettings(Action<bool, string> check, string directory)
    {
        var path = Path.Combine(directory, "settings-unit.json");
        check(LanguageSettings.Load(path) == "id" && !File.Exists(path),
            "Missing language settings default to Indonesian without creating a file");
        LanguageSettings.Save(path, "en");
        check(LanguageSettings.Load(path) == "en", "English language preference survives a settings reload");
        LanguageSettings.Save(path, "id");
        check(LanguageSettings.Load(path) == "id" && Directory.GetFiles(directory, ".ui-settings-*.tmp").Length == 0,
            "Language settings replace atomically without leaving temporary files");
        var before = File.ReadAllText(path);
        check(Throws<ArgumentOutOfRangeException>(() => LanguageSettings.Save(path, "fr"))
            && File.ReadAllText(path) == before,
            "Unsupported saved languages cannot overwrite a valid preference");
        foreach (var invalid in new[] { "{unfinished", "[]", "{}", "{\"Language\":null}",
            "{\"Language\":1}", "{\"Language\":\"fr\"}", "{\"Language\":\"EN\"}",
            "{\"Language\":\"en\",\"Padding\":\"" + new string('x', 16400) + "\"}" })
        {
            File.WriteAllText(path, invalid);
            check(Throws<JsonException>(() => LanguageSettings.Load(path)) && File.ReadAllText(path) == invalid,
                "Invalid or oversized language JSON is rejected without modifying the source file");
        }
        LanguageSettings.Save(path, "id");
        before = File.ReadAllText(path);
        File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
        try
        {
            check(ThrowsSaveFailure(() => LanguageSettings.Save(path, "en"))
                && File.ReadAllText(path) == before,
                "Read-only language settings reject replacement without losing the saved preference");
            check(Directory.GetFiles(directory, ".ui-settings-*.tmp").Length == 0,
                "A failed read-only language save cleans up its temporary file");
        }
        finally { File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly); }
    }

    private static void CheckTrackedStates(Action<bool, string> check)
    {
        foreach (var fixture in new[] { "connected", "controlling", "advertising-failed", "capture-failed", "fallback-lost" })
        {
            var tracker = new ControlStatusTracker();
            SeedState(tracker, fixture);
            var expected = fixture == "connected" ? ControlConnectionState.Connected
                : fixture == "controlling" ? ControlConnectionState.Controlling : ControlConnectionState.Failed;
            UiText.SelectLanguage("id");
            var indonesian = tracker.DisplayText;
            UiText.SelectLanguage("en");
            var english = tracker.DisplayText;
            check(tracker.State == expected && !string.IsNullOrWhiteSpace(english) && english != indonesian,
                $"Changing to English re-renders {fixture} without altering connection evidence");
            UiText.SelectLanguage("id");
            check(tracker.State == expected && tracker.DisplayText == indonesian
                && (fixture != "controlling" || english.Contains("QA Device {0}", StringComparison.Ordinal)),
                $"Returning to Indonesian preserves {fixture} and any raw target name");
        }
    }

    private static void SeedState(ControlStatusTracker tracker, string fixture)
    {
        tracker.Begin();
        if (fixture != "fallback-lost") tracker.Apply("advertising: Started");
        tracker.Apply("[subs] Keyboard input report: 1 subscriber(s)");
        tracker.Apply("[subs] Mouse input report: 1 subscriber(s)");
        tracker.Apply("[hook] keyboard=0x123 (err 0), mouse=0x456 (err 0)");
        tracker.Apply("[host] -> this PC (input stays local)");
        switch (fixture)
        {
            case "controlling": tracker.Apply("[host] -> QA Device {0} (pointer interval 15 ms)"); break;
            case "advertising-failed": tracker.Apply("[adv ] status -> Aborted (error: OtherError)"); break;
            case "capture-failed": tracker.Apply("capture failed: isolated test"); break;
            case "fallback-lost":
                tracker.Apply("[ready] existing HID connection verified; input stays local");
                tracker.Apply("[ready] existing HID connection lost; input returned to this PC");
                break;
        }
    }

    private static void CheckExceptions(Action<bool, string> check)
    {
        UiText.SelectLanguage("id");
        var pointer = UiText.TagException(new ArgumentException(UiText.T("Pointer.InvalidSensitivity")), "Pointer.InvalidSensitivity");
        var storage = UiText.TagException(new InvalidOperationException(UiText.T("Storage.UserDataUnavailable")), "Storage.UserDataUnavailable");
        var component = UiText.TagException(new IOException(UiText.T("Engine.MissingComponent", "QA {0}")), "Engine.MissingComponent", "QA {0}");
        var messages = new[] { pointer.Message, storage.Message, component.Message };
        UiText.SelectLanguage("en");
        check(UiText.ResolveException(pointer) == UiText.T("Pointer.InvalidSensitivity") && pointer.Message == messages[0],
            "Pointer errors re-render in English without rewriting the original exception message");
        check(UiText.ResolveException(storage) == UiText.T("Storage.UserDataUnavailable") && storage.Message == messages[1],
            "Storage errors re-render in English without losing the original exception message");
        check(UiText.ResolveException(component).Contains("QA {0}", StringComparison.Ordinal)
            && component.Message == messages[2] && UiText.ResolveException(new IOException("raw upstream error")) == "raw upstream error",
            "Tagged error arguments survive language changes while raw upstream errors remain verbatim");
    }

    private static void CheckWindow(Action<bool, string> check, string directory)
    {
        var languagePath = Path.Combine(directory, "window-language.json");
        var pointerPath = Path.Combine(directory, "pointer-settings.json");
        PointerSettings.Save(pointerPath, 0.8, 270);
        var pointerBytes = File.ReadAllBytes(pointerPath);
        UiText.SelectLanguage("id");
        var preview = NewWindow();
        try
        {
            preview.Show();
            preview.LanguageCombo.SelectedValue = "en";
            Settle(preview);
            check(UiText.CurrentLanguage == "en" && preview.LanguageStatus.Text == UiText.T("Language.Preview")
                && !File.Exists(languagePath),
                "Default preview changes UI language without persisting preferences");
        }
        finally { preview.Close(); }

        check(Throws<ArgumentException>(() => _ = new MainWindow(preview: true,
            languageSettingsPath: Path.Combine(UserStorage.Root, "data", "ui-settings.json"))),
            "Preview persistence refuses the normal application language file");
        check(Throws<ArgumentException>(() => _ = new MainWindow(preview: true,
            languageSettingsPath: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "iDock", "data", "ui-settings.json"))),
            "Preview persistence refuses the installed user's language file");

        var window = NewWindow(pointerPath, languagePath);
        try
        {
            window.Show();
            Settle(window);
            var combo = window.LanguageCombo;
            var scroll = (ScrollViewer)window.FindName("WorkspaceScrollViewer");
            check(UiText.CurrentLanguage == "id" && (string)combo.SelectedValue == "id" && !File.Exists(languagePath),
                "An isolated missing preference starts the actual selector in Indonesian without writing");
            check(combo.SelectedValuePath == "Tag" && combo.Items.Cast<ComboBoxItem>()
                .Select(item => item.Tag as string).SequenceEqual(new[] { "id", "en" })
                && combo.Items.Cast<ComboBoxItem>().Select(item => item.Content as string)
                    .SequenceEqual(new[] { "Bahasa Indonesia", "English" }),
                "The language selector uses plain language names with stable id/en storage tags");
            combo.ApplyTemplate();
            check(combo.Focusable && combo.IsTabStop && combo.Template.FindName("PART_Popup", combo) is Popup,
                "The language selector retains keyboard focus and its native popup part");
            window.SensitivitySlider.Value = 0.8;
            window.OrientationCombo.SelectedValue = "270";
            window.LogBox.Text = "Raw BLE log fixture; do not translate or replay.";
            var rawLog = window.LogBox.Text;
            var buttons = new[] { window.MirrorButton, window.ControlButton, window.DiagnoseButton, window.StopButton };
            var buttonStates = buttons.Select(button => button.IsEnabled).ToArray();
            SeedState(window.ControlState, "controlling");
            window.UpdateControlStatus();
            foreach (var language in new[] { "en", "id" })
            {
                combo.SelectedValue = language;
                window.Width = 900;
                window.Height = 680;
                scroll.ScrollToTop();
                Settle(window);
                check((string)window.MirrorButton.Content == UiText.ForLanguage("Mirror.Open", language)
                    && (string)window.ControlButton.Content == UiText.ForLanguage("Control.Enable", language)
                    && (string)window.StopButton.Content == UiText.ForLanguage("Session.Stop", language)
                    && (string)window.FindResource("Text.Guide.Title") == UiText.ForLanguage("Guide.Title", language),
                    $"Live {language} selection updates real button and guide resources");
                check(AutomationProperties.GetName(combo) == UiText.ForLanguage("Language.Accessible", language)
                    && AutomationProperties.GetName(window.SensitivitySlider) == UiText.ForLanguage("Sensitivity.Accessible", language)
                    && AutomationProperties.GetName(window.OrientationCombo) == UiText.ForLanguage("Orientation.Accessible", language),
                    $"Live {language} selection updates language and pointer accessibility labels");
                check(window.ControlState.State == ControlConnectionState.Controlling
                    && window.ControlStatus.Text == window.ControlState.DisplayText
                    && window.ControlStatus.Text.Contains("QA Device {0}", StringComparison.Ordinal),
                    $"Live {language} selection preserves the tracked remote target without replaying BLE logs");
                check(window.SensitivitySlider.Value == 0.8 && (string)window.OrientationCombo.SelectedValue == "270"
                    && File.ReadAllBytes(pointerPath).SequenceEqual(pointerBytes),
                    $"Live {language} selection leaves pointer values and their saved file unchanged");
                check(window.LogBox.Text == rawLog && buttons.Select(button => button.IsEnabled).SequenceEqual(buttonStates),
                    $"Live {language} selection leaves existing raw logs and session button state unchanged");
                check(scroll.ViewportWidth > 0 && scroll.ViewportHeight > 0
                    && scroll.HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled
                    && scroll.ExtentWidth <= scroll.ViewportWidth + 1,
                    $"The {language} workspace fits the minimum 900x680 window without horizontal scrolling");
                check(new FrameworkElement[] { combo, window.OrientationCombo, window.SensitivitySlider,
                    window.MirrorButton, window.ControlButton, window.ControlStatus, window.LanguageStatus }
                    .All(control => control.ActualWidth > 0 && control.ActualHeight > 0 && FitsHorizontally(control, scroll)),
                    $"Language, pointer and status controls fit horizontally in the {language} minimum layout");
                check(LanguageSettings.Load(languagePath) == language && window.LanguageStatus.Text == UiText.T("Language.Saved"),
                    $"The actual {language} selector persists only the isolated language preference");
            }

            SeedState(window.ControlState, "capture-failed");
            window.UpdateControlStatus();
            var indonesianFailure = window.ControlStatus.Text;
            combo.SelectedValue = "en";
            Settle(window);
            check(window.ControlState.State == ControlConnectionState.Failed
                && window.ControlStatus.Text == UiText.T("Control.FailureInput") && window.ControlStatus.Text != indonesianFailure,
                "Changing language re-renders the existing failure without resetting session state");
            check(window.OrientationCombo.Items.Cast<ComboBoxItem>().Select(item => item.Tag as string)
                .SequenceEqual(new[] { "0", "90", "270", "180" })
                && ((ComboBoxItem)window.OrientationCombo.SelectedItem).Content as string == UiText.T("Orientation.Right"),
                "Translated orientation labels preserve all backend rotation tags and the current choice");
        }
        finally { window.Close(); }

        var restarted = NewWindow(pointerPath, languagePath);
        try
        {
            restarted.Show();
            Settle(restarted);
            check(UiText.CurrentLanguage == "en" && (string)restarted.LanguageCombo.SelectedValue == "en"
                && (string)restarted.MirrorButton.Content == "Open mirroring",
                "A new isolated window restores the saved English language before rendering");
        }
        finally { restarted.Close(); }

        const string invalidJson = "{incomplete-language";
        File.WriteAllText(languagePath, invalidJson);
        var corrupt = NewWindow(pointerPath, languagePath);
        try
        {
            corrupt.Show();
            Settle(corrupt);
            check(UiText.CurrentLanguage == "id" && (string)corrupt.LanguageCombo.SelectedValue == "id"
                && corrupt.LanguageStatus.Text.StartsWith(UiText.T("Language.LoadFailed", ""), StringComparison.Ordinal)
                && File.ReadAllText(languagePath) == invalidJson,
                "Malformed saved language falls back visibly without crashing or overwriting the file");
            corrupt.LanguageCombo.SelectedValue = "en";
            check(LanguageSettings.Load(languagePath) == "en" && corrupt.LanguageStatus.Text == UiText.T("Language.Saved"),
                "An explicit selector change can recover malformed isolated language settings");
        }
        finally { corrupt.Close(); }

        LanguageSettings.Save(languagePath, "id");
        File.SetAttributes(languagePath, File.GetAttributes(languagePath) | FileAttributes.ReadOnly);
        MainWindow? readOnly = null;
        try
        {
            readOnly = NewWindow(pointerPath, languagePath);
            readOnly.Show();
            readOnly.LanguageCombo.SelectedValue = "en";
            Settle(readOnly);
            check(UiText.CurrentLanguage == "en" && (string)readOnly.MirrorButton.Content == "Open mirroring"
                && readOnly.LanguageStatus.Text.StartsWith(UiText.T("Language.SaveFailed", ""), StringComparison.Ordinal)
                && LanguageSettings.Load(languagePath) == "id",
                "A read-only preference shows a save warning while retaining live English and the previous file");
            check(File.ReadAllBytes(pointerPath).SequenceEqual(pointerBytes)
                && Directory.GetFiles(directory, ".ui-settings-*.tmp").Length == 0,
                "Failed UI language persistence leaves pointer settings intact and no temporary files");
        }
        finally
        {
            readOnly?.Close();
            File.SetAttributes(languagePath, File.GetAttributes(languagePath) & ~FileAttributes.ReadOnly);
        }
        using (var lockedFile = new FileStream(languagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var inaccessible = NewWindow(pointerPath, languagePath);
            try
            {
                inaccessible.Show();
                Settle(inaccessible);
                check(UiText.CurrentLanguage == "id"
                    && inaccessible.LanguageStatus.Text.StartsWith(UiText.T("Language.LoadFailed", ""), StringComparison.Ordinal),
                    "An inaccessible language file falls back visibly without crashing the window");
            }
            finally { inaccessible.Close(); }
        }
        check(File.ReadAllBytes(pointerPath).SequenceEqual(pointerBytes),
            "Language preview, restart and fallback tests never alter pointer persistence");
    }

    private static MainWindow NewWindow(string? pointerPath = null, string? languagePath = null) =>
        new(preview: true, settingsPath: pointerPath, languageSettingsPath: languagePath)
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -20000, Top = -20000, ShowActivated = false, ShowInTaskbar = false
        };

    private static void Settle(Window window)
    {
        window.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
        window.UpdateLayout();
    }

    private static bool FitsHorizontally(FrameworkElement element, FrameworkElement ancestor)
    {
        var bounds = element.TransformToAncestor(ancestor).TransformBounds(new Rect(element.RenderSize));
        return bounds.Left >= -1 && bounds.Right <= ancestor.ActualWidth + 1;
    }

    private static string[] Signature(string text) => Placeholder.Matches(text).Select(match => match.Value)
        .OrderBy(value => value, StringComparer.Ordinal).ToArray();

    private static bool Formats(string text)
    {
        var matches = Placeholder.Matches(text);
        var count = matches.Count == 0 ? 0 : matches.Max(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)) + 1;
        try { _ = string.Format(CultureInfo.InvariantCulture, text, Enumerable.Range(0, count).Cast<object>().ToArray()); return true; }
        catch (FormatException) { return false; }
    }

    private static bool Throws<T>(Action action) where T : Exception
    {
        try { action(); return false; }
        catch (T) { return true; }
    }

    private static bool ThrowsSaveFailure(Action action)
    {
        try { action(); return false; }
        catch (IOException) { return true; }
        catch (UnauthorizedAccessException) { return true; }
    }
}
