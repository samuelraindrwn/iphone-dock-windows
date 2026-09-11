using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;

namespace iDock;

/// <summary>Hardware-free checks for the decoder choice, the D3D11 probe, and arguments.txt editing.</summary>
internal static class DecoderVerification
{
    internal static void Run(Action<bool, string> check)
    {
        UiText.SelectLanguage("id");
        string[] T(string line) => UxPlayArguments.Tokenize(line);
        check(T("  -n uxplay-windows   -nh ").SequenceEqual(new[] { "-n", "uxplay-windows", "-nh" }) && T("").Length == 0,
            "arguments.txt tokens split on single spaces exactly like the UxPlay wrapper");
        var user = T("-n uxplay-windows -nh -vsync no -fps 60");
        var added = UxPlayArguments.WithHardwareDecoder(user);
        check(added.SequenceEqual(new[] { "-n", "uxplay-windows", "-nh", "-vsync", "no", "-fps", "60", "-vd", "d3d11h264dec" }),
            "Enabling the hardware decoder appends only the -vd pair and keeps every user token in order");
        check(UxPlayArguments.WithHardwareDecoder(added).SequenceEqual(added),
            "Enabling the hardware decoder twice does not duplicate the pair");
        var custom = T("-n uxplay-windows -vd nvh264dec");
        check(UxPlayArguments.WithHardwareDecoder(custom).SequenceEqual(custom) && UxPlayArguments.HasOtherDecoder(custom),
            "A user's own -vd choice is never replaced by d3d11h264dec");
        check(UxPlayArguments.WithoutHardwareDecoder(added).SequenceEqual(user),
            "Disabling the hardware decoder removes exactly the d3d11h264dec pair");
        check(UxPlayArguments.WithoutHardwareDecoder(custom).SequenceEqual(custom),
            "Disabling the hardware decoder leaves a different -vd choice untouched");
        check(UxPlayArguments.WithoutHardwareDecoder(T("-vd d3d11h264dec -n x -vd d3d11h264dec")).SequenceEqual(new[] { "-n", "x" }),
            "Every d3d11h264dec pair is removed, wherever it sits");
        check(UxPlayArguments.HasOtherDecoder(T("-n uxplay-windows -vd"))
            && UxPlayArguments.WithHardwareDecoder(T("-n uxplay-windows -vd")).SequenceEqual(new[] { "-n", "uxplay-windows", "-vd" }),
            "A dangling -vd is treated as the user's choice and left alone");

        var directory = Path.Combine(Path.GetTempPath(), "idock-uxplay-args-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "arguments.txt");
        var backup = UxPlayArguments.BackupPath(path);
        try
        {
            var created = UxPlayArguments.Apply(path, wantHardwareDecoder: true);
            check(created.Changed && !created.BackedUp && !File.Exists(backup)
                && File.ReadAllText(path, Encoding.UTF8) == "-n uxplay-windows -nh -vd d3d11h264dec",
                "A missing arguments.txt is created from the wrapper defaults plus the decoder, without a backup of nothing");
            File.WriteAllText(path, "-n uxplay-windows -nh -vsync no -fps 60", new UTF8Encoding(false));
            var original = File.ReadAllBytes(path);
            var applied = UxPlayArguments.Apply(path, wantHardwareDecoder: true);
            check(applied.Changed && applied.BackedUp && File.ReadAllBytes(backup).SequenceEqual(original)
                && File.ReadAllText(path, Encoding.UTF8) == "-n uxplay-windows -nh -vsync no -fps 60 -vd d3d11h264dec",
                "The first edit backs up the original file byte for byte before appending the decoder");
            var repeat = UxPlayArguments.Apply(path, wantHardwareDecoder: true);
            check(!repeat.Changed && !repeat.BackedUp && File.ReadAllBytes(backup).SequenceEqual(original),
                "Re-applying an already present decoder changes nothing and keeps the backup pristine");
            var removed = UxPlayArguments.Apply(path, wantHardwareDecoder: false);
            check(removed.Changed && !removed.BackedUp && File.ReadAllText(path, Encoding.UTF8) == "-n uxplay-windows -nh -vsync no -fps 60"
                && File.ReadAllBytes(backup).SequenceEqual(original),
                "Choosing the software decoder removes the pair and never overwrites the one backup");
            check(Directory.GetFiles(directory).Length == 2 && File.ReadAllBytes(path)[0] != 0xEF,
                "arguments.txt edits are atomic, leave no temporary files, and write UTF-8 without a BOM");
            File.WriteAllText(path, "   ", new UTF8Encoding(false));
            var blank = UxPlayArguments.Apply(path, wantHardwareDecoder: true);
            check(blank.Tokens.SequenceEqual(new[] { "-n", "uxplay-windows", "-nh", "-vd", "d3d11h264dec" }),
                "A blank arguments.txt falls back to the wrapper defaults like the wrapper itself does");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }

        var probe = VideoDecoderProbe.Run();
        check(!string.IsNullOrWhiteSpace(probe.Detail),
            "The D3D11 decoder probe returns a definite answer with a reason instead of throwing");
        var again = VideoDecoderProbe.Run();
        check(again.Supported == probe.Supported,
            "The D3D11 decoder probe is stable across repeated runs in one process");

        check(MirrorSettings.TryParseDecoder("auto", out var auto) && auto == VideoDecoderMode.Auto
            && MirrorSettings.TryParseDecoder("software", out var software) && software == VideoDecoderMode.Software
            && !MirrorSettings.TryParseDecoder("Software", out _) && !MirrorSettings.TryParseDecoder("nvdec", out _)
            && MirrorSettings.DecoderTag(VideoDecoderMode.Software) == "software",
            "Decoder modes accept only the auto/software storage tags");
        var settingsDirectory = Path.Combine(Path.GetTempPath(), "idock-decoder-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(settingsDirectory);
        var settingsPath = Path.Combine(settingsDirectory, "mirror-settings.json");
        try
        {
            MirrorSettings.Save(settingsPath, new(70, false, MirrorWindowMode.Windowed, VideoDecoderMode.Software));
            check(MirrorSettings.Load(settingsPath) == new MirrorConfiguration(70, false, MirrorWindowMode.Windowed, VideoDecoderMode.Software),
                "The decoder choice round-trips with volume, mute and display mode");
            var window = NewWindow(settingsPath);
            try
            {
                window.Show();
                Settle(window);
                var combo = window.DecoderCombo;
                check(combo.SelectedValuePath == "Tag" && combo.Items.Cast<ComboBoxItem>().Select(item => item.Tag as string)
                        .SequenceEqual(new[] { "auto", "software" }) && (string)combo.SelectedValue == "software"
                    && combo.Focusable && combo.IsTabStop && !string.IsNullOrWhiteSpace(AutomationProperties.GetName(combo)),
                    "The decoder selector offers automatic and software, restores the saved choice, and is keyboard reachable");
                check(window.DecoderStatus.Text == UiText.T("Decoder.SoftwareChosen"),
                    "A saved software choice is explained without waiting for the GPU probe");
                combo.SelectedValue = "auto";
                check(MirrorSettings.Load(settingsPath)?.Decoder == VideoDecoderMode.Auto
                    && MirrorSettings.Load(settingsPath)?.Volume == 70,
                    "Changing the decoder saves immediately while preserving the other mirror settings");
                window.PublishDecoderProbe(new VideoDecoderProbe.ProbeResult(true, "fixture"));
                check(window.DecoderStatus.Text == UiText.T("Decoder.Supported"),
                    "A supported probe reports that the hardware decoder will be used when mirroring opens");
                window.PublishDecoderProbe(new VideoDecoderProbe.ProbeResult(false, "fixture"));
                check(window.DecoderStatus.Text == UiText.T("Decoder.Unsupported", "fixture"),
                    "An unsupported probe reports the software fallback with its reason");
                window.LanguageCombo.SelectedValue = "en";
                Settle(window);
                check(window.DecoderStatus.Text == UiText.ForLanguage("Decoder.Unsupported", "en", "fixture")
                    && ((ComboBoxItem)combo.SelectedItem).Content as string == UiText.ForLanguage("Decoder.Auto", "en"),
                    "A live language change re-renders the decoder status and labels");
                window.LanguageCombo.SelectedValue = "id";
                Settle(window);
            }
            finally { window.Close(); }
        }
        finally
        {
            UiText.SelectLanguage("id");
            if (Directory.Exists(settingsDirectory)) Directory.Delete(settingsDirectory, recursive: true);
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
}
