using CB2Toolkit.Core.Models;

namespace CB2Toolkit.Core.Services;

public class FileTreeStateService
{
    private static readonly Lazy<FileTreeStateService> _instance = new(() => new FileTreeStateService());
    public static FileTreeStateService Instance => _instance.Value;

    private FileTreeStateService()
    {
    }

    public void SaveExpansionState(List<FileNode> currentNodes, bool isAddonProject)
    {
        var expandedPaths = new List<string>();
        GetExpandedPathsRecursive(currentNodes, expandedPaths);

        if (isAddonProject)
        {
            SettingsService.Instance.Current.ExpandedAddonFolders = expandedPaths;
        }
        else
        {
            SettingsService.Instance.Current.ExpandedAngelScriptFolders = expandedPaths;
        }
    }

    public void RestoreTreeState(List<FileNode> newNodes, bool isAddonProject)
    {
        var expandedPaths = isAddonProject 
            ? SettingsService.Instance.Current.ExpandedAddonFolders 
            : SettingsService.Instance.Current.ExpandedAngelScriptFolders;

        RestoreExpansionRecursive(newNodes, expandedPaths);

        if (!isAddonProject && !string.IsNullOrEmpty(SettingsService.Instance.Current.LastOpenedAngelScriptFilePath))
        {
            FindAndSelectFileRecursive(newNodes, SettingsService.Instance.Current.LastOpenedAngelScriptFilePath);
        }
    }

    public void SaveLastOpenedFile(string filePath)
    {
        SettingsService.Instance.Current.LastOpenedAngelScriptFilePath = filePath;
    }

    private void GetExpandedPathsRecursive(List<FileNode> nodes, List<string> expandedPaths)
    {
        if (nodes == null) return;
        foreach (var node in nodes)
        {
            if (node.IsDirectory && node.IsExpanded)
            {
                expandedPaths.Add(node.FullPath);
                GetExpandedPathsRecursive(node.Value, expandedPaths);
            }
        }
    }

    private void RestoreExpansionRecursive(List<FileNode> nodes, List<string> expandedPaths)
    {
        if (nodes == null || expandedPaths == null) return;
        foreach (var node in nodes)
        {
            if (node.IsDirectory && expandedPaths.Contains(node.FullPath))
            {
                node.IsExpanded = true;
                RestoreExpansionRecursive(node.Value, expandedPaths);
            }
        }
    }

    private bool FindAndSelectFileRecursive(List<FileNode> nodes, string filePath)
    {
        if (nodes == null) return false;
        foreach (var node in nodes)
        {
            if (!node.IsDirectory && string.Equals(node.FullPath, filePath, StringComparison.OrdinalIgnoreCase))
            {
                node.IsSelected = true;
                return true;
            }
            if (FindAndSelectFileRecursive(node.Value, filePath))
            {
                node.IsExpanded = true;
                return true;
            }
        }
        return false;
    }
}