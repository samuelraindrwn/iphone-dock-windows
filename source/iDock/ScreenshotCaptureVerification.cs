using System.Windows;

namespace iDock;

internal static class ScreenshotCaptureVerification
{
    internal static void Run(Action<bool, string> check)
    {
        bool Reject(Action action, string key)
        {
            try { action(); return false; }
            catch (InvalidOperationException ex) { return Equals(ex.Data["iDock.TextKey"], key); }
        }
        var video = new ReceiverWindow(101, 42, "GSTD3D11", "AirPlay Video Stream", true, false);
        check(Reject(() => ScreenshotCapture.ValidateCandidate(video, _ => false), "Screenshot.NoWindow"), "Screenshot refuses a same-title process outside the owned job");
        var queried = false;
        check(Reject(() => ScreenshotCapture.ValidateCandidate(video with { ClassName = "QtWindow" }, _ => { queried = true; return true; }), "Screenshot.NoWindow") && !queried,
            "Screenshot rejects non-video windows before ownership probing");
        check(Reject(() => ScreenshotCapture.ValidateCandidate(video with { Handle = 0 }, _ => true), "Screenshot.NoWindow"), "Screenshot refuses invalid renderer handles");
        check(Reject(() => ScreenshotCapture.ValidateCandidate(video with { Minimized = true }, _ => true), "Screenshot.RestoreWindow"), "Screenshot refuses minimized stale frames");
        check(Reject(() => ScreenshotCapture.ValidateCandidate(video with { Visible = false }, _ => true), "Screenshot.RestoreWindow"), "Screenshot refuses hidden renderer frames");
        ScreenshotCapture.ValidateCandidate(video, pid => pid == 42);
        check(true, "Screenshot candidate validation accepts the exact renderer in the owned job");
        ScreenshotCapture.ValidateCandidate(video with { ClassName = "GstD3D12Hwnd", Title = "Direct3D12 Renderer" }, pid => pid == 42);
        check(true, "Screenshot candidate validation supports the D3D12 renderer before its title changes");

        var dwm = new ScreenshotRectangle(100, 100, 500, 900);
        var outer = new ScreenshotRectangle(93, 100, 507, 907);
        var client = new ScreenshotRectangle(101, 131, 499, 899);
        check(ScreenshotCapture.CalculateClientCrop(400, 800, dwm, outer, client) == new Int32Rect(1, 31, 398, 768),
            "Screenshot removes the titlebar and borders using DWM visible-frame pixels");
        check(ScreenshotCapture.CalculateClientCrop(414, 807, dwm, outer, client) == new Int32Rect(8, 31, 398, 768),
            "Screenshot handles an exact full-window frame without guessing border offsets");
        var leftMonitor = new ScreenshotRectangle(-2000, -100, -1200, 300);
        check(ScreenshotCapture.CalculateClientCrop(800, 400, leftMonitor, leftMonitor,
            new ScreenshotRectangle(-1998, -68, -1202, 298)) == new Int32Rect(2, 32, 796, 366),
            "Landscape screenshot crop supports negative multi-monitor coordinates");
        check(Reject(() => ScreenshotCapture.CalculateClientCrop(401, 800, dwm, outer, client), "Screenshot.Resized"),
            "Screenshot refuses mismatched frame sizes during rotation or resizing");
        check(Reject(() => ScreenshotCapture.CalculateClientCrop(400, 800, dwm, outer,
            client with { Left = 99 }), "Screenshot.Resized"), "Screenshot refuses client pixels outside the captured window");
        check(Reject(() => ScreenshotCapture.CalculateClientCrop(400, 800, dwm, outer,
            client with { Right = client.Left }), "Screenshot.Resized"), "Screenshot refuses an empty client area");
        check(Reject(() => ScreenshotCapture.ValidateDimensions(0, 100), "Screenshot.InvalidSize"), "Screenshot refuses zero-sized frames");
        check(Reject(() => ScreenshotCapture.ValidateDimensions(-1, -1), "Screenshot.InvalidSize"), "Screenshot refuses negative frame dimensions");
        check(Reject(() => ScreenshotCapture.ValidateDimensions(int.MaxValue, int.MaxValue), "Screenshot.InvalidSize"),
            "Screenshot dimensions cannot overflow into an unbounded pixel allocation");
        ScreenshotCapture.ValidateDimensions(4096, 4096);
        check(ScreenshotCapture.MaximumPixels == 4096L * 4096 && ScreenshotCapture.Timeout <= TimeSpan.FromSeconds(10),
            "Screenshot bounds memory and frame waiting without touching a device");
    }
}
