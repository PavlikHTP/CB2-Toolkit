using System.IO.Compression;
using CB2Toolkit.Core.Models;

namespace CB2Toolkit.Core.Services;

public class ProjectService : IDisposable
{
    private FileSystemWatcher? _watcher;
    public string? CurrentFolderPath { get; private set; }
    
    public event Action? OnTreeStructureChanged;
    public event Action<string>? OnFileChangedExternally;
    public event Action<string>? OnActiveFileDeletedExternally;
    public event Action<string, string>? OnActiveFileRenamedExternally; 
    
    private static readonly Lazy<ProjectService> _instance = new(() => new ProjectService());

    public static ProjectService Instance => _instance.Value;
    
    public void OpenProject(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;
        
        CurrentFolderPath = path;
        InitFileWatcher(path);
    }

    public List<FileNode> BuildProjectTree()
    {
        if (string.IsNullOrEmpty(CurrentFolderPath) || !Directory.Exists(CurrentFolderPath)) 
            return new List<FileNode>();

        var rootNode = CreateDirectoryNode(CurrentFolderPath);
        return rootNode.Value; 
    }

    private FileNode CreateDirectoryNode(string path)
    {
        var directoryNode = new FileNode
        {
            Key = Path.GetFileName(path),
            FullPath = path,
            IsDirectory = true
        };

        try
        {
            foreach (var directory in Directory.GetDirectories(path))
            {
                directoryNode.Value.Add(CreateDirectoryNode(directory));
            }

            foreach (var file in Directory.GetFiles(path, "*.*"))
            {
                directoryNode.Value.Add(new FileNode
                {
                    Key = Path.GetFileName(file),
                    FullPath = file,
                    IsDirectory = false
                });
            }
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogError($"Failed to scan directory {path}: {ex.Message}");
        }

        directoryNode.Value.Sort((a, b) =>
        {
            if (a.IsDirectory != b.IsDirectory)
                return b.IsDirectory.CompareTo(a.IsDirectory);
            return string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase);
        });

        return directoryNode;
    }

    private void InitFileWatcher(string path)
    {
        _watcher?.Dispose();
        _watcher = new FileSystemWatcher(path)
        {
            IncludeSubdirectories = true,
            Filter = "*.*",
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName
        };

        _watcher.Changed += (s, e) => { if (!Directory.Exists(e.FullPath)) OnFileChangedExternally?.Invoke(e.FullPath); };
        _watcher.Created += (s, e) => OnTreeStructureChanged?.Invoke();
        _watcher.Deleted += (s, e) => { OnActiveFileDeletedExternally?.Invoke(e.FullPath); OnTreeStructureChanged?.Invoke(); };
        _watcher.Renamed += (s, e) => { OnActiveFileRenamedExternally?.Invoke(e.OldFullPath, e.FullPath); OnTreeStructureChanged?.Invoke(); };

        _watcher.EnableRaisingEvents = true;
    }

    public void SuspendWatcher() { if (_watcher != null) _watcher.EnableRaisingEvents = false; }
    public void ResumeWatcher() { if (_watcher != null) _watcher.EnableRaisingEvents = true; }
    
    public void CreateFile(string targetDir, string name)
    {
        string path = Path.Combine(targetDir, name);
        if (!File.Exists(path)) File.WriteAllText(path, string.Empty);
    }

    public void CreateDirectory(string targetDir, string name)
    {
        string path = Path.Combine(targetDir, name);
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
    }

    public string RenameNode(FileNode node, string newName)
    {
        string? parentDir = Path.GetDirectoryName(node.FullPath) 
            ?? throw new InvalidOperationException("Parent directory not found");
        
        string targetPath = Path.Combine(parentDir, newName);
        
        Directory.Move(node.FullPath, targetPath);
        
        return targetPath;
    }

    public void DeleteNode(FileNode node)
    {
        if (node.IsDirectory) Directory.Delete(node.FullPath, true);
        else File.Delete(node.FullPath);
    }

    public void DuplicateNode(FileNode node)
    {
        string? parentDir = Path.GetDirectoryName(node.FullPath) ?? throw new InvalidOperationException();
        string targetPath;

        if (node.IsDirectory)
        {
            string dirName = Path.GetFileName(node.FullPath);
            targetPath = Path.Combine(parentDir, $"{dirName}_copy");
            int counter = 1;
            while (Directory.Exists(targetPath)) targetPath = Path.Combine(parentDir, $"{dirName}_copy_{counter++}");
            CopyDirectoryRecursive(node.FullPath, targetPath);
        }
        else
        {
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(node.FullPath);
            string extension = Path.GetExtension(node.FullPath);
            targetPath = Path.Combine(parentDir, $"{fileNameWithoutExt}_copy{extension}");
            int counter = 1;
            while (File.Exists(targetPath)) targetPath = Path.Combine(parentDir, $"{fileNameWithoutExt}_copy_{counter++}{extension}");
            File.Copy(node.FullPath, targetPath);
        }
    }

    public void MoveNode(string sourcePath, string targetDir, string nodeKey, bool isDirectory)
    {
        string targetPath = Path.Combine(targetDir, nodeKey);
        if (sourcePath == targetPath) return;

        if (isDirectory) Directory.Move(sourcePath, targetPath);
        else File.Move(sourcePath, targetPath);
    }

    public void ArchiveNode(FileNode node)
    {
        string archivePath = node.FullPath + ".zip";
        
        if (File.Exists(archivePath)) 
            File.Delete(archivePath);

        try
        {
            SuspendWatcher();
            CompressToZip(node, archivePath);
        }
        finally
        {
            ResumeWatcher();
            OnTreeStructureChanged?.Invoke();
        }
    }

    public void CompressToZip(FileNode node, string archivePath)
    {
        if (node.IsDirectory) ZipFile.CreateFromDirectory(node.FullPath, archivePath);
        else
        {
            using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
            archive.CreateEntryFromFile(node.FullPath, Path.GetFileName(node.FullPath));
        }
    }

    private void CopyDirectoryRecursive(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (string file in Directory.GetFiles(sourceDir))
            File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)));
        foreach (string subDir in Directory.GetDirectories(sourceDir))
            CopyDirectoryRecursive(subDir, Path.Combine(targetDir, Path.GetFileName(subDir)));
    }

    public void Dispose() => _watcher?.Dispose();
}