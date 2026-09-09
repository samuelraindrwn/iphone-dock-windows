using System.Windows;
using System.Windows.Input;

namespace iDock;

/// <summary>
/// Records a new switch-target combination. Recording reads raw key state from this window only;
/// it never installs a global hook, so it cannot capture keys while the window is not focused.
/// </summary>
public partial class HotkeyWindow : Window
{
    private readonly string settingsPath;
    private readonly bool controlRunning;
    private readonly HotkeyConfiguration activeHotkey;
    private readonly HashSet<Key> recordedKeys = [];
    private bool recording;

    internal bool IsRecording => recording;

    internal HotkeyConfiguration Current { get; private set; } = HotkeySettings.DefaultSwitch;

    /// <summary>True when a save happened and a running control session is still using the old binding.</summary>
    internal bool RestartNeeded { get; private set; }

    internal HotkeyWindow(string settingsPath, bool controlRunning, HotkeyConfiguration? activeHotkey = null)
    {
        this.settingsPath = settingsPath;
        this.controlRunning = controlRunning;
        InitializeComponent();
        Resources.MergedDictionaries.Add(UiText.GetResources());
        try { Current = HotkeySettings.Load(settingsPath); }
        catch (Exception ex)
        {
            Current = HotkeySettings.DefaultSwitch;
            SwitchStatus.Text = UiText.T("Hotkey.LoadFailed") + " " + UiText.ResolveException(ex);
        }
        this.activeHotkey = activeHotkey ?? Current;
        RestartNeeded = controlRunning && Current != this.activeHotkey;
        ReleaseHotkeyText.Text = HotkeySettings.Describe(HotkeySettings.Release);
        ScreenshotHotkeyText.Text = HotkeySettings.Describe(HotkeySettings.Screenshot);
        ActiveHotkeyText.Text = UiText.T("Hotkey.ActiveBinding", HotkeySettings.Describe(this.activeHotkey));
        ActiveHotkeyText.Visibility = controlRunning ? Visibility.Visible : Visibility.Collapsed;
        UpdateHotkeyLabel();
    }

    private void UpdateHotkeyLabel() => SwitchHotkeyText.Text = HotkeySettings.Describe(Current);

    private void Record_Click(object sender, RoutedEventArgs e) => BeginRecording();

    internal void BeginRecording(bool focus = true)
    {
        recording = true;
        RecordButton.IsEnabled = false;
        SwitchStatus.Text = UiText.T("Hotkey.Recording");
        // Keys must reach this window rather than moving focus or closing the dialog.
        if (focus) Keyboard.Focus(this);
    }

    private void StopRecording()
    {
        recording = false;
        RecordButton.IsEnabled = true;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (ProcessRecordingKey(key, Keyboard.Modifiers, e.IsRepeat)) e.Handled = true;
        base.OnPreviewKeyDown(e);
    }

    /// <summary>Window-local recording state machine, also exercised without live keyboard hooks.</summary>
    internal bool ProcessRecordingKey(Key key, ModifierKeys keyboardModifiers, bool isRepeat = false)
    {
        // Consume repeats until the accepted/cancelled key is released. Otherwise a held
        // Enter could save the shortcut and immediately activate the dialog's default Close.
        if (!recording) return recordedKeys.Contains(key);
        recordedKeys.Add(key);
        if (isRepeat) return true;

        if (key == Key.Escape)
        {
            StopRecording();
            SwitchStatus.Text = "";
            return true;
        }

        // Wait for a non-modifier key; the user is still assembling the combination.
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return true;

        if (keyboardModifiers.HasFlag(ModifierKeys.Windows))
        {
            SwitchStatus.Text = UiText.T("Hotkey.WindowsUnsupported");
            return true;
        }

        var modifiers = HotkeyModifiers.None;
        if (keyboardModifiers.HasFlag(ModifierKeys.Control)) modifiers |= HotkeyModifiers.Control;
        if (keyboardModifiers.HasFlag(ModifierKeys.Alt)) modifiers |= HotkeyModifiers.Alt;
        if (keyboardModifiers.HasFlag(ModifierKeys.Shift)) modifiers |= HotkeyModifiers.Shift;

        var virtualKey = KeyInterop.VirtualKeyFromKey(key);

        if ((modifiers & (HotkeyModifiers.Control | HotkeyModifiers.Alt)) == 0)
        {
            SwitchStatus.Text = UiText.T("Hotkey.NeedModifier");
            return true;
        }
        var candidate = new HotkeyConfiguration(modifiers, virtualKey);
        if (HotkeySettings.ConflictsWithRelease(candidate))
        {
            SwitchStatus.Text = UiText.T("Hotkey.ReservedRelease");
            return true;
        }
        if (HotkeySettings.ConflictsWithScreenshot(candidate))
        {
            SwitchStatus.Text = UiText.T("Hotkey.ReservedScreenshot");
            return true;
        }
        if (!HotkeySettings.IsValid(candidate))
        {
            SwitchStatus.Text = UiText.T("Hotkey.Unsupported");
            return true;
        }

        StopRecording();
        Save(candidate);
        return true;
    }

    internal bool ProcessRecordedKeyUp(Key key) => recordedKeys.Remove(key) || recording;

    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        if (ProcessRecordedKeyUp(e.Key == Key.System ? e.SystemKey : e.Key)) e.Handled = true;
        base.OnPreviewKeyUp(e);
    }

    internal void CancelRecordingOnFocusLoss()
    {
        if (recording)
        {
            StopRecording();
            SwitchStatus.Text = UiText.T("Hotkey.RecordingCancelled");
        }
        recordedKeys.Clear();
    }

    protected override void OnDeactivated(EventArgs e)
    {
        CancelRecordingOnFocusLoss();
        base.OnDeactivated(e);
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        StopRecording();
        Save(HotkeySettings.DefaultSwitch);
    }

    private void Save(HotkeyConfiguration hotkey)
    {
        try
        {
            HotkeySettings.Save(settingsPath, hotkey);
            Current = hotkey;
            UpdateHotkeyLabel();
            RestartNeeded = controlRunning && Current != activeHotkey;
            SwitchStatus.Text = UiText.T(RestartNeeded ? "Hotkey.SavedRestartNeeded" : "Hotkey.Saved");
        }
        catch (Exception ex)
        {
            SwitchStatus.Text = UiText.T("Hotkey.SaveFailed", UiText.ResolveException(ex));
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
