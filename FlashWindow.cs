using System;
using System.Runtime.InteropServices;

namespace HaierAC
{
    public static class FlashWindow
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool FlashWindowEx(ref FLASHWINFO pwfi);
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [StructLayout(LayoutKind.Sequential)]
        public struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }

        public const uint FLASHW_ALL = 3;

        public static void Flash(uint count = 5)
        {
            FLASHWINFO fInfo = new FLASHWINFO();

            fInfo.cbSize = Convert.ToUInt32(Marshal.SizeOf(fInfo));
            fInfo.hwnd = GetConsoleWindow();
            fInfo.dwFlags = FLASHW_ALL;
            fInfo.uCount = count;
            fInfo.dwTimeout = 0;

            FlashWindowEx(ref fInfo);
        }
    }
}
