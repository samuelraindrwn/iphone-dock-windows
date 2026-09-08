using System.IO;
using System.Text.Json;

namespace iDock;

internal sealed record PointerConfiguration(double Sensitivity, int RotationDegrees = 0);

internal static class PointerSettings
{
    public const double Minimum = 0.25;
    public const double Maximum = 3.0;
    public const double Normal = 1.0;

    public static double Normalize(double value) =>
        double.IsFinite(value) ? Math.Clamp(value, Minimum, Maximum) : Normal;

    public static bool IsValidRotation(int degrees) => degrees is 0 or 90 or 180 or 270;

    public static double Load(string path) => LoadAll(path).Sensitivity;

    public static PointerConfiguration LoadAll(string path)
    {
        if (!File.Exists(path)) return new(Normal);
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var json = JsonDocument.Parse(file);
        if (json.RootElement.ValueKind != JsonValueKind.Object
            || !json.RootElement.TryGetProperty("Sensitivity", out var value)
            || value.ValueKind != JsonValueKind.Number
            || !value.TryGetDouble(out var sensitivity) || !double.IsFinite(sensitivity))
            throw new JsonException("Nilai sensitivitas tidak valid.");
        var rotation = 0;
        if (json.RootElement.TryGetProperty("RotationDegrees", out var rotationValue)
            && (rotationValue.ValueKind != JsonValueKind.Number || !rotationValue.TryGetInt32(out rotation)
                || !IsValidRotation(rotation)))
            throw new JsonException("Nilai orientasi tidak valid.");
        return new(Normalize(sensitivity), rotation);
    }

    public static void Save(string path, double sensitivity, int rotationDegrees = 0)
    {
        if (!IsValidRotation(rotationDegrees)) throw new ArgumentOutOfRangeException(nameof(rotationDegrees));
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, ".pointer-settings-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(
                new PointerConfiguration(Normalize(sensitivity), rotationDegrees)));
            // Publish a complete JSON document; the running control engine never reads a partial write.
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
