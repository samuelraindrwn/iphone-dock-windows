using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace BleHid.App.Services;

/// <summary>
/// User preferences, persisted next to the background log so the CLI and the UI agree on a home.
/// </summary>
public sealed class AppSettings : INotifyPropertyChanged
{
    private static readonly string Path = System.IO.Path.Combine(
        BleHid.Core.AppPaths.Root, "app-settings.json");

    public static AppSettings Instance { get; } = Load();

    private bool _closeToTray = true;
    public bool CloseToTray
    {
        get => _closeToTray;
        set { if (Set(ref _closeToTray, value)) Save(); }
    }

    private bool _startPeripheralOnLaunch = true;
    public bool StartPeripheralOnLaunch
    {
        get => _startPeripheralOnLaunch;
        set { if (Set(ref _startPeripheralOnLaunch, value)) Save(); }
    }

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(Path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path)) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // A corrupt or unreadable file is not worth blocking startup over.
        }

        return new AppSettings();
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.WriteAllText(Path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}
