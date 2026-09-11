using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;

namespace iDock;

public partial class MainWindow
{
    private string mirrorSettingsPath = Path.Combine(UserStorage.Root, "data", "mirror-settings.json");
    private readonly DispatcherTimer volumeSave = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly MirrorWindowPositioner positioner = new();
    private MirrorConfiguration mirrorConfiguration = MirrorConfiguration.Default;
    private bool mirrorReady, mirrorPersisted, audioManaged, audioPending, displayPending, decoderPending, displayWarning, audioWarning;
    private string displayStatusKey = "Display.AutoSaved", audioStatusKey = "Audio.AutoSaved", decoderStatusKey = "Decoder.Probing";
    private object[] displayStatusArgs = [], audioStatusArgs = [], decoderStatusArgs = [];
    private VideoDecoderProbe.ProbeResult? decoderProbe;

    internal VideoDecoderProbe.ProbeResult? DecoderProbe => decoderProbe;

    internal MirrorConfiguration MirrorState => mirrorConfiguration;

    private void InitializeMirrorPresentation(bool preview, string? settingsFile)
    {
        mirrorPersisted = !preview || settingsFile is not null;
        if (settingsFile is not null)
        {
            var supplied = Path.GetFullPath(settingsFile);
            var installed = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "iDock", "data", "mirror-settings.json");
            if (preview && (string.Equals(supplied, Path.GetFullPath(mirrorSettingsPath), StringComparison.OrdinalIgnoreCase)
                || string.Equals(supplied, Path.GetFullPath(installed), StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Preview persistence requires an isolated mirror settings file.", nameof(settingsFile));
            mirrorSettingsPath = supplied;
        }
        Exception? loadError = null;
        if (mirrorPersisted)
        {
            try
            {
                if (MirrorSettings.Load(mirrorSettingsPath) is { } saved)
                {
                    mirrorConfiguration = saved;
                    audioManaged = true; // A saved file is the user's explicit choice to manage the receiver's volume.
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            { loadError = ex; }
        }
        VolumeSlider.Value = mirrorConfiguration.Volume;
        DisplayModeCombo.SelectedValue = MirrorSettings.ModeTag(mirrorConfiguration.WindowMode);
        DecoderCombo.SelectedValue = MirrorSettings.DecoderTag(mirrorConfiguration.Decoder);
        UpdateVolumeLabel();
        RenderMuteButton();
        volumeSave.Tick += (_, _) => SaveMirrorSettings();
        mirrorReady = true;
        if (loadError is not null)
        {
            SetAudioStatus("MirrorSettings.LoadFailed", loadError);
            SetDisplayStatus("MirrorSettings.LoadFailed", loadError);
            AppendT("MirrorSettings.LoadFailed", loadError);
        }
        else if (!mirrorPersisted)
        {
            SetAudioStatus("Audio.Preview");
            SetDisplayStatus("Display.Preview");
        }
        RenderDecoderStatus();
        StartDecoderProbe();
    }

    // Direct3D device creation takes a moment; keep it off the UI thread and only ever
    // publish the answer into a window that is still open.
    private void StartDecoderProbe()
    {
        _ = Task.Run(VideoDecoderProbe.Run).ContinueWith(task =>
        {
            if (task.IsCompletedSuccessfully) Dispatcher.BeginInvoke(new Action(() => PublishDecoderProbe(task.Result)));
        });
    }

    internal void PublishDecoderProbe(VideoDecoderProbe.ProbeResult result)
    {
        if (closed) return;
        var first = decoderProbe is null;
        decoderProbe = result;
        if (first) AppendT("Log.DecoderProbe", result.Supported ? result.Detail : "unsupported - " + result.Detail);
        RenderDecoderStatus();
    }

    private void RenderDecoderStatus()
    {
        if (mirrorConfiguration.Decoder == VideoDecoderMode.Software) SetDecoderStatus("Decoder.SoftwareChosen");
        else if (decoderProbe is not { } probe) SetDecoderStatus("Decoder.Probing");
        else if (probe.Supported) SetDecoderStatus("Decoder.Supported");
        else SetDecoderStatus("Decoder.Unsupported", probe.Detail);
    }

    private void Decoder_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!mirrorReady || closing || applyingLanguage || DecoderCombo.SelectedValue is not string tag
            || !MirrorSettings.TryParseDecoder(tag, out var mode) || mode == mirrorConfiguration.Decoder) return;
        mirrorConfiguration = mirrorConfiguration with { Decoder = mode };
        decoderPending = true;
        SaveMirrorSettings();
        RenderDecoderStatus();
    }

    // Runs right before the receiver starts, so the wrapper reads the intended file.
    // Editing failures are reported but never block opening mirroring.
    private void PrepareReceiverArguments()
    {
        if (previewMode) return;
        decoderProbe ??= VideoDecoderProbe.Run();
        var wantHardware = mirrorConfiguration.Decoder == VideoDecoderMode.Auto && decoderProbe.Value.Supported;
        var path = UxPlayArguments.DefaultPath;
        try
        {
            var result = UxPlayArguments.Apply(path, wantHardware);
            if (result.BackedUp) AppendT("Log.DecoderBackup", UxPlayArguments.BackupPath(path));
            if (!result.Changed) return;
            var line = string.Join(' ', result.Tokens);
            AppendT("Log.DecoderArguments", path, line);
            SetDecoderStatus("Decoder.Applied", line);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetDecoderStatus("Decoder.ApplyFailed", ex);
            AppendT("Decoder.ApplyFailed", ex);
        }
    }

    private void UpdateVolumeLabel() => VolumeValue.Text =
        MirrorSettings.NormalizeVolume(VolumeSlider.Value).ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";

    private void RenderMuteButton()
    {
        MuteButton.SetResourceReference(ContentControl.ContentProperty, mirrorConfiguration.Muted ? "Text.Audio.Unmute" : "Text.Audio.Mute");
        MuteButton.SetResourceReference(AutomationProperties.NameProperty,
            mirrorConfiguration.Muted ? "Text.Audio.UnmuteAccessible" : "Text.Audio.MuteAccessible");
    }

    private void Volume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!mirrorReady || closing || applyingLanguage) return;
        UpdateVolumeLabel();
        mirrorConfiguration = mirrorConfiguration with { Volume = MirrorSettings.NormalizeVolume(VolumeSlider.Value) };
        audioManaged = audioPending = true;
        SetAudioStatus("Audio.Saving");
        volumeSave.Stop();
        volumeSave.Start();
    }

    private void Mute_Click(object sender, RoutedEventArgs e)
    {
        if (!mirrorReady || closing) return;
        mirrorConfiguration = mirrorConfiguration with { Muted = !mirrorConfiguration.Muted };
        RenderMuteButton();
        audioManaged = audioPending = true;
        SaveMirrorSettings();
        ApplyMirrorAudio();
    }

    private void DisplayMode_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!mirrorReady || closing || applyingLanguage || DisplayModeCombo.SelectedValue is not string tag
            || !MirrorSettings.TryParseMode(tag, out var mode) || mode == mirrorConfiguration.WindowMode) return;
        mirrorConfiguration = mirrorConfiguration with { WindowMode = mode };
        displayPending = true;
        SaveMirrorSettings();
        ApplyMirrorLayout(force: true);
    }

    private void ReapplyDisplay_Click(object sender, RoutedEventArgs e)
    {
        if (closing || closed) return;
        ApplyMirrorLayout(force: true);
    }

    private void SaveMirrorSettings()
    {
        volumeSave.Stop();
        if (!audioPending && !displayPending && !decoderPending) return;
        var savingAudio = audioPending;
        var savingDisplay = displayPending;
        var savingDecoder = decoderPending;
        if (!mirrorPersisted)
        {
            audioPending = displayPending = decoderPending = false;
            if (savingAudio) SetAudioStatus("Audio.Preview");
            if (savingDisplay) SetDisplayStatus("Display.Preview");
            if (savingDecoder) SetDecoderStatus("Decoder.Preview");
            return;
        }
        try
        {
            MirrorSettings.Save(mirrorSettingsPath, mirrorConfiguration);
            audioPending = displayPending = decoderPending = false;
            if (savingAudio) SetAudioStatus("Audio.Saved");
            if (savingDisplay) SetDisplayStatus("Display.Saved");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (savingAudio) SetAudioStatus("Audio.SaveFailed");
            if (savingDisplay) SetDisplayStatus("Display.SaveFailed");
            if (savingDecoder) SetDecoderStatus("Decoder.SaveFailed");
            AppendT("Log.MirrorSettingsSaveFailed", ex);
        }
    }

    // Runs from the 250 ms lifecycle poll with the snapshot it already took, or on demand
    // with a fresh one. Only windows the lifecycle proved to be in this session's Job arrive.
    private void ApplyMirrorLayout(bool force)
    {
        if (previewMode || closing || closed || endingSession || !engines.MirrorRunning) return;
        try
        {
            var snapshot = force ? engines.CaptureVideoWindows() : engines.LastMirrorSnapshot;
            if (snapshot is not { ReadSucceeded: true, ReceiverRunning: true } current) return;
            var changed = positioner.Apply(current.Windows, mirrorConfiguration.WindowMode, force);
            displayWarning = false;
            if (changed == 0) return;
            SetDisplayStatus("Display.Applied", changed);
            AppendT("Log.DisplayApplied", MirrorSettings.ModeTag(mirrorConfiguration.WindowMode));
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or ArgumentOutOfRangeException)
        {
            if (displayWarning) return;
            displayWarning = true;
            SetDisplayStatus("Display.ApplyFailed", ex);
            AppendT("Display.ApplyFailed", ex);
        }
    }

    // Runs from the one-second refresh. The receiver's audio session only exists once the
    // device streams sound, so the saved value is re-checked while mirroring is running.
    private void ApplyMirrorAudio()
    {
        if (previewMode || closing || closed || endingSession || !audioManaged || !engines.MirrorRunning) return;
        try
        {
            var result = MirrorAudioControl.Apply(engines.GetMirrorOwnershipCheck(), mirrorConfiguration.Volume, mirrorConfiguration.Muted);
            audioWarning = false;
            if (result.Changed > 0) SetAudioStatus("Audio.Applied", result.Matched);
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or Win32Exception)
        {
            if (audioWarning) return;
            audioWarning = true;
            SetAudioStatus("Audio.ApplyFailed", ex);
            AppendT("Audio.ApplyFailed", ex);
        }
    }

    private void ResetMirrorPresentation()
    {
        positioner.Reset();
        displayWarning = audioWarning = false;
    }

    private void SetDisplayStatus(string key, params object[] args)
    { displayStatusKey = key; displayStatusArgs = args; DisplayStatus.Text = UiText.T(key, ResolveArguments(args)); }

    private void SetAudioStatus(string key, params object[] args)
    { audioStatusKey = key; audioStatusArgs = args; AudioStatus.Text = UiText.T(key, ResolveArguments(args)); }

    private void SetDecoderStatus(string key, params object[] args)
    { decoderStatusKey = key; decoderStatusArgs = args; DecoderStatus.Text = UiText.T(key, ResolveArguments(args)); }

    private void RenderMirrorStatuses()
    {
        DisplayStatus.Text = UiText.T(displayStatusKey, ResolveArguments(displayStatusArgs));
        AudioStatus.Text = UiText.T(audioStatusKey, ResolveArguments(audioStatusArgs));
        DecoderStatus.Text = UiText.T(decoderStatusKey, ResolveArguments(decoderStatusArgs));
    }
}
