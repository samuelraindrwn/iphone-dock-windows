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
            var pinnedConfiguration = new MirrorConfiguration(35, true, MirrorWindowMode.Fullscreen,
                VideoDecoderMode.Software, true);
            MirrorSettings.Save(path, pinnedConfiguration);
            check(MirrorSettings.Load(path) == pinnedConfiguration,
                "Volume, mute, display mode, decoder and pin state round-trip through JSON");
            const string legacySettings = "{\"Volume\":35,\"Muted\":true,\"WindowMode\":\"fullscreen\",\"VideoDecoder\":\"software\"}";
            File.WriteAllText(path, legacySettings);
            check(MirrorSettings.Load(path) == pinnedConfiguration with { Pinned = false }
                && File.ReadAllText(path) == legacySettings,
                "A pre-pin mirror settings file migrates to unpinned in memory without being rewritten");
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
                "{\"Volume\":50,\"Muted\":false,\"WindowMode\":\"default\",\"VideoDecoder\":\"auto\",\"Pinned\":null}",
                "{\"Volume\":50,\"Muted\":false,\"WindowMode\":\"default\",\"VideoDecoder\":\"auto\",\"Pinned\":\"yes\"}",
                "{\"Volume\":50,\"Muted\":false,\"WindowMode\":\"default\",\"VideoDecoder\":\"auto\",\"Pinned\":1}",
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
            check(MirrorWindowLayout.Shape(828, 1792) == MirrorStreamShape.Portrait
                && MirrorWindowLayout.Shape(1792, 828) == MirrorStreamShape.Landscape
                && MirrorWindowLayout.Shape(1000, 1000) == MirrorStreamShape.Unknown,
                "Stream shape classification distinguishes portrait and landscape without guessing square input");
            VerifyStreamGeometry(directory, check);
            var positioner = new MirrorWindowPositioner();
            var deadWindow = new ReceiverWindow(0, 42, "GSTD3D11", "AirPlay Video Stream", true, false);
            check(positioner.Apply([], MirrorWindowMode.Fullscreen, false, true, false) == 0
                && positioner.Apply([deadWindow], MirrorWindowMode.Windowed, true, true, true) == 0
                && positioner.TrackedCount == 0,
                "Applying a display mode with no live owned video window changes nothing");

            VerifyPinning(check);
            VerifyAutomaticOrientation(check);

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
                var pin = window.PinDisplayCheckBox;
                var scroll = (ScrollViewer)window.FindName("WorkspaceScrollViewer");
                check(combo.SelectedValuePath == "Tag" && combo.Items.Cast<ComboBoxItem>().Select(item => item.Tag as string)
                        .SequenceEqual(new[] { "default", "windowed", "fullscreen" }) && (string)combo.SelectedValue == "default",
                    "The display selector offers leave-alone, windowed and fullscreen with leave-alone as the default");
                check(slider.Minimum == 0 && slider.Maximum == 100 && slider.Value == 100 && slider.SmallChange == 1
                    && slider.LargeChange == 10 && slider.IsSnapToTickEnabled && window.VolumeValue.Text == "100%",
                    "The volume slider covers 0 to 100 percent and starts at full volume");
                check(pin.IsChecked == false && pin.Focusable && pin.IsTabStop
                    && (string)pin.Content == UiText.T("Display.Pin"),
                    "The video pin checkbox starts off, is keyboard reachable and uses the current language");
                check(combo.Focusable && combo.IsTabStop && slider.Focusable && slider.IsTabStop && mute.Focusable && mute.IsTabStop
                    && window.ReapplyDisplayButton.Focusable && !window.ReapplyDisplayButton.IsEnabled,
                    "Display and audio controls are keyboard reachable and reapply waits for a running receiver");
                check(!string.IsNullOrWhiteSpace(AutomationProperties.GetName(combo))
                    && !string.IsNullOrWhiteSpace(AutomationProperties.GetName(slider))
                    && AutomationProperties.GetName(mute) == UiText.T("Audio.MuteAccessible")
                    && AutomationProperties.GetName(pin) == UiText.T("Display.PinAccessible"),
                    "Display and audio controls carry accessibility names");
                check(!File.Exists(windowPath) && window.AudioStatus.Text == UiText.T("Audio.AutoSaved"),
                    "Opening the window never writes mirror settings before the user changes something");
                window.Width = 900;
                window.Height = 680;
                scroll.ScrollToTop();
                Settle(window);
                check(new FrameworkElement[] { combo, pin, slider, mute, window.ReapplyDisplayButton, window.DisplayStatus, window.AudioStatus }
                    .All(control => control.ActualWidth > 0 && control.ActualHeight > 0 && FitsHorizontally(control, scroll)),
                    "Display and audio controls fit the minimum 900x680 window without horizontal clipping");

                pin.IsChecked = true;
                check(MirrorSettings.Load(windowPath) == new MirrorConfiguration(100, false,
                        MirrorWindowMode.Default, VideoDecoderMode.Auto, true)
                    && window.DisplayStatus.Text == UiText.T("Display.Saved"),
                    "Checking video pin saves immediately without requiring a running receiver");

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
                check(MirrorSettings.Load(windowPath) == new MirrorConfiguration(40, true, MirrorWindowMode.Default,
                        VideoDecoderMode.Auto, true)
                    && (string)mute.Content == UiText.T("Audio.Unmute") && slider.Value == 40,
                    "The mute button toggles and saves immediately without moving the slider or clearing pin");
                combo.SelectedValue = "fullscreen";
                check(MirrorSettings.Load(windowPath) == new MirrorConfiguration(40, true, MirrorWindowMode.Fullscreen,
                        VideoDecoderMode.Auto, true)
                    && window.DisplayStatus.Text == UiText.T("Display.Saved"),
                    "Changing the display mode saves immediately while preserving volume, mute and pin");
                var savedBytes = File.ReadAllBytes(windowPath);
                window.LanguageCombo.SelectedValue = "en";
                Settle(window);
                check(UiText.CurrentLanguage == "en" && (string)mute.Content == UiText.ForLanguage("Audio.Unmute", "en")
                    && ((ComboBoxItem)combo.SelectedItem).Content as string == UiText.ForLanguage("Display.Fullscreen", "en")
                    && pin.IsChecked == true && (string)pin.Content == UiText.ForLanguage("Display.Pin", "en")
                    && AutomationProperties.GetName(pin) == UiText.ForLanguage("Display.PinAccessible", "en")
                    && window.DisplayStatus.Text == UiText.ForLanguage("Display.Saved", "en")
                    && File.ReadAllBytes(windowPath).SequenceEqual(savedBytes),
                    "A live language change re-renders pin, mute, display and status text without touching saved state");
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
                    && (string)restarted.DisplayModeCombo.SelectedValue == "fullscreen"
                    && restarted.PinDisplayCheckBox.IsChecked == true && restarted.MirrorState.Pinned,
                    "A new isolated window restores the saved volume, mute, display mode and pin state");
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
                    && corrupt.PinDisplayCheckBox.IsChecked == false
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

    private static void VerifyStreamGeometry(string directory, Action<bool, string> check)
    {
        const string landscapeLine = "0:00:01.000 DEBUG d3d12videosink gstd3d12videosink.cpp:1079:gst_d3d12_video_sink_set_info:<sink> set caps video/x-raw, format=(string)NV12, width=(int)1792, height=(int)828, framerate=(fraction)60/1";
        const string d3d11PortraitLine = "0:00:02.000 DEBUG d3d11videosink gstd3d11videosink.cpp:962:gst_d3d11_video_sink_set_caps:<sink> set caps video/x-raw, height=(int)1792, width=(int)828";
        const string d3d12PortraitLine = "0:00:03.000 DEBUG d3d12videosink gstd3d12videosink.cpp:1079:gst_d3d12_video_sink_set_info:<sink> set caps video/x-raw(memory:D3D12Memory), width=(int)828, height=(int)1792";
        check(MirrorStreamGeometrySource.TryParseCapsLine(landscapeLine, out var landscape)
            && landscape == new MirrorStreamGeometry(MirrorRendererKind.D3D12, new(1792, 828))
            && MirrorStreamGeometrySource.TryParseCapsLine(d3d11PortraitLine, out var portrait)
            && portrait == new MirrorStreamGeometry(MirrorRendererKind.D3D11, new(828, 1792)),
            "D3D11 and D3D12 negotiated-caps lines expose exact landscape and portrait stream geometry");
        check(!MirrorStreamGeometrySource.TryParseCapsLine(
                "d3d12videosink scaling to 828x1792", out _)
            && !MirrorStreamGeometrySource.TryParseCapsLine(
                "other set caps video/x-raw, width=(int)828, height=(int)1792", out _)
            && !MirrorStreamGeometrySource.TryParseCapsLine(
                "d3d12videosink gst_d3d12_video_sink_set_info set caps video/x-h264, width=(int)828, height=(int)1792", out _)
            && !MirrorStreamGeometrySource.TryParseCapsLine(
                "d3d12videosink gst_d3d12_video_sink_set_info set caps video/x-raw, width=(int)50000, height=(int)1792", out _),
            "Caps telemetry rejects summaries, wrong functions/media and dimensions outside the bounded stream range");

        var logPath = Path.Combine(directory, "isolated-gstreamer-caps.log");
        File.WriteAllText(logPath, landscapeLine + Environment.NewLine);
        using (var source = new MirrorStreamGeometrySource(logPath))
        {
            var staged = source.ReadLatest(TimeSpan.Zero);
            var tooEarly = source.ReadLatest(MirrorStreamGeometrySource.SettleDelay - TimeSpan.FromMilliseconds(1));
            var first = source.ReadLatest(MirrorStreamGeometrySource.SettleDelay);
            File.AppendAllText(logPath, d3d12PortraitLine);
            var beforeNewline = source.ReadLatest(TimeSpan.FromSeconds(1));
            File.AppendAllText(logPath, Environment.NewLine);
            var newlyStaged = source.ReadLatest(TimeSpan.FromSeconds(1.1));
            var afterSettle = source.ReadLatest(TimeSpan.FromSeconds(1.1) + MirrorStreamGeometrySource.SettleDelay);
            check(staged.D3D12 is null && tooEarly.D3D12 is null && first.D3D12 == new MirrorStreamSize(1792, 828)
                && beforeNewline == first && newlyStaged == first
                && afterSettle.D3D12 == new MirrorStreamSize(828, 1792),
                "Incremental telemetry waits for complete, quiet caps before publishing a coalesced geometry");
        }
        check(!File.Exists(logPath), "Per-session caps telemetry is deleted when its reader is disposed");

        var info = EngineManager.StartInfo("test.exe");
        info.Environment.Remove("GST_DEBUG");
        info.Environment.Remove("GST_DEBUG_FILE");
        using var configured = MirrorStreamGeometrySource.TryConfigure(info);
        check(configured is not null && info.Environment["GST_DEBUG"] == "*:0,d3d11videosink:5,d3d12videosink:5"
            && !string.IsNullOrWhiteSpace(info.Environment["GST_DEBUG_FILE"])
            && info.Environment["GST_DEBUG_COLOR_MODE"] == "off",
            "Receiver telemetry enables only the two renderer categories in an isolated color-free log");
        var custom = EngineManager.StartInfo("test.exe");
        custom.Environment["GST_DEBUG"] = "*:6";
        var existingFile = custom.Environment.TryGetValue("GST_DEBUG_FILE", out var inheritedFile) ? inheritedFile : null;
        check(MirrorStreamGeometrySource.TryConfigure(custom) is null && custom.Environment["GST_DEBUG"] == "*:6"
            && (!custom.Environment.TryGetValue("GST_DEBUG_FILE", out var afterFile) || afterFile == existingFile),
            "An inherited GStreamer diagnostic setup is never redirected or broadened for orientation telemetry");
    }

    private static void VerifyAutomaticOrientation(Action<bool, string> check)
    {
        static ReceiverWindow Video(nint handle) =>
            new(handle, 42, "GstD3D12Hwnd", "AirPlay Video Stream", true, false);
        static MirrorStreamGeometrySnapshot D3D12(int width, int height) =>
            new(null, new MirrorStreamSize(width, height));

        var operations = new FakeMirrorWindowOperations();
        operations.Add(501, 42);
        operations.SetClientSize(501, 1792, 828);
        var positioner = new MirrorWindowPositioner(operations);
        var video = Video(501);
        positioner.Apply([video], MirrorWindowMode.Windowed, false, true, false, D3D12(1792, 828));
        operations.ClearCalls();
        var unchanged = positioner.Apply([video], MirrorWindowMode.Windowed, false, false, false, D3D12(1920, 1080));
        var portrait = positioner.Apply([video], MirrorWindowMode.Windowed, false, false, false, D3D12(828, 1792));
        var portraitTarget = operations.Moves.Single().Target;
        var steady = positioner.Apply([video], MirrorWindowMode.Windowed, false, false, false, D3D12(828, 1792));
        check(unchanged == 0 && portrait == 1 && steady == 0 && operations.Moves.Count == 1
            && portraitTarget.Height > portraitTarget.Width,
            "A same-HWND landscape-to-portrait caps change relayouts Windowed exactly once");

        operations.ClearCalls();
        var manual = new LayoutRectangle(300, 120, 1000, 760);
        operations.SetBounds(501, manual);
        operations.SetClientSize(501, 684, 601);
        var sameOrientation = positioner.Apply([video], MirrorWindowMode.Windowed, true, false, true, D3D12(1600, 900));
        check(sameOrientation == 1 && operations.Moves.Count == 0 && operations.StyleWrites.Count == 0
            && operations.Bounds(501) == manual && operations.TopmostCalls[^1] == (501, true),
            "Resolution and pin changes within one orientation preserve a manual Windowed size");

        operations = new FakeMirrorWindowOperations();
        operations.Add(502, 42);
        operations.SetClientSize(502, 1792, 828);
        positioner = new MirrorWindowPositioner(operations);
        video = Video(502);
        positioner.Apply([video], MirrorWindowMode.Windowed, false, true, false, D3D12(1792, 828));
        operations.SetClientSize(502, 700, 1200); // User already reshaped it before caps arrived.
        operations.ClearCalls();
        var accepted = positioner.Apply([video], MirrorWindowMode.Windowed, false, false, false, D3D12(828, 1792));
        var acceptedSteady = positioner.Apply([video], MirrorWindowMode.Windowed, false, false, false, D3D12(828, 1792));
        check(accepted == 0 && acceptedSteady == 0 && operations.Moves.Count == 0
            && operations.StyleWrites.Count == 0,
            "An existing manual window shape matching the new stream orientation is accepted without replacement");

        operations = new FakeMirrorWindowOperations();
        operations.Add(505, 42);
        operations.SetClientSize(505, 1792, 828);
        positioner = new MirrorWindowPositioner(operations);
        var d3d11Video = new ReceiverWindow(505, 42, "GSTD3D11", "AirPlay Video Stream", true, false);
        positioner.Apply([d3d11Video], MirrorWindowMode.Windowed, false, true, false, D3D12(828, 1792));
        var ignoredD3D12 = operations.Moves.Single().Target;
        operations.ClearCalls();
        positioner.Apply([d3d11Video], MirrorWindowMode.Windowed, false, false, false,
            new(new MirrorStreamSize(828, 1792), null));
        check(ignoredD3D12.Width > ignoredD3D12.Height && operations.Moves.Single().Target.Height
                > operations.Moves.Single().Target.Width,
            "D3D12 telemetry is ignored for a D3D11 window, while matching D3D11 caps drive its layout");

        operations = new FakeMirrorWindowOperations { ApplyNormalizeImmediately = false };
        operations.Add(503, 42, maximized: true);
        operations.SetClientSize(503, 1706, 937);
        positioner = new MirrorWindowPositioner(operations);
        video = Video(503);
        var requested = positioner.Apply([video], MirrorWindowMode.Windowed, false, true, false, D3D12(828, 1792));
        var stillPending = positioner.Apply([video], MirrorWindowMode.Windowed, false, false, false, D3D12(828, 1792));
        operations.SetMaximized(503, false); // Acknowledge the cross-thread ShowWindowAsync request.
        operations.SetClientSize(503, 32, 32); // Valid caps must not depend on a usable saved restore rect.
        var applied = positioner.Apply([video], MirrorWindowMode.Windowed, false, false, false, D3D12(828, 1792));
        check(requested == 0 && stillPending == 0 && applied == 1 && operations.NormalizeCalls.Count == 1
            && operations.Moves.Count == 1 && operations.Moves[0].Target.Height > operations.Moves[0].Target.Width
            && (operations.StyleWrites[0].Style & 0x01000000) == 0,
            "Selecting Windowed posts one non-activating restore, waits for acknowledgement, then applies portrait geometry");

        operations.ClearCalls();
        operations.SetMaximized(503, true); // A later user action must win over automatic orientation.
        var deferred = positioner.Apply([video], MirrorWindowMode.Windowed, false, false, false, D3D12(1792, 828));
        operations.SetMaximized(503, false);
        var afterRestore = positioner.Apply([video], MirrorWindowMode.Windowed, false, false, false, D3D12(1792, 828));
        check(deferred == 0 && afterRestore == 1 && operations.NormalizeCalls.Count == 0
            && operations.Moves.Count == 1 && operations.Moves[0].Target.Width > operations.Moves[0].Target.Height,
            "Automatic rotation waits while the user has maximized Windowed and applies once after manual restore");

        operations = new FakeMirrorWindowOperations { ApplyNormalizeImmediately = false };
        operations.Add(504, 42, maximized: true);
        positioner = new MirrorWindowPositioner(operations);
        video = Video(504);
        positioner.Apply([video], MirrorWindowMode.Windowed, false, true, false, D3D12(828, 1792));
        positioner.Apply([video], MirrorWindowMode.Default, false, true, false, D3D12(828, 1792));
        operations.SetMaximized(504, false);
        operations.ClearCalls();
        var restoredDefault = positioner.Apply([video], MirrorWindowMode.Default, false, false, false, D3D12(828, 1792));
        check(restoredDefault == 1 && operations.StyleWrites.Count == 1 && operations.Moves.Count == 0
            && operations.PlacementRestores.Count == 1 && operations.IsMaximized(504),
            "Cancelling Windowed while its async restore is pending restores the original maximized Default state");
    }

    private static void VerifyPinning(Action<bool, string> check)
    {
        static ReceiverWindow Video(nint handle, uint processId, bool visible = true, bool minimized = false) =>
            new(handle, processId, "GSTD3D11", "AirPlay Video Stream", visible, minimized);

        var operations = new FakeMirrorWindowOperations();
        operations.Add(101, 42);
        var positioner = new MirrorWindowPositioner(operations);
        var video = Video(101, 42);
        var pinned = positioner.Apply([video], MirrorWindowMode.Default, true, false, false);
        var unpinned = positioner.Apply([video], MirrorWindowMode.Default, false, false, false);
        check(pinned == 1 && unpinned == 1 && operations.TopmostCalls.Count == 2
            && operations.TopmostCalls[0] == (101, true) && operations.TopmostCalls[1] == (101, false)
            && !operations.IsTopmost(101),
            "Pin and unpin move a default renderer into and out of the topmost band");
        check(operations.StyleWrites.Count == 0 && operations.Moves.Count == 0,
            "Pinning in the default display mode never changes frame, size or position");

        operations = new FakeMirrorWindowOperations();
        operations.Add(102, 42, MirrorWindowPositioner.ExtendedStyleTopmost);
        positioner = new MirrorWindowPositioner(operations);
        video = Video(102, 42);
        positioner.Apply([video], MirrorWindowMode.Default, true, false, false);
        positioner.Apply([video], MirrorWindowMode.Default, false, false, false);
        check(operations.TopmostCalls.Count == 2 && operations.TopmostCalls.All(call => call.Topmost)
            && operations.IsTopmost(102),
            "Unpin restores a renderer that was already topmost before iDock first saw it");

        operations = new FakeMirrorWindowOperations();
        operations.Add(103, 42);
        positioner = new MirrorWindowPositioner(operations);
        video = Video(103, 42);
        positioner.Apply([video], MirrorWindowMode.Default, true, false, false);
        operations.ClearCalls();
        var acknowledged = positioner.Apply([video], MirrorWindowMode.Default, true, false, false);
        operations.SetTopmostState(103, false);
        var corrected = positioner.Apply([video], MirrorWindowMode.Default, true, false, false);
        check(acknowledged == 0 && corrected == 0 && operations.TopmostCalls.Count == 1
            && operations.TopmostCalls[0] == (103, true) && operations.IsTopmost(103),
            "A completed pin request becomes quiet, while a later lost topmost state is restored without a user-visible change");

        operations = new FakeMirrorWindowOperations { ApplyTopmostImmediately = false };
        operations.Add(105, 42);
        positioner = new MirrorWindowPositioner(operations);
        video = Video(105, 42);
        var pinRequested = positioner.Apply([video], MirrorWindowMode.Default, true, false, false);
        var pinRequestSent = operations.TopmostCalls.Count == 1 && operations.TopmostCalls[0] == (105, true);
        operations.ClearCalls();
        var pinPending = positioner.Apply([video], MirrorWindowMode.Default, true, false, false);
        operations.SetTopmostState(105, true);
        var pinAccepted = positioner.Apply([video], MirrorWindowMode.Default, true, false, false);
        operations.SetTopmostState(105, false);
        var recoveredAfterAcceptance = positioner.Apply([video], MirrorWindowMode.Default, true, false, false);
        operations.SetTopmostState(105, true);
        var recoveryAccepted = positioner.Apply([video], MirrorWindowMode.Default, true, false, false);
        var pinSteady = positioner.Apply([video], MirrorWindowMode.Default, true, false, false);
        check(pinRequested == 1 && pinRequestSent && pinPending == 0 && pinAccepted == 0
            && recoveredAfterAcceptance == 0 && recoveryAccepted == 0 && pinSteady == 0
            && operations.TopmostCalls.Count == 1 && operations.TopmostCalls[0] == (105, true),
            "A delayed asynchronous pin request is not duplicated, is acknowledged from the actual style, and can recover later loss");

        operations.ClearCalls();
        var unpinRequested = positioner.Apply([video], MirrorWindowMode.Default, false, false, false);
        var unpinRequestSent = operations.TopmostCalls.Count == 1 && operations.TopmostCalls[0] == (105, false);
        operations.ClearCalls();
        var unpinPending = positioner.Apply([video], MirrorWindowMode.Default, false, false, false);
        operations.SetTopmostState(105, false);
        var unpinAccepted = positioner.Apply([video], MirrorWindowMode.Default, false, false, false);
        var unpinSteady = positioner.Apply([video], MirrorWindowMode.Default, false, false, false);
        check(unpinRequested == 1 && unpinRequestSent && unpinPending == 0 && unpinAccepted == 0 && unpinSteady == 0
            && operations.TopmostCalls.Count == 0 && !operations.IsTopmost(105),
            "A delayed asynchronous unpin request stays pending until the actual topmost style is removed without duplicate requests");

        operations = new FakeMirrorWindowOperations { ApplyTopmostImmediately = false };
        operations.Add(106, 42);
        positioner = new MirrorWindowPositioner(operations);
        video = Video(106, 42);
        positioner.Apply([video], MirrorWindowMode.Default, true, false, false);
        operations.ClearCalls();
        var pendingPinTurnedOff = positioner.Apply([video], MirrorWindowMode.Default, false, false, false);
        check(pendingPinTurnedOff == 1 && operations.TopmostCalls.Count == 1
            && operations.TopmostCalls[0] == (106, false),
            "Turning pin off supersedes an asynchronous pin request that is still pending");

        positioner.Apply([video], MirrorWindowMode.Default, false, false, false);
        operations.SetTopmostState(106, true);
        positioner.Apply([video], MirrorWindowMode.Default, false, false, true);
        operations.ClearCalls();
        var pendingUnpinTurnedOn = positioner.Apply([video], MirrorWindowMode.Default, true, false, false);
        check(pendingUnpinTurnedOn == 1 && operations.TopmostCalls.Count == 1
            && operations.TopmostCalls[0] == (106, true),
            "Turning pin on supersedes an asynchronous unpin request that is still pending");

        operations = new FakeMirrorWindowOperations();
        operations.Add(104, 42);
        positioner = new MirrorWindowPositioner(operations);
        video = Video(104, 42);
        positioner.Apply([video], MirrorWindowMode.Default, true, false, false);
        operations.ClearCalls();
        var fullscreenPinned = positioner.Apply([video], MirrorWindowMode.Fullscreen, true, true, false);
        check(fullscreenPinned == 1 && operations.StyleWrites.Count == 1 && operations.Moves.Count == 1
            && operations.TopmostCalls.Count == 1 && operations.TopmostCalls[0] == (104, true)
            && operations.IsTopmost(104),
            "Changing display layout reapplies and preserves pinning after frame styles change");

        operations = new FakeMirrorWindowOperations();
        operations.Add(201, 42);
        positioner = new MirrorWindowPositioner(operations);
        video = Video(201, 42);
        positioner.Apply([video], MirrorWindowMode.Windowed, false, true, false);
        var manualBounds = new LayoutRectangle(321, 123, 999, 777);
        operations.SetBounds(201, manualBounds);
        var writesBeforePin = operations.StyleWrites.Count;
        var movesBeforePin = operations.Moves.Count;
        positioner.Apply([video], MirrorWindowMode.Windowed, true, false, true);
        check(operations.StyleWrites.Count == writesBeforePin && operations.Moves.Count == movesBeforePin
            && operations.Bounds(201) == manualBounds && operations.TopmostCalls[^1] == (201, true),
            "Toggling pin in a managed layout preserves a user's manual resize");

        operations = new FakeMirrorWindowOperations();
        operations.Add(202, 42);
        positioner = new MirrorWindowPositioner(operations);
        video = Video(202, 42);
        positioner.Apply([video], MirrorWindowMode.Default, true, false, false);
        var defaultModeBounds = new LayoutRectangle(240, 120, 1184, 2051);
        operations.SetBounds(202, defaultModeBounds);
        positioner.Apply([video], MirrorWindowMode.Windowed, true, true, false);
        positioner.Apply([video], MirrorWindowMode.Default, true, true, false);
        check(operations.Bounds(202) == defaultModeBounds && operations.IsTopmost(202),
            "Pinning in Default mode does not capture layout before a later manual move or resize");

        operations = new FakeMirrorWindowOperations();
        operations.Add(301, 42);
        operations.Add(302, 42);
        positioner = new MirrorWindowPositioner(operations);
        var first = Video(301, 42);
        var second = Video(302, 42);
        positioner.Apply([first, second], MirrorWindowMode.Default, true, false, false);
        operations.ClearCalls();
        var hidden = first with { Visible = false };
        var minimized = second with { Visible = false, Minimized = true };
        var hiddenChanged = positioner.Apply([hidden, minimized], MirrorWindowMode.Default, false, false, false);
        check(hiddenChanged == 2 && operations.TopmostCalls.Count == 2
            && operations.TopmostCalls.All(call => !call.Topmost) && operations.Moves.Count == 0
            && operations.StyleWrites.Count == 0,
            "Tracked hidden and minimized renderers can be unpinned without changing layout");

        operations = new FakeMirrorWindowOperations();
        operations.Add(401, 42);
        positioner = new MirrorWindowPositioner(operations);
        var mismatched = Video(401, 99);
        check(positioner.Apply([mismatched], MirrorWindowMode.Default, true, false, true) == 0
            && positioner.TrackedCount == 0 && operations.TopmostCalls.Count == 0,
            "A stale snapshot whose HWND now belongs to another process cannot be pinned");

        var original = Video(401, 42);
        positioner.Apply([original], MirrorWindowMode.Default, true, false, false);
        operations.Reassign(401, 99, MirrorWindowPositioner.ExtendedStyleTopmost);
        operations.ClearCalls();
        var replacement = Video(401, 99);
        var replacementChanged = positioner.Apply([replacement], MirrorWindowMode.Default, false, false, true);
        check(replacementChanged == 1 && positioner.TrackedCount == 1
            && operations.TopmostCalls.Count == 1 && operations.TopmostCalls[0] == (401, true)
            && operations.IsTopmost(401),
            "A recycled HWND drops stale process state and preserves the replacement's original topmost state");

        operations.SetLive(401, false);
        operations.Add(402, 99);
        operations.ClearCalls();
        var recreated = Video(402, 99);
        check(positioner.Apply([recreated], MirrorWindowMode.Default, true, false, false) == 1
            && positioner.TrackedCount == 1 && operations.TopmostCalls.Count == 1
            && operations.TopmostCalls[0] == (402, true),
            "A recreated renderer is pinned independently after dead HWND tracking is discarded");
    }

    private sealed class FakeMirrorWindowOperations : IMirrorWindowOperations
    {
        private sealed class State(uint processId, long extendedStyle)
        {
            internal bool Live = true;
            internal bool Maximized;
            internal uint ProcessId = processId;
            internal int ClientWidth = 828, ClientHeight = 1792;
            internal long Style = 0x00CF0000, ExtendedStyle = extendedStyle;
            internal LayoutRectangle Bounds = new(100, 100, 944, 1931);
            internal LayoutRectangle NormalBounds = new(100, 100, 944, 1931);
        }

        private readonly Dictionary<nint, State> windows = [];
        internal readonly List<(nint Handle, long Style, long ExtendedStyle)> StyleWrites = [];
        internal readonly List<(nint Handle, LayoutRectangle Target)> Moves = [];
        internal readonly List<(nint Handle, bool Topmost)> TopmostCalls = [];
        internal readonly List<nint> NormalizeCalls = [];
        internal readonly List<(nint Handle, WindowPlacementState Placement)> PlacementRestores = [];
        internal bool ApplyTopmostImmediately = true;
        internal bool ApplyNormalizeImmediately = true;

        internal void Add(nint handle, uint processId, long extendedStyle = 0, bool maximized = false)
        {
            windows[handle] = new(processId, extendedStyle);
            SetMaximized(handle, maximized);
        }

        internal void Reassign(nint handle, uint processId, long extendedStyle)
        {
            var state = Get(handle);
            state.Live = true;
            state.ProcessId = processId;
            state.ExtendedStyle = extendedStyle;
        }

        internal void SetLive(nint handle, bool live) => Get(handle).Live = live;
        internal void SetBounds(nint handle, LayoutRectangle bounds)
        {
            var state = Get(handle);
            state.Bounds = bounds;
            if (!state.Maximized) state.NormalBounds = bounds;
        }
        internal void SetClientSize(nint handle, int width, int height)
        { Get(handle).ClientWidth = width; Get(handle).ClientHeight = height; }
        internal void SetMaximized(nint handle, bool maximized)
        {
            var state = Get(handle);
            state.Maximized = maximized;
            if (maximized) state.Style |= 0x01000000;
            else state.Style &= ~0x01000000;
        }
        internal void SetTopmostState(nint handle, bool topmost)
        {
            if (topmost) Get(handle).ExtendedStyle |= MirrorWindowPositioner.ExtendedStyleTopmost;
            else Get(handle).ExtendedStyle &= ~MirrorWindowPositioner.ExtendedStyleTopmost;
        }
        internal bool IsTopmost(nint handle) =>
            (Get(handle).ExtendedStyle & MirrorWindowPositioner.ExtendedStyleTopmost) != 0;

        internal void ClearCalls()
        {
            StyleWrites.Clear();
            Moves.Clear();
            TopmostCalls.Clear();
            NormalizeCalls.Clear();
            PlacementRestores.Clear();
        }

        public bool IsWindow(nint handle) => windows.TryGetValue(handle, out var state) && state.Live;
        public bool BelongsToProcess(nint handle, uint processId) =>
            windows.TryGetValue(handle, out var state) && state.Live && state.ProcessId == processId;
        public (int Width, int Height) ClientSize(nint handle) => (Get(handle).ClientWidth, Get(handle).ClientHeight);
        public LayoutRectangle Bounds(nint handle) => Get(handle).Bounds;
        public long Style(nint handle) => Get(handle).Style;
        public long ExtendedStyle(nint handle) => Get(handle).ExtendedStyle;
        public bool IsMaximized(nint handle) => Get(handle).Maximized;
        public WindowPlacementState Placement(nint handle)
        {
            var state = Get(handle);
            return new(0, state.Maximized ? 3u : 1u, new(-1, -1), new(-1, -1), state.NormalBounds);
        }
        public void Normalize(nint handle)
        {
            if (ApplyNormalizeImmediately) SetMaximized(handle, false);
            NormalizeCalls.Add(handle);
        }
        public void RestorePlacement(nint handle, WindowPlacementState placement)
        {
            var state = Get(handle);
            state.NormalBounds = placement.NormalPosition;
            state.Maximized = placement.ShowCommand == 3;
            if (state.Maximized)
            {
                state.Style |= 0x01000000;
                state.Bounds = new(0, 0, 1920, 1080);
            }
            else
            {
                state.Style &= ~0x01000000;
                state.Bounds = placement.NormalPosition;
            }
            PlacementRestores.Add((handle, placement));
        }

        public void WriteStyles(nint handle, long style, long exStyle)
        {
            var state = Get(handle);
            state.Style = style;
            state.Maximized = (style & 0x01000000) != 0;
            state.ExtendedStyle = exStyle;
            StyleWrites.Add((handle, style, exStyle));
        }

        public FrameMargins Frame(long style, long exStyle) => new(8, 31, 8, 8);
        public LayoutRectangle MonitorArea(nint handle, bool workArea) =>
            workArea ? new(0, 0, 1920, 1040) : new(0, 0, 1920, 1080);

        public void Move(nint handle, LayoutRectangle target)
        {
            var state = Get(handle);
            state.Bounds = target;
            if (!state.Maximized) state.NormalBounds = target;
            state.ClientWidth = Math.Max(1, target.Width - 16);
            state.ClientHeight = Math.Max(1, target.Height - 39);
            Moves.Add((handle, target));
        }

        public void SetTopmost(nint handle, bool topmost)
        {
            var state = Get(handle);
            if (ApplyTopmostImmediately)
            {
                if (topmost) state.ExtendedStyle |= MirrorWindowPositioner.ExtendedStyleTopmost;
                else state.ExtendedStyle &= ~MirrorWindowPositioner.ExtendedStyleTopmost;
            }
            TopmostCalls.Add((handle, topmost));
        }

        private State Get(nint handle) => windows.TryGetValue(handle, out var state)
            ? state : throw new InvalidOperationException("Unknown fake window.");
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
