using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace WOTRMultiplayer.Logging
{
    public static class WinApi
    {
        public const uint GENERIC_WRITE = 0x40000000;
        public const uint FILE_SHARE_WRITE = 0x2;
        public const uint OPEN_EXISTING = 0x3;

        [DllImport("kernel32.dll", EntryPoint = "AllocConsole", SetLastError = true, CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, uint lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, uint hTemplateFile);

        public static TextWriter SpawnConsole()
        {
            if (!AllocConsole())
            {
                throw new InvalidOperationException("Unable to alloc console");
            }

            var fileHandle = CreateFile("CONOUT$", GENERIC_WRITE, FILE_SHARE_WRITE, 0, OPEN_EXISTING, 0, 0);
            var safeFileHandle = new SafeFileHandle(fileHandle, true);
            var fileStream = new FileStream(safeFileHandle, FileAccess.Write);
            var standardOutput = new StreamWriter(fileStream, Encoding.UTF8)
            {
                AutoFlush = true
            };

            return standardOutput;
        }
    }
}
