using System.Globalization;
using System.Windows;

namespace iDock;

// UI language never changes the process culture or the BLE protocol. Keep both
// translations together so missing entries cannot silently fall back to another language.
internal static partial class UiText
{
    internal sealed record Translation(string Indonesian, string English);
    private static readonly IReadOnlyDictionary<string, Translation> entries = CreateEntries();
    internal static string CurrentLanguage { get; private set; } = "id";
    internal static IReadOnlyDictionary<string, Translation> Entries => entries;

    internal static bool IsSupported(string? language) => language is "id" or "en";

    internal static void SelectLanguage(string language)
    {
        if (!IsSupported(language)) throw new ArgumentOutOfRangeException(nameof(language));
        CurrentLanguage = language;
    }

    internal static string T(string key, params object[] args) => ForLanguage(key, CurrentLanguage, args);

    internal static TException TagException<TException>(TException exception, string key, params object[] args)
        where TException : Exception
    {
        exception.Data["iDock.TextKey"] = key;
        exception.Data["iDock.TextArguments"] = args;
        return exception;
    }

    internal static string ResolveException(Exception exception) =>
        exception.Data["iDock.TextKey"] is string key
            ? T(key, exception.Data["iDock.TextArguments"] as object[] ?? [])
            : exception.Message;

    internal static string ForLanguage(string key, string language, params object[] args)
    {
        if (!IsSupported(language)) throw new ArgumentOutOfRangeException(nameof(language));
        var entry = entries[key];
        var text = language == "en" ? entry.English : entry.Indonesian;
        return args.Length == 0 ? text : string.Format(CultureInfo.InvariantCulture, text, args);
    }

    internal static ResourceDictionary GetResources()
    {
        var resources = new ResourceDictionary();
        foreach (var key in entries.Keys) resources.Add("Text." + key, T(key));
        return resources;
    }

    private static IReadOnlyDictionary<string, Translation> CreateEntries()
    {
        var result = new Dictionary<string, Translation>(StringComparer.Ordinal);
        AddWindowEntries(result);
        AddEngineEntries(result);
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string, Translation>(result);
    }

    static partial void AddWindowEntries(Dictionary<string, Translation> entries);
    static partial void AddEngineEntries(Dictionary<string, Translation> entries);
}
