using BleHid.Core;
using Xunit;

namespace BleHid.Core.Tests;

public class AppPathsTests
{
    [Fact]
    public void Root_lives_under_local_application_data()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Assert.StartsWith(localAppData, AppPaths.Root, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("BleHid", Path.GetFileName(AppPaths.Root));
    }

    [Fact]
    public void Logs_is_a_subfolder_of_root()
    {
        Assert.Equal(AppPaths.Root, Path.GetDirectoryName(AppPaths.Logs));
        Assert.Equal("logs", Path.GetFileName(AppPaths.Logs));
    }

    [Fact]
    public void InLogs_creates_the_directory_and_returns_a_path_inside_it()
    {
        var path = AppPaths.InLogs("unit-test-probe.txt");

        Assert.True(Directory.Exists(AppPaths.Logs));
        Assert.Equal(AppPaths.Logs, Path.GetDirectoryName(path));
    }

    [Fact]
    public void InRoot_keeps_settings_out_of_the_logs_folder()
    {
        var path = AppPaths.InRoot("app-settings.json");

        Assert.Equal(AppPaths.Root, Path.GetDirectoryName(path));
        Assert.NotEqual(AppPaths.Logs, Path.GetDirectoryName(path));
    }

    /// <summary>Every log a user is asked to send must sit in one folder they can zip.</summary>
    [Fact]
    public void All_known_log_files_resolve_into_the_logs_folder()
    {
        foreach (var name in new[] { "blehid.log", "blehid-app.log", "diagnostics.txt" })
            Assert.Equal(AppPaths.Logs, Path.GetDirectoryName(AppPaths.InLogs(name)));
    }
}
