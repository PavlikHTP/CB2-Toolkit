using CB2Toolkit.Core.Models;
using CB2Toolkit.Core.Utilities;

namespace CB2Toolkit.Core.Services;

public class CrashReportingService
{
    public static CrashReportingService Instance { get; } = new();

    private CrashReportingService() { }

    private static readonly string LogDirectory = Path.Combine(AppMetadata.AppDataFolder, "CrashLogs");

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
            
            SystemMetrics metrics = SystemInfoCollector.GetMetrics();

            string logText = $"=== APPLICATION CRASH REPORT ===\n" +
                             $"Timestamp: {crashTime:yyyy-MM-dd HH:mm:ss}\n" +
                             $"App Version: {AppMetadata.VersionString}\n" +
                             $"OS Version: {metrics.OsVersion}\n" +
                             $"Process Architecture: {metrics.ProcessArch}\n" +
                             $"App RAM Usage: {metrics.WorkingSetMb} MB\n" +
                             $"System RAM: {metrics.FreeMemoryMb} MB free / {metrics.TotalMemoryMb} MB total\n" +
                             $"DotNet Runtime: {metrics.RuntimeVersion}\n" +
                             $"SysInfo Collector Status: {metrics.Error}\n" +
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