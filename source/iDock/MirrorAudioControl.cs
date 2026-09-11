using System.Runtime.InteropServices;

namespace iDock;

// Per-application volume through Core Audio session control: the same control the Windows
// Volume Mixer exposes for one app. Only sessions whose process is in this session's Job are
// touched; the system volume and every other application stay as they are.
internal static class MirrorAudioControl
{
    internal readonly record struct AudioApplyResult(int Matched, int Changed);

    internal static AudioApplyResult Apply(Func<uint, bool> ownsProcess, int volumePercent, bool muted)
    {
        var level = Math.Clamp(volumePercent, MirrorSettings.MinimumVolume, MirrorSettings.MaximumVolume) / 100f;
        int matched = 0, changed = 0;
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        try
        {
            Check(enumerator.EnumAudioEndpoints(DataFlowRender, DeviceStateActive, out var devices));
            try
            {
                Check(devices.GetCount(out var deviceCount));
                for (uint index = 0; index < deviceCount; index++)
                {
                    if (devices.Item(index, out var device) < 0) continue;
                    try { ApplyToDevice(device, ownsProcess, level, muted, ref matched, ref changed); }
                    finally { Marshal.FinalReleaseComObject(device); }
                }
            }
            finally { Marshal.FinalReleaseComObject(devices); }
        }
        finally { Marshal.FinalReleaseComObject(enumerator); }
        return new(matched, changed);
    }

    private static void ApplyToDevice(IMMDevice device, Func<uint, bool> ownsProcess, float level, bool muted,
        ref int matched, ref int changed)
    {
        var managerId = typeof(IAudioSessionManager2).GUID;
        // A device that is disappearing can refuse activation; that is not a failure of the others.
        if (device.Activate(ref managerId, ClsctxAll, IntPtr.Zero, out var activated) < 0
            || activated is not IAudioSessionManager2 manager) return;
        try
        {
            if (manager.GetSessionEnumerator(out var sessions) < 0) return;
            try
            {
                if (sessions.GetCount(out var sessionCount) < 0) return;
                for (var index = 0; index < sessionCount; index++)
                {
                    if (sessions.GetSession(index, out var session) < 0) continue;
                    try
                    {
                        if (session is not IAudioSessionControl2 identity || identity.GetProcessId(out var processId) < 0
                            || processId == 0 || !ownsProcess(processId)) continue;
                        matched++;
                        if (session is not ISimpleAudioVolume volume) continue;
                        if (volume.GetMasterVolume(out var currentLevel) == 0 && Math.Abs(currentLevel - level) > 0.001f
                            && volume.SetMasterVolume(level, IntPtr.Zero) == 0) changed++;
                        if (volume.GetMute(out var currentMute) == 0 && currentMute != muted
                            && volume.SetMute(muted, IntPtr.Zero) == 0) changed++;
                    }
                    finally { Marshal.FinalReleaseComObject(session); }
                }
            }
            finally { Marshal.FinalReleaseComObject(sessions); }
        }
        finally { Marshal.FinalReleaseComObject(manager); }
    }

    private static void Check(int result)
    {
        if (result < 0) throw new COMException("Audio session enumeration failed.", result);
    }

    private const int DataFlowRender = 0;
    private const uint DeviceStateActive = 0x1;
    private const uint ClsctxAll = 0x17;

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumerator { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, uint stateMask, out IMMDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr client);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid interfaceId, uint context, IntPtr activationParameters,
            [MarshalAs(UnmanagedType.IUnknown)] out object activated);
        [PreserveSig] int OpenPropertyStore(uint access, out IntPtr properties);
        [PreserveSig] int GetId(out IntPtr id);
        [PreserveSig] int GetState(out uint state);
    }

    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionManager2
    {
        [PreserveSig] int GetAudioSessionControl(IntPtr sessionId, uint streamFlags, out IAudioSessionControl control);
        [PreserveSig] int GetSimpleAudioVolume(IntPtr sessionId, uint streamFlags, out ISimpleAudioVolume volume);
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator sessions);
        [PreserveSig] int RegisterSessionNotification(IntPtr notification);
        [PreserveSig] int UnregisterSessionNotification(IntPtr notification);
        [PreserveSig] int RegisterDuckNotification(IntPtr sessionId, IntPtr notification);
        [PreserveSig] int UnregisterDuckNotification(IntPtr notification);
    }

    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionEnumerator
    {
        [PreserveSig] int GetCount(out int count);
        [PreserveSig] int GetSession(int index, out IAudioSessionControl session);
    }

    [ComImport, Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl
    {
        [PreserveSig] int GetState(out int state);
        [PreserveSig] int GetDisplayName(out IntPtr name);
        [PreserveSig] int SetDisplayName(IntPtr name, IntPtr eventContext);
        [PreserveSig] int GetIconPath(out IntPtr path);
        [PreserveSig] int SetIconPath(IntPtr path, IntPtr eventContext);
        [PreserveSig] int GetGroupingParam(out Guid grouping);
        [PreserveSig] int SetGroupingParam(ref Guid grouping, IntPtr eventContext);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr notification);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr notification);
    }

    // COM vtables are flat: the base interface's slots are repeated before the extension.
    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl2
    {
        [PreserveSig] int GetState(out int state);
        [PreserveSig] int GetDisplayName(out IntPtr name);
        [PreserveSig] int SetDisplayName(IntPtr name, IntPtr eventContext);
        [PreserveSig] int GetIconPath(out IntPtr path);
        [PreserveSig] int SetIconPath(IntPtr path, IntPtr eventContext);
        [PreserveSig] int GetGroupingParam(out Guid grouping);
        [PreserveSig] int SetGroupingParam(ref Guid grouping, IntPtr eventContext);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr notification);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr notification);
        [PreserveSig] int GetSessionIdentifier(out IntPtr id);
        [PreserveSig] int GetSessionInstanceIdentifier(out IntPtr id);
        [PreserveSig] int GetProcessId(out uint processId);
        [PreserveSig] int IsSystemSoundsSession();
        [PreserveSig] int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
    }

    [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISimpleAudioVolume
    {
        [PreserveSig] int SetMasterVolume(float level, IntPtr eventContext);
        [PreserveSig] int GetMasterVolume(out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, IntPtr eventContext);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }
}
