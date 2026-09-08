using System.IO;

namespace iDock;

// The installer protects executables in Program Files. Only mutable user state
// belongs in LocalAppData. Portable/source builds keep their existing data paths.
internal static class UserStorage
{
    internal static string Root => ResolveRoot(AppContext.BaseDirectory,
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        File.Exists(Path.Combine(AppContext.BaseDirectory, "installed.mode")));

    internal static string ResolveRoot(string applicationDirectory, string localDataDirectory, bool installed)
    {
        if (!installed) return Path.GetFullPath(applicationDirectory);
        if (string.IsNullOrWhiteSpace(localDataDirectory))
            throw new InvalidOperationException("Folder data pengguna Windows tidak tersedia.");
        return Path.GetFullPath(Path.Combine(localDataDirectory, "iDock"));
    }
}
