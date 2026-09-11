using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;

namespace iDock;

/// <summary>Hardware-free checks for display mode and receiver volume settings. Isolated files only.</summary>
internal static class MirrorPresentationVerification
{
    internal static void Run(Action<bool, string> check)
    {
        UiText.SelectLanguage("id");
        check(MirrorSettings.NormalizeVolume(double.NaN) == 100 && MirrorSettings.NormalizeVolume(-5) == 0
            && MirrorSettings.NormalizeVolume(250) == 100 && MirrorSettings.NormalizeVolume(49.6) == 50,
            "Receiver volume normalizes to whole percentages between 0 and 100");
        check(MirrorSettings.TryParseMode("default", out var parsedDefault) && parsedDefault == MirrorWindowMode.Default
            && MirrorSettings.TryParseMode("windowed", out var parsedWindowed) && parsedWindowed == MirrorWindowMode.Windowed
            && MirrorSettings.TryParseMode("fullscreen", out var parsedFullscreen) && parsedFullscreen == MirrorWindowMode.Fullscreen
            && !MirrorSettings.TryParseMode("Fullscreen", out _) && !MirrorSettings.TryParseMode(null, out _)
            && !MirrorSettings.TryParseMode("maximized", out _),
            "Display modes accept only the three lowercase storage tags");

        var directory = Path.Combine(Path.GetTempPath(), "idock-mirror-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "mirror-settings.json");
        try
        {
            check(MirrorSettings.Load(path) is null && !File.Exists(path),
                "Missing mirror settings leave receiver audio untouched and create no file");
            MirrorSettings.Save(path, new(35, true, MirrorWindowMode.Fullscreen));
            check(MirrorSettings.Load(path) == new MirrorConfiguration(35, true, MirrorWindowMode.Fullscreen),
                "Volume, mute and display mode round-trip through JSON");
            MirrorSettings.Save(path, MirrorConfiguration.Default);
            check(MirrorSettings.Load(path) == MirrorConfiguration.Default && Directory.GetFiles(directory).Length == 1,
                "Atomic mirror settings update leaves no temporary files behind");
            var rejected = 0;
            var invalidDocuments = new[]
            {
                "{unfinished", "[]", "{}",
                "{\"Volume\":101,\"Muted\":false,\"WindowMode\":\"default\",\"VideoDecoder\":\"auto\"}",
                "{\"Volume\":-1,\"Muted\":false,\"WindowMode\":\"default\",\"VideoDecoder\":\"auto\"}",
                "{\"Volume\":50.5,\"Muted\":false,\"WindowMode\":\"default\",\"VideoDecoder\":\"auto\"}",
                "{\"Volume\":\"50\",\"Muted\":false,\"WindowMode\":\"default\",\"VideoDecoder\":\"auto\"}",
                "{\"Volume\":50,\"Muted\":\"no\",\"WindowMode\":\"default\",\"VideoDecoder\":\"auto\"}",
                "{\"Volume\":50,\"Muted\":false,\"WindowMode\":\"Windowed\",\"VideoDecoder\":\"auto\"}",
                "{\"Volume\":50,\"Muted\":false,\"VideoDecoder\":\"auto\"}",
                "{\"Volume\":50,\"Muted\":false,\"WindowMode\":\"default\"}",
                "{\"Volume\":50,\"Muted\":false,\"WindowMode\":\"default\",\"VideoDecoder\":\"Auto\"}",
                "{\"Volume\":50,\"Muted\":false,\"WindowMode\":\"default\",\"VideoDecoder\":\"nvdec\"}",
                "{\"Volume\":50,\"Muted\":false,\"WindowMode\":\"default\",\"VideoDecoder\":\"auto\",\"Padding\":\"" + new string('x', 4200) + "\"}"
            };
            foreach (var invalid in invalidDocuments)
            {
                File.WriteAllText(path, invalid);
                try { MirrorSettings.Load(path); }
                catch (JsonException) { if (File.ReadAllText(path) == invalid) rejected++; }
            }
            check(rejected == invalidDocuments.Length,
                "Malformed, out-of-range or oversized mirror settings are rejected without rewriting the file");
            MirrorSettings.Save(path, new(60, false, MirrorWindowMode.Windowed));
            var before = File.ReadAllText(path);
            check(Throws<ArgumentOutOfRangeException>(() => MirrorSettings.Save(path, new(101, false, MirrorWindowMode.Default)))
                && File.ReadAllText(path) == before,
                "An out-of-range volume cannot overwrite a valid mirror preference");

            var work = new LayoutRectangle(0, 0, 1920, 1040);
            var frame = new FrameMargins(8, 31, 8, 8);
            var portrait = MirrorWindowLayout.ComputeWindowed(work, 828, 1792, frame);
            var portraitClientWidth = portrait.Width - frame.Left - frame.Right;
            var portraitClientHeight = portrait.Height - frame.Top - frame.Bottom;
            check(portraitClientHeight == 845 && Math.Abs(portraitClientWidth / (double)portraitClientHeight - 828.0 / 1792) < 0.01,
                "Windowed portrait layout keeps the device aspect ratio within 85 percent of the work area height");
            check(portrait.Left == (work.Width - portrait.Width) / 2 && portrait.Top == (work.Height - portrait.Height) / 2,
                "Windowed layout is centered on the monitor work area");
            var landscape = MirrorWindowLayout.ComputeWindowed(work, 1792, 828, frame);
            check(landscape.Width - frame.Left - frame.Right == 1616
                && Math.Abs((landscape.Width - 16) / (double)(landscape.Height - 39) - 1792.0 / 828) < 0.01,
                "Windowed landscape layout is limited by the work area width while keeping the aspect ratio");
            var offsetWork = new LayoutRectangle(100, 50, 1380, 770);
            var offset = MirrorWindowLayout.ComputeWindowed(offsetWork, 828, 1792, frame);
            check(offset.Left >= offsetWork.Left && offset.Right <= offsetWork.Right
                && offset.Top >= offsetWork.Top && offset.Bottom <= offsetWork.Bottom,
                "Windowed layout stays inside a secondary monitor's offset work area");
            var monitor = new LayoutRectangle(-2560, 0, 0, 1440);
            check(MirrorWindowLayout.ComputeFullscreen(monitor) == monitor,
                "Fullscreen layout covers exactly the monitor that holds the video window");
            check(Throws<ArgumentOutOfRangeException>(() => MirrorWindowLayout.ComputeWindowed(work, 10, 10, frame))
                && Throws<ArgumentOutOfRangeException>(() => MirrorWindowLayout.ComputeWindowed(new(0, 0, 0, 0), 828, 1792, frame))
                && Throws<ArgumentOutOfRangeException>(() => MirrorWindowLayout.ComputeFullscreen(new(5, 5, 5, 5))),
                "Unusable stream sizes and empty monitors are rejected instead of producing a zero-size window");
            check(!MirrorWindowLayout.IsUsableStreamSize(0, 100) && !MirrorWindowLayout.IsUsableStreamSize(63, 64)
                && MirrorWindowLayout.IsUsableStreamSize(64, 64) && !MirrorWindowLayout.IsUsableStreamSize(20000, 64),
                "Transient zero-size renderer windows are never used as a stream aspect source");
            var positioner = new MirrorWindowPositioner();
            var deadWindow = new ReceiverWindow(0, 42, "GSTD3D11", "AirPlay Video Stream", true, false);
            check(positioner.Apply([], MirrorWindowMode.Fullscreen, true) == 0
                && positioner.Apply([deadWindow], MirrorWindowMode.Windowed, true) == 0 && positioner.TrackedCount == 0,
                "Applying a display mode with no live owned video window changes nothing");

            var audio = MirrorAudioControl.Apply(_ => false, 50, true);
            check(audio.Matched == 0 && audio.Changed == 0,
                "Read-only audio session enumeration completes without touching sessions this session does not own");

            check(Throws<ArgumentException>(() => _ = new MainWindow(preview: true,
                mirrorSettingsPath: Path.Combine(UserStorage.Root, "data", "mirror-settings.json"))),
                "Preview persistence refuses the normal application mirror settings file");

            var windowPath = Path.Combine(directory, "window-mirror.json");
            var window = NewWindow(windowPath);
            try
            {
                window.Show();
                Settle(window);
                var combo = window.DisplayModeCombo;
                var slider = window.VolumeSlider;
                var mute = window.MuteButton;
                var scroll = (ScrollViewer)window.FindName("WorkspaceScrollViewer");
                check(combo.SelectedValuePath == "Tag" && combo.Items.Cast<ComboBoxItem>().Select(item => item.Tag as string)
                        .SequenceEqual(new[] { "default", "windowed", "fullscreen" }) && (string)combo.SelectedValue == "default",
                    "The display selector offers leave-alone, windowed and fullscreen with leave-alone as the default");
                check(slider.Minimum == 0 && slider.Maximum == 100 && slider.Value == 100 && slider.SmallChange == 1
                    && slider.LargeChange == 10 && slider.IsSnapToTickEnabled && window.VolumeValue.Text == "100%",
                    "The volume slider covers 0 to 100 percent and starts at full volume");
                check(combo.Focusable && combo.IsTabStop && slider.Focusable && slider.IsTabStop && mute.Focusable && mute.IsTabStop
                    && window.ReapplyDisplayButton.Focusable && !window.ReapplyDisplayButton.IsEnabled,
                    "Display and audio controls are keyboard reachable and reapply waits for a running receiver");
                check(!string.IsNullOrWhiteSpace(AutomationProperties.GetName(combo))
                    && !string.IsNullOrWhiteSpace(AutomationProperties.GetName(slider))
                    && AutomationProperties.GetName(mute) == UiText.T("Audio.MuteAccessible"),
                    "Display and audio controls carry accessibility names");
                check(!File.Exists(windowPath) && window.AudioStatus.Text == UiText.T("Audio.AutoSaved"),
                    "Opening the window never writes mirror settings before the user changes something");
                window.Width = 900;
                window.Height = 680;
                scroll.ScrollToTop();
                Settle(window);
                check(new FrameworkElement[] { combo, slider, mute, window.ReapplyDisplayButton, window.DisplayStatus, window.AudioStatus }
                    .All(control => control.ActualWidth > 0 && control.ActualHeight > 0 && FitsHorizontally(control, scroll)),
                    "Display and audio controls fit the minimum 900x680 window without horizontal clipping");

                slider.Value = 40;
                check(window.VolumeValue.Text == "40%" && window.AudioStatus.Text == UiText.T("Audio.Saving"),
                    "Moving the volume slider updates the label immediately and reports a pending save");
                var autosaveFrame = new DispatcherFrame();
                var deadline = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) };
                deadline.Tick += (_, _) => { deadline.Stop(); autosaveFrame.Continue = false; };
                deadline.Start();
                Dispatcher.PushFrame(autosaveFrame);
                check(MirrorSettings.Load(windowPath)?.Volume == 40 && window.AudioStatus.Text == UiText.T("Audio.Saved"),
                    "The volume slider autosaves through a debounce without Reset or closing");
                mute.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                check(MirrorSettings.Load(windowPath) == new MirrorConfiguration(40, true, MirrorWindowMode.Default)
                    && (string)mute.Content == UiText.T("Audio.Unmute") && slider.Value == 40,
                    "The mute button toggles and saves immediately without moving the slider");
                combo.SelectedValue = "fullscreen";
                check(MirrorSettings.Load(windowPath) == new MirrorConfiguration(40, true, MirrorWindowMode.Fullscreen)
                    && window.DisplayStatus.Text == UiText.T("Display.Saved"),
                    "Changing the display mode saves immediately while preserving volume and mute");
                var savedBytes = File.ReadAllBytes(windowPath);
                window.LanguageCombo.SelectedValue = "en";
                Settle(window);
                check(UiText.CurrentLanguage == "en" && (string)mute.Content == UiText.ForLanguage("Audio.Unmute", "en")
                    && ((ComboBoxItem)combo.SelectedItem).Content as string == UiText.ForLanguage("Display.Fullscreen", "en")
                    && window.DisplayStatus.Text == UiText.ForLanguage("Display.Saved", "en")
                    && File.ReadAllBytes(windowPath).SequenceEqual(savedBytes),
                    "A live language change re-renders mute, display and status text without touching the saved file");
                window.LanguageCombo.SelectedValue = "id";
                Settle(window);
            }
            finally { window.Close(); }
            UiText.SelectLanguage("id");

            var restarted = NewWindow(windowPath);
            try
            {
                restarted.Show();
                Settle(restarted);
                check(restarted.VolumeSlider.Value == 40 && (string)restarted.MuteButton.Content == UiText.T("Audio.Unmute")
                    && (string)restarted.DisplayModeCombo.SelectedValue == "fullscreen",
                    "A new isolated window restores the saved volume, mute and display mode");
            }
            finally { restarted.Close(); }

            const string invalidJson = "{incomplete-mirror";
            File.WriteAllText(windowPath, invalidJson);
            var corrupt = NewWindow(windowPath);
            try
            {
                corrupt.Show();
                Settle(corrupt);
                check(corrupt.VolumeSlider.Value == 100 && (string)corrupt.DisplayModeCombo.SelectedValue == "default"
                    && corrupt.AudioStatus.Text.StartsWith(UiText.T("MirrorSettings.LoadFailed", ""), StringComparison.Ordinal)
                    && File.ReadAllText(windowPath) == invalidJson,
                    "Malformed saved mirror settings fall back visibly without crashing or overwriting the file");
                corrupt.DisplayModeCombo.SelectedValue = "windowed";
                check(MirrorSettings.Load(windowPath)?.WindowMode == MirrorWindowMode.Windowed,
                    "An explicit selector change recovers malformed mirror settings");
            }
            finally { corrupt.Close(); }
        }
        finally
        {
            UiText.SelectLanguage("id");
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static MainWindow NewWindow(string mirrorPath) =>
        new(preview: true, mirrorSettingsPath: mirrorPath)
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

    private static bool Throws<T>(Action action) where T : Exception
    {
        try { action(); return false; }
        catch (T) { return true; }
    }
}
