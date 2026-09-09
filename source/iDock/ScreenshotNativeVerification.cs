using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media.Imaging;

namespace iDock;

// Explicit opt-in integration check, not part of hardware-free --self-test. Shows
// and closes only synthetic windows from this process, without activating them.
// The blue renderer is deliberately covered by a red fixture to catch accidental
// desktop capture; neither phone content nor other applications are targeted.
internal static class ScreenshotNativeVerification
{
    private const string VideoClass = "GSTD3D11";
    private const string CoverClass = "iDockScreenshotVerificationCover";
    private static readonly WindowProcedure procedure = WndProc;
    private static nint videoBrush, coverBrush;

    internal static async Task<int> RunAsync(string reportPath)
    {
        var lines = new List<string>();
        nint video = 0, cover = 0;
        var videoRegistered = false;
        var coverRegistered = false;
        var instance = GetModuleHandle(null);
        try
        {
            videoBrush = CreateSolidBrush(0x00B46414); // RGB(20, 100, 180)
            coverBrush = CreateSolidBrush(0x001414DC); // RGB(220, 20, 20)
            if (videoBrush == 0 || coverBrush == 0) throw new Win32Exception();
            Register(VideoClass, instance, videoBrush);
            videoRegistered = true;
            Register(CoverClass, instance, coverBrush);
            coverRegistered = true;
            video = CreateWindowEx(0x08000000, VideoClass, "AirPlay Video Stream", 0x00CF0000,
                60, 60, 420, 560, 0, 0, instance, 0); // NOACTIVATE / OVERLAPPEDWINDOW
            if (video == 0) throw new Win32Exception();
            ShowWindow(video, 4); // SW_SHOWNOACTIVATE
            UpdateWindow(video);
            cover = CreateWindowEx(0x08000000, CoverClass, "iDock capture verification (occluder)", 0x00800000,
                100, 150, 240, 240, 0, 0, instance, 0);
            if (cover == 0) throw new Win32Exception();
            if (!SetWindowPos(cover, -1, 100, 150, 240, 240, 0x0050)) throw new Win32Exception();
            UpdateWindow(cover);
            await Task.Delay(250);
            Marshal.ThrowExceptionForHR(DwmFlush());
            GetWindowRect(video, out var windowBounds);
            GetClientRect(video, out var clientBounds);
            lines.Add($"Fixture: window {windowBounds.Right - windowBounds.Left}x{windowBounds.Bottom - windowBounds.Top}; client {clientBounds.Right}x{clientBounds.Bottom}; visible={IsWindowVisible(video)}; interactive={Environment.UserInteractive}");
            lines.Add("Fixture desktop: " + DesktopName(GetThreadDesktop(GetCurrentThreadId())));
            var inputDesktop = OpenInputDesktop(0, false, 1);
            try { lines.Add("Input desktop: " + (inputDesktop == 0 ? $"unreadable ({Marshal.GetLastWin32Error()})" : DesktopName(inputDesktop))); }
            finally { if (inputDesktop != 0) CloseDesktop(inputDesktop); }

            var target = new ReceiverWindow(video, (uint)Environment.ProcessId, VideoClass,
                "AirPlay Video Stream", true, false);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(9));
            var bitmap = await ScreenshotCapture.CaptureAsync(target, pid => pid == Environment.ProcessId, deadline.Token);
            var previousDpi = SetThreadDpiAwarenessContext(-4);
            if (previousDpi == 0) throw new Win32Exception();
            Rectangle client;
            try { if (!GetClientRect(video, out client)) throw new Win32Exception(); }
            finally { SetThreadDpiAwarenessContext(previousDpi); }
            Check(bitmap.PixelWidth == client.Right && bitmap.PixelHeight == client.Bottom,
                "Native capture crops to the exact synthetic client dimensions, excluding titlebar", lines);
            var stride = checked(bitmap.PixelWidth * 4);
            var pixels = new byte[checked(stride * bitmap.PixelHeight)];
            bitmap.CopyPixels(pixels, stride, 0);
            var correct = 0;
            for (var index = 0; index < pixels.Length; index += 4)
                if (Math.Abs(pixels[index] - 180) <= 2 && Math.Abs(pixels[index + 1] - 100) <= 2 &&
                    Math.Abs(pixels[index + 2] - 20) <= 2 && pixels[index + 3] == 255) correct++;
            Check(correct >= (long)bitmap.PixelWidth * bitmap.PixelHeight * 99 / 100,
                "Captured pixels come from the blue renderer, not the red occluder or desktop", lines);
            using var encoded = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(encoded);
            encoded.Position = 0;
            var decoded = new PngBitmapDecoder(encoded, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            Check(decoded.Frames.Count == 1 && decoded.Frames[0].PixelWidth == bitmap.PixelWidth &&
                decoded.Frames[0].PixelHeight == bitmap.PixelHeight,
                "Captured client pixels round-trip through a valid PNG", lines);
            ShowWindow(video, 6); // SW_MINIMIZE
            var rejected = false;
            try { await ScreenshotCapture.CaptureAsync(target, pid => pid == Environment.ProcessId, deadline.Token); }
            catch (InvalidOperationException ex) { rejected = Equals(ex.Data["iDock.TextKey"], "Screenshot.RestoreWindow"); }
            Check(rejected, "Native screenshot refuses a renderer minimized after target selection", lines);
            lines.Add("Native screenshot integration: PASS (4 checks). No phone, receiver, or Bluetooth session was opened.");
            return 0;
        }
        catch (Exception ex)
        {
            lines.Add("FAIL " + ex);
            foreach (System.Collections.DictionaryEntry detail in ex.Data)
                lines.Add($"Diagnostic {detail.Key}: {detail.Value}");
            return 1;
        }
        finally
        {
            if (cover != 0) DestroyWindow(cover);
            if (video != 0) DestroyWindow(video);
            if (coverRegistered) UnregisterClass(CoverClass, instance);
            if (videoRegistered) UnregisterClass(VideoClass, instance);
            if (coverBrush != 0) DeleteObject(coverBrush);
            if (videoBrush != 0) DeleteObject(videoBrush);
            coverBrush = videoBrush = 0;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
            File.WriteAllLines(reportPath, lines);
        }
    }

    private static void Check(bool condition, string label, List<string> lines)
    {
        if (!condition) throw new InvalidOperationException(label);
        lines.Add("PASS " + label);
    }

    private static string DesktopName(nint desktop)
    {
        var name = new StringBuilder(256);
        return GetUserObjectInformation(desktop, 2, name, name.Capacity * 2, out _)
            ? name.ToString() : $"unreadable ({Marshal.GetLastWin32Error()})";
    }

    private static void Register(string name, nint instance, nint brush)
    {
        var definition = new WindowClass { Size = (uint)Marshal.SizeOf<WindowClass>(),
            Procedure = procedure, Instance = instance, Background = brush, Name = name };
        if (RegisterClassEx(ref definition) == 0) throw new Win32Exception();
    }

    private static nint WndProc(nint window, uint message, nint wParam, nint lParam) =>
        DefWindowProc(window, message, wParam, lParam);

    private delegate nint WindowProcedure(nint window, uint message, nint wParam, nint lParam);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct WindowClass
    {
        public uint Size, Style;
        public WindowProcedure Procedure;
        public int ClassExtra, WindowExtra;
        public nint Instance, Icon, Cursor, Background;
        public string? MenuName;
        public string Name;
        public nint SmallIcon;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Rectangle { public int Left, Top, Right, Bottom; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegisterClassExW", SetLastError = true)] private static extern ushort RegisterClassEx(ref WindowClass definition);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "UnregisterClassW")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnregisterClass(string name, nint instance);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateWindowExW", SetLastError = true)] private static extern nint CreateWindowEx(uint extendedStyle, string className, string title, uint style,
        int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "DefWindowProcW")] private static extern nint DefWindowProc(nint window, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShowWindow(nint window, int command);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UpdateWindow(nint window);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetClientRect(nint window, out Rectangle rect);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(nint window, out Rectangle rect);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetThreadDpiAwarenessContext(nint context);
    [DllImport("gdi32.dll")] private static extern nint CreateSolidBrush(uint color);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteObject(nint value);
    [DllImport("dwmapi.dll")] private static extern int DwmFlush();
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern nint GetThreadDesktop(uint threadId);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint OpenInputDesktop(uint flags, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint access);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseDesktop(nint desktop);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetUserObjectInformationW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetUserObjectInformation(nint handle, int index, StringBuilder value, int length, out uint needed);
}
