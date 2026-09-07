namespace BleHid.Core;

/// <summary>
/// One home for everything the CLI and the app write, so the two never disagree about where
/// a user's logs are.
/// </summary>
public static class AppPaths
{
    public static string Root { get; } = ResolveRoot(Environment.GetEnvironmentVariable("BLEHID_DATA_DIR"));

    /// <summary>An optional per-process data directory; the default remains LocalAppData/BleHid.</summary>
    internal static string ResolveRoot(string? directory) => string.IsNullOrWhiteSpace(directory)
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BleHid")
        : Path.GetFullPath(directory);

    public static string Logs { get; } = Path.Combine(Root, "logs");

    /// <summary>Returns the full path, having created the directory it lives in.</summary>
    public static string InLogs(string fileName)
    {
        Directory.CreateDirectory(Logs);
        return Path.Combine(Logs, fileName);
    }

    public static string InRoot(string fileName)
    {
        Directory.CreateDirectory(Root);
        return Path.Combine(Root, fileName);
    }
}
