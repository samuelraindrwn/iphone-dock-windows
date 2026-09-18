using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace iDock;

internal enum MirrorStreamShape { Unknown, Portrait, Landscape }
internal enum MirrorRendererKind { D3D11, D3D12 }

internal readonly record struct MirrorStreamSize(int Width, int Height)
{
    internal MirrorStreamShape Shape => Width == Height ? MirrorStreamShape.Unknown
        : Width > Height ? MirrorStreamShape.Landscape : MirrorStreamShape.Portrait;

    internal bool IsUsable => MirrorWindowLayout.IsUsableStreamSize(Width, Height);
}

internal readonly record struct MirrorStreamGeometry(MirrorRendererKind Renderer, MirrorStreamSize Size);

internal readonly record struct MirrorStreamGeometrySnapshot(MirrorStreamSize? D3D11, MirrorStreamSize? D3D12)
{
    internal MirrorStreamSize? For(ReceiverWindow window) => window.ClassName switch
    {
        "GSTD3D11" => D3D11,
        "GstD3D12Hwnd" => D3D12,
        _ => null
    };
}

/// <summary>
/// Reads only GStreamer's negotiated video caps from an isolated per-session debug file.
/// No frame pixels are captured. The two narrowly enabled sink categories log on caps/lifecycle
/// changes, not once per rendered frame, and GStreamer flushes each debug line on Windows.
/// </summary>
internal sealed partial class MirrorStreamGeometrySource : IDisposable
{
    private sealed class SettledGeometry
    {
        internal MirrorStreamSize? Current;
        internal MirrorStreamSize? Pending;
        internal TimeSpan PendingSince;
    }

    private const int MaximumReadBytes = 262_144;
    private const int MaximumLineCharacters = 16_384;
    private const string DebugCategories = "*:0,d3d11videosink:5,d3d12videosink:5";
    internal static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(300);
    private readonly SettledGeometry d3d11 = new(), d3d12 = new();
    private readonly string path;
    private long offset;
    private string pendingText = "";
    private bool disposed;

    internal MirrorStreamGeometrySource(string path) => this.path = Path.GetFullPath(path);

    internal static MirrorStreamGeometrySource? TryConfigure(ProcessStartInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        // An explicit GStreamer diagnostic setup belongs to the user/developer. Do not redirect
        // or broaden it; Windowed mode can still use its safe initial client-size fallback.
        if (HasValue(info, "GST_DEBUG") || HasValue(info, "GST_DEBUG_FILE")) return null;

        try
        {
            var directory = Path.Combine(Path.GetTempPath(), "iDock", "mirror-caps");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"caps-{Guid.NewGuid():N}.log");
            info.Environment["GST_DEBUG"] = DebugCategories;
            info.Environment["GST_DEBUG_FILE"] = path;
            info.Environment["GST_DEBUG_NO_COLOR"] = "1";
            info.Environment["GST_DEBUG_COLOR_MODE"] = "off";
            return new(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException
            or System.Security.SecurityException)
        {
            // Telemetry is optional. A locked or unavailable temp directory must never prevent
            // the AirPlay receiver from opening; Windowed falls back to the initial client size.
            return null;
        }
    }

    internal MirrorStreamGeometrySnapshot ReadLatest() => ReadLatest(MonotonicNow());

    internal MirrorStreamGeometrySnapshot ReadLatest(TimeSpan now)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length < offset) ResetForTruncation();

            var available = stream.Length - offset;
            if (available > MaximumReadBytes)
            {
                // Keep memory bounded even if a compromised/misconfigured child writes more than
                // the two requested categories. Starting mid-line is safe: that fragment is ignored.
                offset = stream.Length - MaximumReadBytes;
                pendingText = "";
                available = MaximumReadBytes;
            }
            if (available > 0)
            {
                stream.Position = offset;
                var bytes = new byte[checked((int)Math.Min(available, MaximumReadBytes))];
                var read = 0;
                while (read < bytes.Length)
                {
                    var count = stream.Read(bytes, read, bytes.Length - read);
                    if (count == 0) break;
                    read += count;
                }
                offset += read;
                if (read > 0) Process(Encoding.UTF8.GetString(bytes, 0, read), now);
            }
        }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
        catch (IOException) { } // Writer startup/teardown is not a mirroring failure.
        catch (UnauthorizedAccessException) { }

        Settle(d3d11, now);
        Settle(d3d12, now);
        return new(d3d11.Current, d3d12.Current);
    }

    internal static bool TryParseCapsLine(ReadOnlySpan<char> line, out MirrorStreamGeometry geometry)
    {
        geometry = default;
        if (line.Length == 0 || line.Length > MaximumLineCharacters) return false;
        var text = line.ToString();
        MirrorRendererKind renderer;
        if (text.Contains("d3d11videosink", StringComparison.Ordinal)
            && text.Contains("gst_d3d11_video_sink_set_caps", StringComparison.Ordinal))
            renderer = MirrorRendererKind.D3D11;
        else if (text.Contains("d3d12videosink", StringComparison.Ordinal)
            && text.Contains("gst_d3d12_video_sink_set_info", StringComparison.Ordinal))
            renderer = MirrorRendererKind.D3D12;
        else return false;

        var marker = text.IndexOf("set caps video/x-raw", StringComparison.Ordinal);
        if (marker < 0) return false;
        var caps = text[marker..];
        var width = WidthPattern().Match(caps);
        var height = HeightPattern().Match(caps);
        if (!width.Success || !height.Success
            || !int.TryParse(width.Groups[1].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedWidth)
            || !int.TryParse(height.Groups[1].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedHeight))
            return false;
        var size = new MirrorStreamSize(parsedWidth, parsedHeight);
        if (!size.IsUsable || size.Shape == MirrorStreamShape.Unknown) return false;
        geometry = new(renderer, size);
        return true;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (!TryDelete(path)) _ = DeleteAfterWriterStopsAsync(path);
    }

    private static bool TryDelete(string file)
    {
        try
        {
            File.Delete(file);
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static async Task DeleteAfterWriterStopsAsync(string file)
    {
        // Closing a Job asks its processes to exit, but their GStreamer file handle can outlive
        // that call briefly. Retry only this GUID-named session file without delaying the UI.
        foreach (var delay in new[] { 50, 100, 250, 500, 1_000 })
        {
            await Task.Delay(delay).ConfigureAwait(false);
            if (TryDelete(file)) return;
        }
    }

    private void Process(string appended, TimeSpan now)
    {
        var text = pendingText + appended;
        var finalNewline = text.LastIndexOf('\n');
        if (finalNewline < 0)
        {
            pendingText = text.Length <= MaximumLineCharacters ? text : "";
            return;
        }

        pendingText = finalNewline + 1 < text.Length ? text[(finalNewline + 1)..] : "";
        if (pendingText.Length > MaximumLineCharacters) pendingText = "";
        MirrorStreamSize? newestD3D11 = null, newestD3D12 = null;
        foreach (var line in text.AsSpan(0, finalNewline + 1).EnumerateLines())
        {
            if (!TryParseCapsLine(line, out var geometry)) continue;
            if (geometry.Renderer == MirrorRendererKind.D3D11) newestD3D11 = geometry.Size;
            else newestD3D12 = geometry.Size;
        }
        if (newestD3D11 is { } eleven) Stage(d3d11, eleven, now);
        if (newestD3D12 is { } twelve) Stage(d3d12, twelve, now);
    }

    private static void Stage(SettledGeometry state, MirrorStreamSize size, TimeSpan now)
    {
        if (state.Current == size)
        {
            state.Pending = null;
            return;
        }
        if (state.Pending == size) return;
        state.Pending = size;
        state.PendingSince = now;
    }

    private static void Settle(SettledGeometry state, TimeSpan now)
    {
        if (state.Pending is not { } pending || now < state.PendingSince
            || now - state.PendingSince < SettleDelay) return;
        state.Current = pending;
        state.Pending = null;
    }

    private void ResetForTruncation()
    {
        offset = 0;
        pendingText = "";
        d3d11.Current = d3d11.Pending = null;
        d3d12.Current = d3d12.Pending = null;
    }

    private static TimeSpan MonotonicNow() =>
        TimeSpan.FromSeconds((double)Stopwatch.GetTimestamp() / Stopwatch.Frequency);

    private static bool HasValue(ProcessStartInfo info, string name) =>
        info.Environment.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value);

    [GeneratedRegex(@"(?:^|[\s,])width=\(int\)([0-9]{1,5})(?:[,\s]|$)", RegexOptions.CultureInvariant)]
    private static partial Regex WidthPattern();

    [GeneratedRegex(@"(?:^|[\s,])height=\(int\)([0-9]{1,5})(?:[,\s]|$)", RegexOptions.CultureInvariant)]
    private static partial Regex HeightPattern();
}
