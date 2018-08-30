using System;
using System.Net;
using System.Runtime.InteropServices;

namespace Detourium.Detours.Network
{
    /// <summary>
    /// A class for extending the functionality of the built-in networking detour.
    /// </summary>
    public static class NetworkDetourExtensions
    {
        /// <summary>
        /// Retrieve the host name entry from DNS for the specified endpoint.
        /// </summary>
        /// <returns> The host name entry from DNS for the specified endpoint. </returns>
        public static string GetHostName(this IPEndPoint endpoint) => Dns.GetHostEntry(endpoint.Address.ToString()).HostName;

        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int memcmp(byte[] b1, byte[] b2, long count);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out UIntPtr lpNumberOfBytesWritten);

        [StructLayout(LayoutKind.Sequential)]
        internal struct sockaddr
        {
            public short sin_family;
            public ushort sin_port;
            public in_addr sin_addr;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] sin_zero;
        }

        internal struct in_addr
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] sin_addr;
        }

        [DllImport("ws2_32.dll")]
        internal static extern int getpeername(IntPtr socketHandle, ref sockaddr socketAddress, ref int socketAddressSize);

        [DllImport("ws2_32.dll", SetLastError = true)]
        internal static extern int getsockname(IntPtr socketHandle, ref sockaddr socketAddress, ref int socketAddressSize);

        internal static IPEndPoint GetSourceIPEndPoint(this IntPtr socket)
        {
            var address = new sockaddr();
            var nameLength = Marshal.SizeOf(address);
            var result = getsockname(socket, ref address, ref nameLength);

            var octets = new byte[4];

            for (var i = 0; i < 4; i++)
                octets[i] = address.sin_addr.sin_addr[i];

            return new IPEndPoint(IPAddress.Parse(string.Join(".", octets)), address.sin_port);
        }

        internal static IPEndPoint GetDestinationIPEndPoint(this IntPtr socket)
        {
            var address = new sockaddr();
            var nameLength = 0x10;
            var result = getpeername(socket, ref address, ref nameLength);

            var octets = new byte[4];

            for (var i = 0; i < 4; i++)
                octets[i] = address.sin_addr.sin_addr[i];

            return new IPEndPoint(IPAddress.Parse(string.Join(".", octets)), address.sin_port);
        }

        internal static byte[] Copy(this IntPtr buffer, int index, int length)
        {
            var _buffer = new byte[length];
            Marshal.Copy(buffer, _buffer, index, length);

            return _buffer;
        }

        internal static bool FastSequenceEquals(this byte[] b1, byte[] b2)
        {
            return b1.Length == b2.Length && memcmp(b1, b2, b1.Length) == 0;
        }
    }
}
