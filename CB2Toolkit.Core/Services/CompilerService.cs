using System.Diagnostics;
using System.Text;
using CB2Toolkit.Core.Models;

namespace CB2Toolkit.Core.Services;

public class CompilerService
{
    private static readonly Lazy<CompilerService> _instance = new(() => new CompilerService());
    public static CompilerService Instance => _instance.Value;

    private readonly SemaphoreSlim _compileSemaphore = new(1, 1);

    private CompilerService()
    {
    }

    public async Task<List<LogEntry>> RunCompilerAsync(string? code, string? filePath, string outputName)
    {
        string rawCompilerPath = SettingsService.Instance.Current.AngelScriptCompilerPath;

        if (string.IsNullOrWhiteSpace(rawCompilerPath) || !File.Exists(rawCompilerPath))
        {
            rawCompilerPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ASCompiler.exe");
        }

        string compilerPath = Path.GetFullPath(rawCompilerPath);

        if (!File.Exists(compilerPath))
        {
            return ProcessLogToEntries("Error: ASCompiler.exe not found!");
        }

        string compilerDir = Path.GetDirectoryName(compilerPath) ?? AppDomain.CurrentDomain.BaseDirectory;
        string workingDir = compilerDir;
        string scriptFileName = "main.as";

        if (!string.IsNullOrWhiteSpace(filePath))
        {
            string? fileDir = Path.GetDirectoryName(Path.GetFullPath(filePath));
            if (!string.IsNullOrEmpty(fileDir))
            {
                workingDir = fileDir;
                scriptFileName = Path.GetFileName(filePath);
            }
        }

        string scriptPath = Path.Combine(workingDir, scriptFileName);
        string logInWorkingDir = Path.Combine(workingDir, "templog.txt");
        string logInCompilerDir = Path.Combine(compilerDir, "templog.txt");
        string logInAppDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "templog.txt");

        await _compileSemaphore.WaitAsync();
        try
        {
            TryDeleteFile(logInWorkingDir);
            TryDeleteFile(logInCompilerDir);
            TryDeleteFile(logInAppDir);

            if (code != null)
            {
                await File.WriteAllTextAsync(scriptPath, code, Encoding.UTF8);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = compilerPath,
                Arguments = $"\"{scriptFileName}\"",
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = workingDir
            };

            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                return ProcessLogToEntries("Error: The OS refused to start the compilation process.");
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(2500));
            try
            {
                while (!process.HasExited)
                {
                    if (IsLogAvailable(logInWorkingDir) || IsLogAvailable(logInCompilerDir) ||
                        IsLogAvailable(logInAppDir))
                    {
                        break;
                    }

                    await Task.Delay(15, cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }

            if (!process.HasExited)
            {
                try
                {
                    process.Kill(true);
                }
                catch
                {
                }
            }

            string defaultCompiledPath = Path.Combine(workingDir, Path.GetFileNameWithoutExtension(scriptFileName) + ".asc");
            if (File.Exists(defaultCompiledPath))
            {
                string cleanOutputName = outputName.EndsWith(".asc", StringComparison.OrdinalIgnoreCase)
                    ? Path.GetFileNameWithoutExtension(outputName)
                    : outputName;

                string targetCompiledPath = Path.Combine(workingDir, $"{cleanOutputName}.asc");

                if (!string.Equals(Path.GetFullPath(defaultCompiledPath), Path.GetFullPath(targetCompiledPath), StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        if (File.Exists(targetCompiledPath))
                        {
                            File.Delete(targetCompiledPath);
                        }

                        File.Move(defaultCompiledPath, targetCompiledPath);
                    }
                    catch
                    {
                    }
                }
            }

            string fileLogContent = string.Empty;

            if (File.Exists(logInWorkingDir))
            {
                fileLogContent = await TryReadAllTextAsync(logInWorkingDir);
            }

            if (string.IsNullOrEmpty(fileLogContent) && File.Exists(logInCompilerDir))
            {
                fileLogContent = await TryReadAllTextAsync(logInCompilerDir);
            }

            if (string.IsNullOrEmpty(fileLogContent) && File.Exists(logInAppDir))
            {
                fileLogContent = await TryReadAllTextAsync(logInAppDir);
            }

            TryDeleteFile(logInWorkingDir);
            TryDeleteFile(logInCompilerDir);
            TryDeleteFile(logInAppDir);

            if (!string.IsNullOrWhiteSpace(fileLogContent))
            {
                return ProcessLogToEntries(fileLogContent);
            }

            return ProcessLogToEntries("Warning: No compiler log file contained text or was created.");
        }
        catch (Exception ex)
        {
            return ProcessLogToEntries($"Error: {ex.Message}");
        }
        finally
        {
            _compileSemaphore.Release();
        }
    }

    private bool IsLogAvailable(string path)
    {
        if (!File.Exists(path)) return false;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return stream.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> TryReadAllTextAsync(string path)
    {
        for (int i = 0; i < 5; i++)
        {
            try
            {
                return await File.ReadAllTextAsync(path, Encoding.UTF8);
            }
            catch (IOException)
            {
                await Task.Delay(10);
            }
            catch
            {
                break;
            }
        }

        return string.Empty;
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private List<LogEntry> ProcessLogToEntries(string text)
    {
        var result = new List<LogEntry>();
        if (string.IsNullOrEmpty(text)) return result;

        foreach (ReadOnlySpan<char> lineSpan in text.AsSpan().EnumerateLines())
        {
            ReadOnlySpan<char> trimmed = lineSpan.Trim();
            if (trimmed.IsEmpty) continue;

            string color = "Cyan";

            if (trimmed.Contains("ERROR", StringComparison.OrdinalIgnoreCase))
            {
                color = "Red";
            }
            else if (trimmed.Contains("INFO", StringComparison.OrdinalIgnoreCase) ||
                     trimmed.Contains("SUCCESS", StringComparison.OrdinalIgnoreCase))
            {
                color = "Green";
            }

            result.Add(new LogEntry { Text = trimmed.ToString(), Color = color });
        }

        return result;
    }
}