using System.Diagnostics;
using System.Runtime.InteropServices;
using CB2Toolkit.Core.Models;

namespace CB2Toolkit.Core.Utilities;

public static class SystemInfoCollector
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    public static SystemMetrics GetMetrics()
    {
        ulong totalMemoryMb = 0;
        ulong freeMemoryMb = 0;
        long appMemoryMb = 0;
        long gcHeapMb = GC.GetTotalMemory(false) / (1024 * 1024);
        string sysInfoError = "None";

        try
        {
            var memStatus = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)) };
            if (GlobalMemoryStatusEx(ref memStatus))
            {
                totalMemoryMb = memStatus.ullTotalPhys / (1024 * 1024);
                freeMemoryMb = memStatus.ullAvailPhys / (1024 * 1024);
            }
            appMemoryMb = Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024);
        }
        catch (Exception sysEx)
        {
            sysInfoError = sysEx.Message;
        }

        return new SystemMetrics(
            totalMemoryMb,
            freeMemoryMb,
            appMemoryMb,
            gcHeapMb,
            $"{Environment.OSVersion} ({(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")})",
            Environment.Is64BitProcess ? "x64" : "x86",
            Environment.Version.ToString(),
            sysInfoError
        );
    }
}