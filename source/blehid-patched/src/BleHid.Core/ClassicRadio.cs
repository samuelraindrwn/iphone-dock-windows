using System.Runtime.InteropServices;

namespace BleHid.Core;

/// <summary>
/// Controls the BR/EDR side of the local radio. Bonded hosts see our LE peripheral and our
/// Classic identity as one device (same address) and prefer BR/EDR on reconnect, where the PC
/// has no HID device role. Closing the Classic door is the only lever WinRT does not expose.
/// </summary>
public static class ClassicRadio
{
    [StructLayout(LayoutKind.Sequential)]
    private struct BluetoothFindRadioParams
    {
        public uint dwSize;
    }

    [DllImport("bthprops.cpl", SetLastError = true)]
    private static extern IntPtr BluetoothFindFirstRadio(ref BluetoothFindRadioParams pbtfrp, out IntPtr phRadio);

    [DllImport("bthprops.cpl", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BluetoothFindRadioClose(IntPtr hFind);

    [DllImport("bthprops.cpl", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BluetoothEnableIncomingConnections(IntPtr hRadio, [MarshalAs(UnmanagedType.Bool)] bool fEnabled);

    [DllImport("bthprops.cpl", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BluetoothIsConnectable(IntPtr hRadio);

    [DllImport("bthprops.cpl", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BluetoothEnableDiscovery(IntPtr hRadio, [MarshalAs(UnmanagedType.Bool)] bool fEnabled);

    [DllImport("bthprops.cpl", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BluetoothIsDiscoverable(IntPtr hRadio);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    private static T WithRadio<T>(Func<IntPtr, T> action, T unavailable)
    {
        var parameters = new BluetoothFindRadioParams { dwSize = (uint)Marshal.SizeOf<BluetoothFindRadioParams>() };
        var find = BluetoothFindFirstRadio(ref parameters, out var radio);
        if (find == IntPtr.Zero) return unavailable;

        try
        {
            return action(radio);
        }
        finally
        {
            CloseHandle(radio);
            BluetoothFindRadioClose(find);
        }
    }

    public static bool IsConnectable => WithRadio(BluetoothIsConnectable, false);

    public static bool IsDiscoverable => WithRadio(BluetoothIsDiscoverable, false);

    /// <summary>Returns the Win32 error code, or 0 on success.</summary>
    public static int SetIncomingConnections(bool enabled)
    {
        var viaHandle = WithRadio(radio =>
            BluetoothEnableIncomingConnections(radio, enabled) ? 0 : Marshal.GetLastWin32Error(), -1);
        if (viaHandle == 0) return 0;
        // A NULL handle asks the stack to apply the change across all local radios.
        return BluetoothEnableIncomingConnections(IntPtr.Zero, enabled) ? 0 : Marshal.GetLastWin32Error();
    }

    /// <summary>Returns the Win32 error code, or 0 on success.</summary>
    public static int SetDiscoverable(bool enabled)
    {
        var viaHandle = WithRadio(radio =>
            BluetoothEnableDiscovery(radio, enabled) ? 0 : Marshal.GetLastWin32Error(), -1);
        if (viaHandle == 0) return 0;
        return BluetoothEnableDiscovery(IntPtr.Zero, enabled) ? 0 : Marshal.GetLastWin32Error();
    }
}
