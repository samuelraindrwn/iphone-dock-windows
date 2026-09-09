using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace iDock;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.FirstOrDefault() == "--screenshot-test")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var report = e.Args.Length > 1 ? e.Args[1] : throw new ArgumentException("An isolated screenshot test report path is required.");
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                try { Shutdown(await ScreenshotNativeVerification.RunAsync(report)); }
                catch (Exception ex) { File.WriteAllText(report, ex.ToString()); Shutdown(1); }
            }));
            return;
        }
        if (e.Args.FirstOrDefault() is "--child-wait" or "--child-spawn" or "--child-env")
        {
            if (e.Args[0] == "--child-env")
                File.WriteAllText(e.Args[1], Environment.GetEnvironmentVariable("IDOCK_TEST_CHILD_ENV") ?? "missing");
            if (e.Args[0] == "--child-spawn")
            {
                using var child = Process.Start(EngineManager.StartInfo(Environment.ProcessPath!, "--child-wait"))!;
                File.WriteAllText(e.Args[1], child.Id.ToString());
            }
            using var never = new ManualResetEvent(false);
            never.WaitOne();
            Shutdown();
            return;
        }
        if (e.Args.FirstOrDefault() == "--self-test")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var output = e.Args.Length > 1 ? e.Args[1] : "self-test.txt";
            try { File.WriteAllText(output, Verification.Run()); Shutdown(0); }
            catch (Exception ex) { File.WriteAllText(output, ex.ToString()); Shutdown(1); }
            return;
        }
        if (e.Args.FirstOrDefault() == "--close-test")
        {
            var report = e.Args[1];
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            DispatcherUnhandledException += (_, args) =>
            {
                File.WriteAllText(report, args.Exception.ToString());
                args.Handled = true;
                Shutdown(1);
            };
            var testWindow = new MainWindow
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -20000, Top = -20000, ShowActivated = false, ShowInTaskbar = false
            };
            testWindow.Closed += (_, _) =>
            {
                File.WriteAllText(report, "PASS Real WPF window closes without reentrant Close / modal error.\n");
                Shutdown(0);
            };
            testWindow.Show();
            Dispatcher.BeginInvoke(new Action(testWindow.Close));
            return;
        }
        if (e.Args.FirstOrDefault() == "--preview-hotkey")
        {
            // Renders the shortcut dialog from an isolated settings file, so a preview
            // never reads or writes the current user's saved combination.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            UiText.SelectLanguage(e.Args.Length > 4 ? e.Args[4] : "id");
            var isolated = Path.Combine(Path.GetTempPath(), "idock-preview-" + Guid.NewGuid().ToString("N"),
                "hotkey-settings.json");
            var dialog = new HotkeyWindow(isolated, controlRunning: false)
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -20000, Top = -20000, ShowActivated = false, ShowInTaskbar = false
            };
            var dialogWidth = e.Args.Length > 3 ? int.Parse(e.Args[3]) : 560;
            var dialogHeight = e.Args.Length > 2 ? int.Parse(e.Args[2]) : 470;
            dialog.Width = dialogWidth;
            dialog.Height = dialogHeight;
            dialog.Show();
            dialog.UpdateLayout();
            Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
            dialog.UpdateLayout();
            var dialogContent = (FrameworkElement)dialog.Content;
            dialog.Content = null;
            var dialogSurface = new Border
            {
                Background = dialog.Background,
                Child = new ContentControl
                {
                    Content = dialogContent, Resources = dialog.Resources, Foreground = dialog.Foreground,
                    FontFamily = dialog.FontFamily, FontSize = dialog.FontSize,
                    Width = dialogWidth, Height = dialogHeight
                }
            };
            SaveElementPng(dialogSurface, dialogWidth, dialogHeight, e.Args[1]);
            dialog.Close();
            Shutdown();
            return;
        }
        Environment.SetEnvironmentVariable("BLEHID_DATA_DIR", Path.Combine(UserStorage.Root, "data", "blehid"));
        if (e.Args.FirstOrDefault() == "--preview")
        {
            // A preview chooses its own language, never reading or saving user preferences.
            UiText.SelectLanguage(e.Args.Length > 4 ? e.Args[4] : "id");
        }
        var window = new MainWindow(preview: e.Args.FirstOrDefault() == "--preview",
            enableGlobalShortcuts: e.Args.Length == 0);
        MainWindow = window;
        if (e.Args.FirstOrDefault() == "--preview")
        {
            // Documentation previews must not disclose the developer's real device name.
            window.DeviceNameLabel.Text = "WINDOWS-LAPTOP";
            var height = e.Args.Length > 2 ? int.Parse(e.Args[2]) : 900;
            var width = e.Args.Length > 3 ? int.Parse(e.Args[3]) : 1100;
            if (width < 900 || width > 3000 || height < 600 || height > 4000)
                throw new ArgumentOutOfRangeException(nameof(e), "Preview dimensions must fit the supported desktop range.");
            var size = new Size(width, height);
            var content = window.Content;
            window.Content = null;
            var control = new ContentControl
            {
                Content = content, Resources = window.Resources, Background = window.Background,
                Foreground = window.Foreground, FontFamily = window.FontFamily, FontSize = window.FontSize,
                Width = size.Width, Height = size.Height
            };
            var surface = new Border { Background = window.Background, Child = control };
            surface.Measure(size);
            surface.Arrange(new Rect(size));
            surface.UpdateLayout();
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(surface);
            var png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(e.Args[1]);
            png.Save(file);
            Shutdown();
            return;
        }
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(UiText.ResolveException(args.Exception), ProductInfo.DisplayName, MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        window.Show();
    }

    private static void SaveElementPng(FrameworkElement element, int width, int height, string path)
    {
        var size = new Size(width, height);
        element.Measure(size);
        element.Arrange(new Rect(size));
        element.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        png.Save(file);
    }
}
