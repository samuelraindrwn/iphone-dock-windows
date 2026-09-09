using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace iDock;

internal readonly record struct ScreenshotRectangle(int Left, int Top, int Right, int Bottom)
{
    internal long Width => (long)Right - Left;
    internal long Height => (long)Bottom - Top;
}

// Captures one explicitly requested, app-owned renderer. Never uses a desktop/DC
// capture fallback: another window covering the phone must not leak into the PNG.
// Windows owns the capture border and content-protection policy; neither is bypassed.
internal static class ScreenshotCapture
{
    internal static readonly TimeSpan Timeout = TimeSpan.FromSeconds(8);
    internal const long MaximumPixels = 16_777_216;

    internal static Task<BitmapSource> CaptureAsync(ReceiverWindow target, Func<uint, bool> ownsProcess,
        CancellationToken cancellationToken = default) => Task.Run(async () =>
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(Timeout);
        try { return await CaptureCoreAsync(target, ownsProcess, deadline.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw Error("Screenshot.Timeout"); }
        catch (Exception ex) when (ex is COMException or Win32Exception or NotSupportedException)
        { throw UiText.TagException(new InvalidOperationException("Window capture is unavailable.", ex), "Screenshot.Unavailable"); }
    }, cancellationToken);

    private static async Task<BitmapSource> CaptureCoreAsync(ReceiverWindow target, Func<uint, bool> ownsProcess,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateLiveTarget(target, ownsProcess);
        if (!GraphicsCaptureSession.IsSupported()) throw Error("Screenshot.Unsupported");
        var item = CreateWindowItem(target.Handle);
        var size = item.Size;
        ValidateDimensions(size.Width, size.Height);
        var originalCrop = ReadCrop(target, size.Width, size.Height);
        using var device = CreateDevice();
        using var pool = Direct3D11CaptureFramePool.CreateFreeThreaded(device,
            DirectXPixelFormat.B8G8R8A8UIntNormalized, 1, size);
        using var session = pool.CreateCaptureSession(item);
        session.IsCursorCaptureEnabled = false; // The Windows cursor is not phone content.

        var ready = new TaskCompletionSource<Direct3D11CaptureFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new object();
        var accepting = true;
        Direct3D11CaptureFrame? frame = null;
        void FrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            lock (gate)
            {
                if (!accepting) return;
                try
                {
                    var next = sender.TryGetNextFrame();
                    if (next is not null && !ready.TrySetResult(next)) next.Dispose();
                }
                catch (Exception ex) { ready.TrySetException(ex); }
            }
        }
        pool.FrameArrived += FrameArrived;
        try
        {
            ValidateLiveTarget(target, ownsProcess);
            session.StartCapture();
            frame = await ready.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (frame.ContentSize.Width != size.Width || frame.ContentSize.Height != size.Height)
                throw Error("Screenshot.Resized");
            ValidateLiveTarget(target, ownsProcess);
            if (ReadCrop(target, size.Width, size.Height) != originalCrop) throw Error("Screenshot.Resized");

            // Deep-copy while the frame remains checked out of its pool. No Direct3D
            // pointer escapes its lifetime; no third-party graphics package is needed.
            using var copied = await SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface)
                .AsTask(cancellationToken).ConfigureAwait(false);
            using var bitmap = SoftwareBitmap.Convert(copied, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);
            if (bitmap.PixelWidth != size.Width || bitmap.PixelHeight != size.Height)
                throw Error("Screenshot.Resized");
            var stride = checked(bitmap.PixelWidth * 4);
            var bytes = new byte[checked(stride * bitmap.PixelHeight)];
            var buffer = new Windows.Storage.Streams.Buffer((uint)bytes.Length);
            bitmap.CopyToBuffer(buffer);
            using (var reader = DataReader.FromBuffer(buffer)) reader.ReadBytes(bytes);
            cancellationToken.ThrowIfCancellationRequested();
            ValidateLiveTarget(target, ownsProcess);
            if (ReadCrop(target, size.Width, size.Height) != originalCrop) throw Error("Screenshot.Resized");
            var source = BitmapSource.Create(bitmap.PixelWidth, bitmap.PixelHeight, 96, 96,
                PixelFormats.Bgra32, null, bytes, stride);
            var cropped = new CroppedBitmap(source, originalCrop);
            cropped.Freeze();
            return cropped;
        }
        finally
        {
            lock (gate)
            {
                accepting = false;
                // Cancellation can race the first callback. Dispose even a completed
                // frame that WaitAsync did not hand back to this method.
                if (frame is null && ready.Task.IsCompletedSuccessfully) ready.Task.Result.Dispose();
            }
            pool.FrameArrived -= FrameArrived;
            frame?.Dispose();
        }
    }

    internal static Int32Rect CalculateClientCrop(int frameWidth, int frameHeight,
        ScreenshotRectangle extendedFrame, ScreenshotRectangle windowFrame, ScreenshotRectangle client)
    {
        ValidateDimensions(frameWidth, frameHeight);
        // WGC normally uses DWM's visible bounds, not GetWindowRect's invisible
        // resize borders. Accept an exact physical-pixel match; never guess a crop.
        var bounds = extendedFrame.Width == frameWidth && extendedFrame.Height == frameHeight
            ? extendedFrame : windowFrame;
        if (bounds.Width != frameWidth || bounds.Height != frameHeight || client.Width <= 0 || client.Height <= 0 ||
            client.Left < bounds.Left || client.Top < bounds.Top || client.Right > bounds.Right || client.Bottom > bounds.Bottom)
            throw Error("Screenshot.Resized");
        return new Int32Rect(checked(client.Left - bounds.Left), checked(client.Top - bounds.Top),
            checked((int)client.Width), checked((int)client.Height));
    }

    internal static void ValidateDimensions(int width, int height)
    {
        if (width <= 0 || height <= 0 || (long)width * height > MaximumPixels)
        {
            var error = Error("Screenshot.InvalidSize");
            error.Data["iDock.CaptureSize"] = $"{width}x{height}";
            throw error;
        }
    }

    internal static void ValidateCandidate(ReceiverWindow target, Func<uint, bool> ownsProcess)
    {
        if (!MirrorWindowLifecycle.IsVideoWindow(target) || !ownsProcess(target.ProcessId))
            throw Error("Screenshot.NoWindow");
        if (!target.Visible || target.Minimized) throw Error("Screenshot.RestoreWindow");
    }

    private static void ValidateLiveTarget(ReceiverWindow target, Func<uint, bool> ownsProcess)
    {
        ValidateCandidate(target, ownsProcess);
        if (!IsWindow(target.Handle) || GetWindowThreadProcessId(target.Handle, out var pid) == 0 || pid != target.ProcessId)
            throw Error("Screenshot.NoWindow");
        var name = new StringBuilder(256);
        var title = new StringBuilder(256);
        if (GetClassName(target.Handle, name, name.Capacity) == 0 || GetWindowText(target.Handle, title, title.Capacity) == 0)
            throw Error("Screenshot.NoWindow");
        var current = new ReceiverWindow(target.Handle, pid, name.ToString(), title.ToString(),
            IsWindowVisible(target.Handle), IsIconic(target.Handle));
        ValidateCandidate(current, ownsProcess);
        if (current.ClassName != target.ClassName || GetWindowThreadProcessId(target.Handle, out var currentId) == 0 || currentId != pid)
            throw Error("Screenshot.NoWindow");
        Marshal.ThrowExceptionForHR(DwmGetWindowAttribute(target.Handle, 14, out int cloaked, sizeof(int)));
        if (cloaked != 0) throw Error("Screenshot.RestoreWindow");
    }

    private static Int32Rect ReadCrop(ReceiverWindow target, int frameWidth, int frameHeight)
    {
        // GetWindowRect is DPI-virtualized unless this synchronous native read uses
        // physical coordinates. Always restore the previous calling-thread context.
        var previous = SetThreadDpiAwarenessContext(-4); // PER_MONITOR_AWARE_V2
        if (previous == 0) throw new Win32Exception();
        try
        {
            if (!GetWindowRect(target.Handle, out var window) || !GetClientRect(target.Handle, out var client))
                throw new Win32Exception();
            var origin = new PointNative();
            if (!ClientToScreen(target.Handle, ref origin)) throw new Win32Exception();
            Marshal.ThrowExceptionForHR(DwmGetWindowAttribute(target.Handle, 9, out RectangleNative extended, Marshal.SizeOf<RectangleNative>()));
            return CalculateClientCrop(frameWidth, frameHeight, extended.Value, window.Value,
                new ScreenshotRectangle(origin.X, origin.Y, checked(origin.X + client.Right - client.Left),
                    checked(origin.Y + client.Bottom - client.Top)));
        }
        finally { SetThreadDpiAwarenessContext(previous); }
    }

    private static GraphicsCaptureItem CreateWindowItem(nint window)
    {
        const string className = "Windows.Graphics.Capture.GraphicsCaptureItem";
        Marshal.ThrowExceptionForHR(WindowsCreateString(className, (uint)className.Length, out var name));
        nint factory = 0, item = 0;
        object? instance = null;
        try
        {
            var factoryId = typeof(IGraphicsCaptureItemInterop).GUID;
            Marshal.ThrowExceptionForHR(RoGetActivationFactory(name, ref factoryId, out factory));
            instance = Marshal.GetObjectForIUnknown(factory);
            var itemId = new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760");
            Marshal.ThrowExceptionForHR(((IGraphicsCaptureItemInterop)instance).CreateForWindow(window, ref itemId, out item));
            return WinRT.MarshalInspectable<GraphicsCaptureItem>.FromAbi(item);
        }
        finally
        {
            if (item != 0) Marshal.Release(item);
            if (instance is not null && Marshal.IsComObject(instance)) Marshal.ReleaseComObject(instance);
            if (factory != 0) Marshal.Release(factory);
            WindowsDeleteString(name);
        }
    }

    private static IDirect3DDevice CreateDevice()
    {
        nint device = 0, context = 0, dxgi = 0, inspectable = 0;
        try
        {
            // BGRA support, standard hardware driver. Failure is surfaced rather than
            // using unrelated desktop capture or changing the user's graphics driver.
            Marshal.ThrowExceptionForHR(D3D11CreateDevice(0, 1, 0, 0x20, 0, 0, 7, out device, out _, out context));
            var dxgiId = new Guid("54EC77FA-1377-44E6-8C32-88FD5F44C84C");
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(device, in dxgiId, out dxgi));
            Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgi, out inspectable));
            return WinRT.MarshalInterface<IDirect3DDevice>.FromAbi(inspectable);
        }
        finally
        {
            if (inspectable != 0) Marshal.Release(inspectable);
            if (dxgi != 0) Marshal.Release(dxgi);
            if (context != 0) Marshal.Release(context);
            if (device != 0) Marshal.Release(device);
        }
    }

    private static InvalidOperationException Error(string key) => UiText.TagException(new InvalidOperationException(key), key);

    [StructLayout(LayoutKind.Sequential)] private struct PointNative { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct RectangleNative
    {
        public int Left, Top, Right, Bottom;
        public readonly ScreenshotRectangle Value => new(Left, Top, Right, Bottom);
    }
    [ComImport, Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        [PreserveSig] int CreateForWindow(nint window, ref Guid iid, out nint item);
        [PreserveSig] int CreateForMonitor(nint monitor, ref Guid iid, out nint item);
    }
    [DllImport("combase.dll", CharSet = CharSet.Unicode)] private static extern int WindowsCreateString(string source, uint length, out nint value);
    [DllImport("combase.dll")] private static extern int WindowsDeleteString(nint value);
    [DllImport("combase.dll")] private static extern int RoGetActivationFactory(nint className, ref Guid iid, out nint factory);
    [DllImport("d3d11.dll")] private static extern int D3D11CreateDevice(nint adapter, uint driverType, nint software, uint flags,
        nint featureLevels, uint featureLevelCount, uint sdkVersion, out nint device, out uint chosenLevel, out nint immediateContext);
    [DllImport("d3d11.dll")] private static extern int CreateDirect3D11DeviceFromDXGIDevice(nint device, out nint inspectable);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetClassNameW")] private static extern int GetClassName(nint window, StringBuilder text, int capacity);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetWindowTextW")] private static extern int GetWindowText(nint window, StringBuilder text, int capacity);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(nint window, out RectangleNative rect);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetClientRect(nint window, out RectangleNative rect);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ClientToScreen(nint window, ref PointNative point);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetThreadDpiAwarenessContext(nint context);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint window, uint attribute, out RectangleNative value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint window, uint attribute, out int value, int size);
}
