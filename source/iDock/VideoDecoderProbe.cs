using System.Runtime.InteropServices;

namespace iDock;

// Asks Direct3D 11 the same question GStreamer's d3d11 plugin asks before it registers
// d3d11h264dec: is there a hardware H.264 VLD decoder profile with NV12 output? A
// negative or failed answer means the software decoder stays in use; nothing is rendered.
internal static class VideoDecoderProbe
{
    internal readonly record struct ProbeResult(bool Supported, string Detail);

    private static readonly Guid H264VldNoFgt = new("1B81BE68-A0C7-11D3-B984-00C04F2E73C5");
    private static readonly Guid VideoDeviceId = new("10EC4D5B-975A-4689-B9E4-D0AAC30FE333");
    private const int DriverTypeHardware = 1;
    private const uint BgraSupport = 0x20, VideoSupport = 0x800, SdkVersion = 7, FormatNv12 = 103;

    internal static ProbeResult Run()
    {
        var device = IntPtr.Zero;
        var context = IntPtr.Zero;
        try
        {
            var result = D3D11CreateDevice(IntPtr.Zero, DriverTypeHardware, IntPtr.Zero, BgraSupport | VideoSupport,
                IntPtr.Zero, 0, SdkVersion, out device, out _, out context);
            if (result < 0)
                result = D3D11CreateDevice(IntPtr.Zero, DriverTypeHardware, IntPtr.Zero, BgraSupport,
                    IntPtr.Zero, 0, SdkVersion, out device, out _, out context);
            if (result < 0) return new(false, $"D3D11CreateDevice 0x{result:X8}");
            var videoId = VideoDeviceId;
            if (Marshal.QueryInterface(device, in videoId, out var videoPointer) < 0 || videoPointer == IntPtr.Zero)
                return new(false, "ID3D11VideoDevice unavailable");
            try
            {
                var video = (ID3D11VideoDevice)Marshal.GetObjectForIUnknown(videoPointer);
                try
                {
                    var count = video.GetVideoDecoderProfileCount();
                    var found = false;
                    for (uint index = 0; index < count && !found; index++)
                        found = video.GetVideoDecoderProfile(index, out var profile) == 0 && profile == H264VldNoFgt;
                    if (!found) return new(false, $"no H.264 VLD profile among {count}");
                    var wanted = H264VldNoFgt;
                    if (video.CheckVideoDecoderFormat(ref wanted, FormatNv12, out var supported) < 0 || supported == 0)
                        return new(false, "H.264 VLD without NV12 output");
                    return new(true, $"H.264 VLD + NV12 ({count} decoder profiles)");
                }
                finally { Marshal.FinalReleaseComObject(video); }
            }
            finally { Marshal.Release(videoPointer); }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or COMException
            or InvalidCastException or AccessViolationException)
        { return new(false, ex.GetType().Name + ": " + ex.Message); }
        finally
        {
            if (context != IntPtr.Zero) Marshal.Release(context);
            if (device != IntPtr.Zero) Marshal.Release(device);
        }
    }

    [DllImport("d3d11.dll")]
    private static extern int D3D11CreateDevice(IntPtr adapter, int driverType, IntPtr software, uint flags,
        IntPtr featureLevels, uint featureLevelCount, uint sdkVersion, out IntPtr device, out int featureLevel, out IntPtr context);

    // Only the slots up to CheckVideoDecoderFormat are declared; nothing later is called.
    [ComImport, Guid("10EC4D5B-975A-4689-B9E4-D0AAC30FE333"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ID3D11VideoDevice
    {
        [PreserveSig] int CreateVideoDecoder(IntPtr description, IntPtr configuration, out IntPtr decoder);
        [PreserveSig] int CreateVideoProcessor(IntPtr enumerator, uint rateConversionIndex, out IntPtr processor);
        [PreserveSig] int CreateAuthenticatedChannel(int channelType, out IntPtr channel);
        [PreserveSig] int CreateCryptoSession(IntPtr cryptoType, IntPtr decoderProfile, IntPtr keyExchangeType, out IntPtr session);
        [PreserveSig] int CreateVideoDecoderOutputView(IntPtr resource, IntPtr description, out IntPtr view);
        [PreserveSig] int CreateVideoProcessorInputView(IntPtr resource, IntPtr enumerator, IntPtr description, out IntPtr view);
        [PreserveSig] int CreateVideoProcessorOutputView(IntPtr resource, IntPtr enumerator, IntPtr description, out IntPtr view);
        [PreserveSig] int CreateVideoProcessorEnumerator(IntPtr description, out IntPtr enumerator);
        [PreserveSig] uint GetVideoDecoderProfileCount();
        [PreserveSig] int GetVideoDecoderProfile(uint index, out Guid profile);
        [PreserveSig] int CheckVideoDecoderFormat(ref Guid profile, uint format, out int supported);
    }
}
