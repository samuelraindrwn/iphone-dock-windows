using Microsoft.Win32;

namespace BleHid.App.Services;

/// <summary>
/// Per-user auto-start through the Run key.
/// </summary>
public static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    // Deliberately the same value the CLI's --install-autostart writes: only one BLE HID
    // peripheral may own the radio, so enabling one has to displace the other.
    private const string RunValue = "BleHid";

    public const string TrayArgument = "--tray";

    /// <summary>Null when the app is running through `dotnet run`, where there is no exe to register.</summary>
    public static string? Command
    {
        get
        {
            var exe = Environment.ProcessPath;
            return exe is null || exe.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase)
                ? null
                : $"\"{exe}\" {TrayArgument}";
        }
    }

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(RunValue) is string value && value.Length > 0;
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKey);

        if (!enabled)
        {
            key.DeleteValue(RunValue, throwOnMissingValue: false);
            return;
        }

        if (Command is { } command) key.SetValue(RunValue, command);
    }
}
