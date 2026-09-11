using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace iDock;

public partial class MainWindow
{
    private bool previewMode, persistLanguage, languageReady, applyingLanguage, controlStatusObserved;
    private string languageSettingsFile = Path.Combine(UserStorage.Root, "data", "ui-settings.json");
    private ResourceDictionary? languageResources;
    private string mirrorStatusKey = "Mirror.NotStarted";
    private string sensitivityStatusKey = "Sensitivity.AutoSaved";
    private string diagnosticStatusKey = "Diagnostic.Ready";
    private object[] diagnosticStatusArgs = [];
    private (int ExitCode, string Report)? diagnosticResult;
    private string languageStatusKey = "Language.AutoSaved";
    private object[] languageStatusArgs = [];

    internal ControlStatusTracker ControlState => controlStatus;

    private Exception? InitializeLanguagePreference(bool preview, string? settingsFile)
    {
        previewMode = preview;
        persistLanguage = !preview || settingsFile is not null;
        if (settingsFile is not null)
        {
            var supplied = Path.GetFullPath(settingsFile);
            var installed = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "iDock", "data", "ui-settings.json");
            if (preview && (string.Equals(supplied, Path.GetFullPath(languageSettingsFile), StringComparison.OrdinalIgnoreCase)
                || string.Equals(supplied, Path.GetFullPath(installed), StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Preview persistence requires an isolated language settings file.", nameof(settingsFile));
            languageSettingsFile = supplied;
        }
        if (!persistLanguage) return null; // Keep an explicitly selected preview language; never read user preferences.
        try { UiText.SelectLanguage(LanguageSettings.Load(languageSettingsFile)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            UiText.SelectLanguage("id");
            return ex;
        }
        return null;
    }

    private void InitializeLanguageControls(Exception? loadError)
    {
        ApplyLanguageResources();
        LanguageCombo.SelectedValue = UiText.CurrentLanguage;
        languageReady = true;
        if (loadError is not null) SetLanguageStatus("Language.LoadFailed", loadError);
        else if (!persistLanguage) SetLanguageStatus("Language.Preview");
    }

    private void Language_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!languageReady || applyingLanguage || closing || closed
            || LanguageCombo.SelectedValue is not string language || !UiText.IsSupported(language)
            || language == UiText.CurrentLanguage) return;
        UiText.SelectLanguage(language);
        ApplyLanguageResources();
        // Language changes do not call Refresh, replay BLE logs, restart engines,
        // change the input target, or flush pending pointer settings.
        if (!persistLanguage) { SetLanguageStatus("Language.Preview"); return; }
        try
        {
            LanguageSettings.Save(languageSettingsFile, language);
            SetLanguageStatus("Language.Saved");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetLanguageStatus("Language.SaveFailed", ex);
            AppendT("Language.SaveFailed", ex);
        }
    }

    private void ApplyLanguageResources()
    {
        applyingLanguage = true;
        try
        {
            var replacement = UiText.GetResources();
            if (languageResources is null) Resources.MergedDictionaries.Add(replacement);
            else Resources.MergedDictionaries[Resources.MergedDictionaries.IndexOf(languageResources)] = replacement;
            languageResources = replacement;
            MirrorStatus.Text = UiText.T(mirrorStatusKey);
            SensitivityStatus.Text = UiText.T(sensitivityStatusKey);
            ControlStatus.Text = controlStatusObserved ? controlStatus.DisplayText : UiText.T("Control.NotConnected");
            RenderDiagnosticStatus();
            RenderScreenshotStatus();
            RenderMirrorStatuses();
            LanguageStatus.Text = UiText.T(languageStatusKey, ResolveArguments(languageStatusArgs));
        }
        finally { applyingLanguage = false; }
    }

    private static object[] ResolveArguments(object[] args) => args.Select(value =>
        value is Exception exception ? (object)UiText.ResolveException(exception) : value).ToArray();

    private void SetMirrorStatus(string key)
    { mirrorStatusKey = key; MirrorStatus.Text = UiText.T(key); }

    private void SetSensitivityStatus(string key)
    { sensitivityStatusKey = key; SensitivityStatus.Text = UiText.T(key); }

    private void SetLanguageStatus(string key, params object[] args)
    { languageStatusKey = key; languageStatusArgs = args; LanguageStatus.Text = UiText.T(key, ResolveArguments(args)); }

    private void SetDiagnosticStatus(string key, params object[] args)
    {
        diagnosticResult = null;
        diagnosticStatusKey = key;
        diagnosticStatusArgs = args;
        RenderDiagnosticStatus();
    }

    private void RenderDiagnosticStatus() => DiagnosticStatus.Text = diagnosticResult is { } result
        ? DescribeDiagnostic(result.ExitCode, result.Report)
        : UiText.T(diagnosticStatusKey, ResolveArguments(diagnosticStatusArgs));

    internal void UpdateControlStatus()
    { controlStatusObserved = true; ControlStatus.Text = controlStatus.DisplayText; }

    private void AppendT(string key, params object[] args) => Append(UiText.T(key, ResolveArguments(args)));
}
