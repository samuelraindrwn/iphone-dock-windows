using System.IO;
using System.Text;

namespace iDock;

// The pinned uxplay-windows wrapper builds its UxPlay command line only from
// %APPDATA%\leapbtw\uxplay-windows\arguments.txt (QStandardPaths, not argv or env).
// iDock edits exactly one thing in that file, the "-vd d3d11h264dec" pair, keeps the
// user's other tokens verbatim, and saves the untouched original once as a backup.
internal static class UxPlayArguments
{
    internal const string DecoderFlag = "-vd";
    internal const string HardwareDecoder = "d3d11h264dec";
    internal static readonly string[] DefaultTokens = ["-n", "uxplay-windows", "-nh"]; // the wrapper's own default

    internal static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "leapbtw", "uxplay-windows", "arguments.txt");

    internal static string BackupPath(string path) => path + ".idock-backup";

    internal sealed record ApplyResult(bool Changed, bool BackedUp, string[] Tokens);

    /// <summary>Same split as the wrapper: single spaces, empty entries dropped, no quoting.</summary>
    internal static string[] Tokenize(string content) => content.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

    internal static bool HasHardwareDecoder(IReadOnlyList<string> tokens)
    {
        for (var index = 0; index + 1 < tokens.Count; index++)
            if (tokens[index] == DecoderFlag && tokens[index + 1] == HardwareDecoder) return true;
        return false;
    }

    /// <summary>A different "-vd" value is the user's explicit choice and is never replaced.</summary>
    internal static bool HasOtherDecoder(IReadOnlyList<string> tokens)
    {
        for (var index = 0; index < tokens.Count; index++)
            if (tokens[index] == DecoderFlag && (index + 1 >= tokens.Count || tokens[index + 1] != HardwareDecoder)) return true;
        return false;
    }

    internal static string[] WithHardwareDecoder(IReadOnlyList<string> tokens)
    {
        if (HasHardwareDecoder(tokens) || HasOtherDecoder(tokens)) return tokens.ToArray();
        return tokens.Concat([DecoderFlag, HardwareDecoder]).ToArray();
    }

    internal static string[] WithoutHardwareDecoder(IReadOnlyList<string> tokens)
    {
        var result = new List<string>(tokens.Count);
        for (var index = 0; index < tokens.Count; index++)
        {
            if (tokens[index] == DecoderFlag && index + 1 < tokens.Count && tokens[index + 1] == HardwareDecoder)
            { index++; continue; }
            result.Add(tokens[index]);
        }
        return result.ToArray();
    }

    internal static ApplyResult Apply(string path, bool wantHardwareDecoder)
    {
        string[] current;
        var existed = true;
        try { current = Tokenize(File.ReadAllText(path, Encoding.UTF8)); }
        catch (FileNotFoundException) { current = DefaultTokens; existed = false; }
        catch (DirectoryNotFoundException) { current = DefaultTokens; existed = false; }
        if (current.Length == 0) current = DefaultTokens; // The wrapper also falls back to its defaults.
        var desired = wantHardwareDecoder ? WithHardwareDecoder(current) : WithoutHardwareDecoder(current);
        if (existed && desired.SequenceEqual(current)) return new(false, false, current);

        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var backedUp = false;
        if (existed && !File.Exists(BackupPath(path)))
        {
            File.Copy(path, BackupPath(path)); // Only the pristine pre-iDock file, never overwritten later.
            backedUp = true;
        }
        var temporary = Path.Combine(directory, ".arguments-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temporary, string.Join(' ', desired), new UTF8Encoding(false));
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
        return new(true, backedUp, desired);
    }
}
