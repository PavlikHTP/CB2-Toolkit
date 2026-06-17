using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CB2Toolkit.Core.Services;

public class CrashReportingService
{
    public static CrashReportingService Instance { get; } = new();

    private CrashReportingService() { }

    private static readonly string LogDirectory = Path.Combine(AppMetadata.AppDataFolder, "CrashLogs");

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

    public void HandleException(Exception ex)
    {
        try
        {
            if (!Directory.Exists(LogDirectory))
            {
                Directory.CreateDirectory(LogDirectory);
            }
            
            DateTime crashTime = DateTime.Now;
            string fileName = $"crash_{crashTime:yyyy-MM-dd_HHmmss}.txt";
            string filePath = Path.Combine(LogDirectory, fileName);
            
            ulong totalMemoryMb = 0;
            ulong freeMemoryMb = 0;
            long appMemoryMb = 0;
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

            string logText = $"=== APPLICATION CRASH REPORT ===\n" +
                             $"Timestamp: {crashTime:yyyy-MM-dd HH:mm:ss}\n" +
                             $"App Version: {AppMetadata.VersionString}\n" +
                             $"OS Version: {Environment.OSVersion} ({(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")})\n" +
                             $"Process Architecture: {(Environment.Is64BitProcess ? "x64" : "x86")}\n" +
                             $"App RAM Usage: {appMemoryMb} MB\n" +
                             $"System RAM: {freeMemoryMb} MB free / {totalMemoryMb} MB total\n" +
                             $"DotNet Runtime: {Environment.Version}\n" +
                             $"SysInfo Collector Status: {sysInfoError}\n" +
                             $"=================================\n\n" +
                             $"Exception Type: {ex.GetType().FullName}\n" +
                             $"Message: {ex.Message}\n" +
                             $"\nStackTrace:\n{ex.StackTrace}\n";

            if (ex.InnerException != null)
            {
                logText += $"\nInner Exception: {ex.InnerException.Message}\n" +
                           $"{ex.InnerException.StackTrace}\n";
            }

            File.WriteAllText(filePath, logText);
        }
        catch
        {
        }
    }
}