using System.Security.Cryptography;
using System.Text;

namespace CB2Toolkit.Core.Services;

public class TempFileService
{
    private static readonly Lazy<TempFileService> _instance = new(() => new TempFileService());
    private readonly string _tempFolder;

    private readonly HashSet<string> _pendingFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public static TempFileService Instance => _instance.Value;

    private TempFileService()
    {
        _tempFolder = Path.Combine(Path.GetTempPath(), $"{AppMetadata.Title} cache");
        if (!Directory.Exists(_tempFolder))
        {
            Directory.CreateDirectory(_tempFolder);
        }
    }

    private string GetTempPathForFile(string filePath)
    {
        using (var md5 = MD5.Create())
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(filePath.ToLowerInvariant());
            byte[] hashBytes = md5.ComputeHash(inputBytes);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hashBytes.Length; i++)
            {
                sb.Append(hashBytes[i].ToString("X2"));
            }

            return Path.Combine(_tempFolder, sb + ".tmp");
        }
    }

    public void SaveTemp(string filePath, string content)
    {
        lock (_lock)
        {
            _pendingFiles.Add(filePath);
        }


        try
        {
            string tempPath = GetTempPathForFile(filePath);
            File.WriteAllText(tempPath, content, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogError($"Failed to save temporary file for {filePath}: {ex.Message}");
        }
    }

    public string GetTemp(string filePath)
    {
        try
        {
            string tempPath = GetTempPathForFile(filePath);
            if (!File.Exists(tempPath)) return null;

            string tempContent = File.ReadAllText(tempPath, Encoding.UTF8);

            if (File.Exists(filePath))
            {
                string originalContent = File.ReadAllText(filePath, Encoding.UTF8);

                if (string.Equals(tempContent, originalContent, StringComparison.Ordinal))
                {
                    File.Delete(tempPath);

                    lock (_lock)
                    {
                        _pendingFiles.Remove(filePath);
                    }

                    return null;
                }
            }

            return tempContent;
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogError($"Failed to read temporary file for {filePath}: {ex.Message}");
        }

        return null;
    }

    public void ClearTemp(string filePath)
    {
        try
        {
            string tempPath = GetTempPathForFile(filePath);
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            lock (_lock)
            {
                _pendingFiles.Remove(filePath);
            }
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogError($"Failed to delete temporary file for {filePath}: {ex.Message}");
        }
    }

    public List<string> GetPendingFiles()
    {
        lock (_lock)
        {
            return _pendingFiles.ToList();
        }
    }
}