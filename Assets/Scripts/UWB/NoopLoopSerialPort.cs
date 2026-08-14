using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Fortal.UWB
{
    /// <summary>Minimal Win32 serial port reader used for the NoopLoop LinkTrack local anchor USB link.</summary>
    internal sealed class NoopLoopSerialPort : IDisposable
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private const uint GenericRead = 0x80000000;
        private const uint GenericWrite = 0x40000000;
        private const uint OpenExisting = 3;
        private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);

        private IntPtr handle = InvalidHandleValue;
        private readonly string portName;
        private readonly int baudRate;
        private readonly byte[] oneByte = new byte[1];

        public NoopLoopSerialPort(string portName, int baudRate)
        {
            this.portName = portName;
            this.baudRate = baudRate;
        }

        public bool IsOpen => handle != InvalidHandleValue;

        public void Open()
        {
            if (IsOpen)
            {
                return;
            }

            string devicePath = portName.StartsWith(@"\\.\", StringComparison.Ordinal) ? portName : @"\\.\" + portName;
            handle = CreateFile(devicePath, GenericRead | GenericWrite, 0, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            if (handle == InvalidHandleValue)
            {
                ThrowLastWin32Error($"Could not open {portName}");
            }

            Dcb dcb = new Dcb();
            dcb.DCBlength = Marshal.SizeOf<Dcb>();
            if (!GetCommState(handle, ref dcb))
            {
                ThrowLastWin32Error($"Could not read serial settings for {portName}");
            }

            if (!BuildCommDCB($"baud={baudRate} parity=N data=8 stop=1", ref dcb))
            {
                ThrowLastWin32Error($"Could not build serial settings for {portName}");
            }

            if (!SetCommState(handle, ref dcb))
            {
                ThrowLastWin32Error($"Could not apply serial settings for {portName}");
            }

            CommTimeouts timeouts = new CommTimeouts
            {
                ReadIntervalTimeout = 1,
                ReadTotalTimeoutConstant = 20,
                ReadTotalTimeoutMultiplier = 0,
                WriteTotalTimeoutConstant = 20,
                WriteTotalTimeoutMultiplier = 0
            };

            if (!SetCommTimeouts(handle, ref timeouts))
            {
                ThrowLastWin32Error($"Could not apply serial timeouts for {portName}");
            }
        }

        public int ReadByte()
        {
            if (!IsOpen)
            {
                return -1;
            }

            if (!ReadFile(handle, oneByte, 1, out uint bytesRead, IntPtr.Zero))
            {
                ThrowLastWin32Error($"Could not read from {portName}");
            }

            return bytesRead == 1 ? oneByte[0] : -1;
        }

        public void Dispose()
        {
            if (!IsOpen)
            {
                return;
            }

            CloseHandle(handle);
            handle = InvalidHandleValue;
        }

        private static void ThrowLastWin32Error(string message)
        {
            throw new InvalidOperationException($"{message}: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadFile(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetCommState(IntPtr hFile, ref Dcb lpDCB);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetCommState(IntPtr hFile, ref Dcb lpDCB);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool BuildCommDCB(string lpDef, ref Dcb lpDCB);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetCommTimeouts(IntPtr hFile, ref CommTimeouts lpCommTimeouts);

        [StructLayout(LayoutKind.Sequential)]
        private struct CommTimeouts
        {
            public uint ReadIntervalTimeout;
            public uint ReadTotalTimeoutMultiplier;
            public uint ReadTotalTimeoutConstant;
            public uint WriteTotalTimeoutMultiplier;
            public uint WriteTotalTimeoutConstant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Dcb
        {
            public int DCBlength;
            public int BaudRate;
            public int Flags;
            public ushort wReserved;
            public ushort XonLim;
            public ushort XoffLim;
            public byte ByteSize;
            public byte Parity;
            public byte StopBits;
            public sbyte XonChar;
            public sbyte XoffChar;
            public sbyte ErrorChar;
            public sbyte EofChar;
            public sbyte EvtChar;
            public ushort wReserved1;
        }
#else
        public NoopLoopSerialPort(string portName, int baudRate)
        {
        }

        public bool IsOpen => false;

        public void Open()
        {
            throw new PlatformNotSupportedException("NoopLoop UWB serial link currently supports Windows Editor/Player only.");
        }

        public int ReadByte()
        {
            return -1;
        }

        public void Dispose()
        {
        }
#endif
    }
}
