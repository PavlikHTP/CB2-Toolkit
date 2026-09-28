using System.Diagnostics;

namespace CB2Toolkit.Core.Services;

public class TerminalExecutionService
{
    public event Action<string>? OutputReceived;
    public event Action<string>? ErrorReceived;

    public async Task ExecuteAsync(string command, string workingDirectory)
    {
        LoggerService.Instance.LogDebug($"Terminal run: {command}");
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {command}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = string.IsNullOrEmpty(workingDirectory) ? AppDomain.CurrentDomain.BaseDirectory : workingDirectory
            };

            process.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null) OutputReceived?.Invoke(e.Data);
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null) ErrorReceived?.Invoke(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();
            LoggerService.Instance.LogDebug($"Terminal exit: {process.ExitCode}: {command}");
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogError($"Terminal failed: {command}: {ex.Message}");
            ErrorReceived?.Invoke($"[Terminal Error] {ex.Message}");
        }
    }
}