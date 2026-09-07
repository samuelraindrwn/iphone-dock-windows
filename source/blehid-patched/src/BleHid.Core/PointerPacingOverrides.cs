using System.Text.Json;

namespace BleHid.Core;

internal sealed class PointerPacingOverrides
{
    private const int MaximumIntervalMs = 1000;
    private readonly Dictionary<string, int> _hostMinimums;

    private PointerPacingOverrides(int? defaultMinimumIntervalMs,
        Dictionary<string, int>? hostMinimums, string? warning = null)
    {
        DefaultMinimumIntervalMs = Valid(defaultMinimumIntervalMs);
        _hostMinimums = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, interval) in hostMinimums ?? [])
        {
            var valid = Valid(interval);
            if (!string.IsNullOrWhiteSpace(name) && valid is not null)
                _hostMinimums[name.Trim()] = valid.Value;
        }
        Warning = warning;
    }

    public int? DefaultMinimumIntervalMs { get; }
    public string? Warning { get; }

    public int MinimumIntervalMs(string? hostName, int configuredMinimumIntervalMs)
    {
        var minimum = Math.Max(1, configuredMinimumIntervalMs);
        if (DefaultMinimumIntervalMs is { } defaultMinimum)
            minimum = Math.Max(minimum, defaultMinimum);
        if (!string.IsNullOrWhiteSpace(hostName) &&
            _hostMinimums.TryGetValue(hostName.Trim(), out var hostMinimum))
            minimum = Math.Max(minimum, hostMinimum);
        return minimum;
    }

    public static PointerPacingOverrides Load(string path)
    {
        if (!File.Exists(path)) return new(null, null);

        try
        {
            return Parse(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new(null, null, $"could not read {path}: {ex.Message}");
        }
    }

    internal static PointerPacingOverrides Parse(string json)
    {
        var model = JsonSerializer.Deserialize<FileModel>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new FileModel();
        return new(model.DefaultMinimumIntervalMs, model.HostMinimumIntervalMs);
    }

    private static int? Valid(int? intervalMs) =>
        intervalMs is >= 1 and <= MaximumIntervalMs ? intervalMs : null;

    private sealed class FileModel
    {
        public int? DefaultMinimumIntervalMs { get; init; }
        public Dictionary<string, int>? HostMinimumIntervalMs { get; init; }
    }
}