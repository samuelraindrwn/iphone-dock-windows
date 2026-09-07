using System.Runtime.InteropServices;

namespace BleHid.Core;

/// <summary>
/// Checks whether user mode can listen on the L2CAP PSMs that Classic HID Device role needs
/// (0x11 control, 0x13 interrupt). RFCOMM is probed first as a control: if it also fails,
/// the probe is wrong rather than L2CAP being blocked.
/// </summary>
public static class L2capProbe
{
    private const int AF_BTH = 32;
    private const int SOCK_STREAM = 1;
    private const int SOCK_SEQPACKET = 5;
    private const int BTHPROTO_RFCOMM = 3;
    private const int BTHPROTO_L2CAP = 0;
    private const uint BT_PORT_ANY = 0xFFFFFFFF;

    private static readonly IntPtr InvalidSocket = new(-1);

    public static IReadOnlyList<string> Run()
    {
        var results = new List<string>();

        var startup = WSAStartup(0x0202, new byte[512]);
        if (startup != 0)
        {
            results.Add($"WSAStartup failed: {startup}");
            return results;
        }

        try
        {
            results.Add(Attempt("RFCOMM     (control)", SOCK_STREAM, BTHPROTO_RFCOMM, BT_PORT_ANY));
            results.Add(Attempt("L2CAP stream    0x11", SOCK_STREAM, BTHPROTO_L2CAP, 0x11));
            results.Add(Attempt("L2CAP seqpacket 0x11", SOCK_SEQPACKET, BTHPROTO_L2CAP, 0x11));
            results.Add(Attempt("L2CAP seqpacket 0x13", SOCK_SEQPACKET, BTHPROTO_L2CAP, 0x13));
        }
        finally
        {
            WSACleanup();
        }

        return results;
    }

    private static string Attempt(string label, int type, int protocol, uint port)
    {
        var handle = socket(AF_BTH, type, protocol);
        if (handle == InvalidSocket)
            return $"{label}: socket() -> {Describe(WSAGetLastError())}";

        try
        {
            var address = new SockaddrBth
            {
                AddressFamily = AF_BTH,
                BtAddr = 0,
                ServiceClassId = Guid.Empty,
                Port = port
            };

            if (bind(handle, ref address, Marshal.SizeOf<SockaddrBth>()) != 0)
                return $"{label}: bind() -> {Describe(WSAGetLastError())}";

            if (listen(handle, 1) != 0)
                return $"{label}: listen() -> {Describe(WSAGetLastError())}";

            return $"{label}: SUCCESS - listening";
        }
        finally
        {
            closesocket(handle);
        }
    }

    private static string Describe(int error) => error switch
    {
        10041 => "WSAEPROTOTYPE (wrong protocol for this socket type)",
        10043 => "WSAEPROTONOSUPPORT (protocol not supported)",
        10044 => "WSAESOCKTNOSUPPORT (socket type not supported)",
        10047 => "WSAEAFNOSUPPORT (address family not supported)",
        10049 => "WSAEADDRNOTAVAIL (address not available)",
        _ => $"WSA {error}"
    };

    // ws2bth.h wraps SOCKADDR_BTH in pshpack1.h, so it is 30 bytes, not the 40 that
    // default alignment would produce.
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct SockaddrBth
    {
        public ushort AddressFamily;
        public ulong BtAddr;
        public Guid ServiceClassId;
        public uint Port;
    }

    [DllImport("ws2_32.dll")]
    private static extern int WSAStartup(ushort versionRequested, byte[] wsaData);

    [DllImport("ws2_32.dll")]
    private static extern int WSACleanup();

    [DllImport("ws2_32.dll")]
    private static extern IntPtr socket(int af, int type, int protocol);

    [DllImport("ws2_32.dll")]
    private static extern int bind(IntPtr s, ref SockaddrBth name, int nameLength);

    [DllImport("ws2_32.dll")]
    private static extern int listen(IntPtr s, int backlog);

    [DllImport("ws2_32.dll")]
    private static extern int closesocket(IntPtr s);

    [DllImport("ws2_32.dll")]
    private static extern int WSAGetLastError();
}
