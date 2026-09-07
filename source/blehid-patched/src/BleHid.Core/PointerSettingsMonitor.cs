using System.Text.Json;

namespace BleHid.Core;

/// <summary>The pump observes sensitivity, rotation and their revision atomically.</summary>
internal sealed record PointerSettingsSnapshot(double Sensitivity, long Revision, int RotationDegrees = 0);

/// <summary>
/// Reads pointer-settings.json on a worker, never on an input hook or the report pump.
/// Missing files restore 1x/0 degrees; incomplete/unreadable writes keep the last good snapshot.
/// </summary>
internal sealed class PointerSettingsMonitor : IAsyncDisposable
{
    internal const double MinimumSensitivity = 0.25;
    internal const double MaximumSensitivity = 3.0;
    internal const double DefaultSensitivity = 1.0;
    private readonly string _path;
    private readonly Action<string> _log;
    private readonly CancellationTokenSource _cancellation;
    private readonly Task _worker;
    private PointerSettingsSnapshot _current = new(DefaultSensitivity, 0);
    private bool _warningLogged;

    public PointerSettingsMonitor(string path, Action<string> log, CancellationToken cancellationToken)
    {
        _path = path;
        _log = log;
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _worker = Task.Run(MonitorAsync);
    }

    public PointerSettingsSnapshot Current => Volatile.Read(ref _current);
    internal Task Completion => _worker;

    internal static double Normalize(double sensitivity) => double.IsFinite(sensitivity)
        ? Math.Clamp(sensitivity, MinimumSensitivity, MaximumSensitivity)
        : DefaultSensitivity;

    internal static bool IsValidRotation(int rotationDegrees) => rotationDegrees is 0 or 90 or 180 or 270;

    internal static bool TryParse(string json, out double sensitivity) => TryParse(json, out sensitivity, out _);

    internal static bool TryParse(string json, out double sensitivity, out int rotationDegrees)
    {
        sensitivity = DefaultSensitivity;
        rotationDegrees = 0;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("Sensitivity", out var property) ||
                property.ValueKind != JsonValueKind.Number ||
                !property.TryGetDouble(out var value) || !double.IsFinite(value)) return false;
            if (document.RootElement.TryGetProperty("RotationDegrees", out var rotation) &&
                (rotation.ValueKind != JsonValueKind.Number ||
                 !rotation.TryGetInt32(out rotationDegrees) || !IsValidRotation(rotationDegrees))) return false;
            sensitivity = Normalize(value);
            return true;
        }
        catch (JsonException) { return false; }
    }

    private async Task MonitorAsync()
    {
        var token = _cancellation.Token;
        try
        {
            while (!token.IsCancellationRequested)
            {
                var sensitivity = DefaultSensitivity;
                var rotationDegrees = 0;
                var valid = true;
                try
                {
                    // Readers tolerate atomic replacement by the UI. A failed/incomplete
                    // read is retried on the next poll without changing an active gain.
                    using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete);
                    if (stream.Length > 4096) valid = false;
                    else
                    {
                        using var reader = new StreamReader(stream);
                        valid = TryParse(await reader.ReadToEndAsync(token), out sensitivity, out rotationDegrees);
                    }
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    valid = false;
                }

                if (valid)
                {
                    var previous = Current;
                    if (previous.Sensitivity != sensitivity || previous.RotationDegrees != rotationDegrees)
                        Volatile.Write(ref _current, new(sensitivity, previous.Revision + 1, rotationDegrees));
                }
                else if (!_warningLogged)
                {
                    _warningLogged = true;
                    _log("[pointer] settings unreadable/invalid; keeping last good sensitivity/rotation and retrying (further warnings suppressed)");
                }

                await Task.Delay(250, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    public async ValueTask DisposeAsync()
    {
        _cancellation.Cancel();
        await _worker;
        _cancellation.Dispose();
    }
}
