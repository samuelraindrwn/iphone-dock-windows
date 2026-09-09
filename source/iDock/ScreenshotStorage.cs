using System.IO;
using System.Windows.Media.Imaging;

namespace iDock;

internal static class ScreenshotStorage
{
    internal static string DefaultDirectory => Path.Combine(UserStorage.Root, "data", "screenshots");

    internal static string Save(BitmapSource image, string directory, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(directory);
        var id = Guid.NewGuid().ToString("N");
        var result = Path.Combine(directory, $"iDock-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{id[..8]}.png");
        var temporary = Path.Combine(directory, ".screenshot-" + id + ".tmp");
        var ownsTemporary = false;
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                ownsTemporary = true;
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                encoder.Save(file);
                file.Flush(true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, result); // Never overwrite another capture.
            return result;
        }
        finally
        {
            if (ownsTemporary)
            {
                try { File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
