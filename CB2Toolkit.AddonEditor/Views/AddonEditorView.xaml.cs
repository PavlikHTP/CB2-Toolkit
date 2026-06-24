using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CB2Toolkit.CodeEditor.Models.Enums;
using CB2Toolkit.CodeEditor.Views;
using CB2Toolkit.Core;
using CB2Toolkit.Core.Models;
using CB2Toolkit.Core.Services;
using CB2Toolkit.Core.Utilities;

namespace CB2Toolkit.AddonEditor.Views;

public partial class AddonEditorView : UserControl
{
    private string? _currentFilePath;
    private string? _projectFolderPath;

    public AddonEditorView()
    {
        InitializeComponent();

        LoggerService.Instance.OnLogAdded += entry => Dispatcher.Invoke(() =>
        {
            ConsoleOutput.Items.Add(entry);
            if (AutoscrollToggle.IsChecked == true)
            {
                ConsoleScrollViewer.ScrollToBottom();
            }
        });

        LoggerService.Instance.OnLogCleared += () => Dispatcher.Invoke(() => ConsoleOutput.Items.Clear());

        ProjectService.Instance.OnTreeStructureChanged += () => Dispatcher.Invoke(LoadProjectTree);
        ProjectService.Instance.OnFileChangedExternally += path => Dispatcher.Invoke(() => OnFileChanged(path));
        ProjectService.Instance.OnActiveFileDeletedExternally += path => Dispatcher.Invoke(() => OnFileDeleted(path));
        ProjectService.Instance.OnActiveFileRenamedExternally += (oldPath, newPath) =>
            Dispatcher.Invoke(() => OnFileRenamed(oldPath, newPath));
    }

    private string? ShowInputDialog(string title, string defaultText)
    {
        var result = ModernMessageBox.Show(
            Window.GetWindow(this),
            $"Please enter the name for {title.ToLower()}:",
            title,
            ModernBoxType.Input,
            defaultText
        );

        return result.Result == ModernBoxResultType.OK ? result.InputText : null;
    }

    private void ContextMenu_CreateFile_Click(object sender, RoutedEventArgs e)
    {
        FileNode? node = GetNodeFromMenu(sender);
        string? targetDir = node != null
            ? (node.IsDirectory ? node.FullPath : Path.GetDirectoryName(node.FullPath))
            : _projectFolderPath;

        if (string.IsNullOrEmpty(targetDir)) return;

        string? name = ShowInputDialog("New File", "newfile.ini");
        if (string.IsNullOrEmpty(name)) return;

        try
        {
            ProjectService.Instance.CreateFile(targetDir, name);
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogError($"[Create Error] {ex.Message}");
        }
    }

    private void ContextMenu_CreateFolder_Click(object sender, RoutedEventArgs e)
    {
        FileNode? node = GetNodeFromMenu(sender);
        string? targetDir = node != null
            ? (node.IsDirectory ? node.FullPath : Path.GetDirectoryName(node.FullPath))
            : _projectFolderPath;

        if (string.IsNullOrEmpty(targetDir)) return;

        string? name = ShowInputDialog("New Folder", "NewFolder");
        if (string.IsNullOrEmpty(name)) return;

        try
        {
            ProjectService.Instance.CreateDirectory(targetDir, name);
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogError($"[Create Error] {ex.Message}");
        }
    }

    private void OnFileChanged(string path)
    {
        if (_currentFilePath == path)
        {
            try
            {
                CodeEditor.Text = File.ReadAllText(path);
            }
            catch
            {
            }
        }
    }

    private void OnFileDeleted(string path)
    {
        if (_currentFilePath == path)
        {
            _currentFilePath = null;
            CurrentFileNameText.Text = "No file selected";
            CodeEditor.Text = string.Empty;
            EditorPanel.Visibility = Visibility.Collapsed;
            InspectorScrollViewer.Visibility = Visibility.Collapsed;
            ModernMessageBox.Show(Window.GetWindow(this), "The active file was deleted externally.", "File Deleted",
                ModernBoxType.Information);
        }
    }

    private void OnFileRenamed(string oldPath, string newPath)
    {
        if (_currentFilePath == oldPath)
        {
            _currentFilePath = newPath;
            CurrentFileNameText.Text = Path.GetFileName(newPath);
        }
    }

    private void BackToMenu_Click(object sender, RoutedEventArgs e)
    {
        BackToMenu();
    }

    public void BackToMenu()
    {
        Window currentWindow = Window.GetWindow(this);
        if (currentWindow != null)
        {
            dynamic mainWindow = currentWindow;
            mainWindow.NavigateToMenu();
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Project Folder"
        };

        if (dialog.ShowDialog() == true)
        {
            _ = OpenProject(dialog.FolderName);
        }
    }

    private void FileMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.ContextMenu != null)
        {
            RefreshRecentFoldersSubmenu();
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.IsOpen = true;
        }
    }

    private void RefreshRecentFoldersSubmenu()
    {
        RecentFoldersMenu.Items.Clear();

        var settings = SettingsService.Instance.Current;
        if (settings.RecentAddonFolders == null || settings.RecentAddonFolders.Count == 0)
        {
            RecentFoldersMenu.Items.Add(new MenuItem { Header = "No recent projects", IsEnabled = false });
        }
        else
        {
            foreach (var path in settings.RecentAddonFolders)
            {
                var item = new MenuItem
                {
                    Header = Path.GetFileName(path),
                    ToolTip = path
                };
                item.Click += (s, args) => OpenProject(path);
                RecentFoldersMenu.Items.Add(item);
            }
        }
    }

    private async Task OpenProject(string path)
    {
        _projectFolderPath = path;
        ServerFolderInput.Text = Path.GetFileName(path);

        ProjectService.Instance.OpenProject(path);
        LoadProjectTree();
        var settings = SettingsService.Instance.Current;
        settings.RecentAddonFolders.Remove(path);
        settings.RecentAddonFolders.Insert(0, path);

        if (settings.RecentAddonFolders.Count > 5)
        {
            settings.RecentAddonFolders.RemoveAt(settings.RecentAddonFolders.Count - 1);
        }

        await SettingsService.Instance.SaveAsync();
    }

    private void RefreshTree_Click(object sender, RoutedEventArgs e)
    {
        LoadProjectTree();
    }

    private void LoadProjectTree()
    {
        FileTree.Items.Clear();
        var nodes = ProjectService.Instance.BuildProjectTree();
        foreach (var child in nodes)
        {
            FileTree.Items.Add(child);
        }
    }

    private async void RunProcess_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_projectFolderPath) || !Directory.Exists(_projectFolderPath))
        {
            return;
        }

        string serverFolder = ServerFolderInput.Text;
        if (string.IsNullOrEmpty(serverFolder) || serverFolder == "Specify server folder there")
        {
            serverFolder = Path.GetFileName(_projectFolderPath);
        }

        int mode = PackingModeComboBox.SelectedIndex;

        if (mode == 0)
        {
            try
            {
                string[] allFiles = Directory.GetFiles(_projectFolderPath, "*.*", SearchOption.AllDirectories);
                var fileEntries = new List<object>();
                string baseUrl = BaseUrlInput.Text.Trim().TrimEnd('/');

                foreach (string filePath in allFiles)
                {
                    string fileName = Path.GetFileName(filePath);
                    if (fileName.Equals("addons.jsonc", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string ext = Path.GetExtension(filePath).ToLower();
                    if (ext == ".exe" || ext == ".dll" || ext == ".pdb")
                    {
                        continue;
                    }

                    FileInfo info = new FileInfo(filePath);
                    if (info.Length > 256 * 1024 * 1024)
                    {
                        continue;
                    }

                    string relativePath = Path.GetRelativePath(_projectFolderPath, filePath).Replace('\\', '/');
                    byte[] bytes = File.ReadAllBytes(filePath);
                    string hash = CryptoBuffer.GetMd5FromBytes(bytes).ToLowerInvariant();
                    string url = $"{baseUrl}/{serverFolder}/{relativePath}";

                    fileEntries.Add(new
                    {
                        url = url,
                        export = relativePath,
                        hash = hash
                    });
                }

                var jsonObject = new
                {
                    serverfolder = serverFolder,
                    files = fileEntries
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonString = JsonSerializer.Serialize(jsonObject, options);
                string outputPath = Path.Combine(_projectFolderPath, "addons.jsonc");
                File.WriteAllText(outputPath, jsonString);

                LoadProjectTree();
            }
            catch
            {
            }
        }
        else if (mode == 1)
        {
            try
            {
                string baseUrl = BaseUrlInput.Text.Trim().TrimEnd('/');
                await CbpakService.Instance.PackDirectoryAsync(_projectFolderPath, serverFolder, baseUrl, serverFolder);
                LoadProjectTree();
            }
            catch
            {
            }
        }
        else if (mode == 2)
        {
            try
            {
                if (!string.IsNullOrEmpty(_currentFilePath) &&
                    _currentFilePath.EndsWith(".cbpak", StringComparison.OrdinalIgnoreCase))
                {
                    string outFolder = Path.Combine(Path.GetDirectoryName(_currentFilePath) ?? _projectFolderPath,
                        Path.GetFileNameWithoutExtension(_currentFilePath));
                    await CbpakService.Instance.UnpackFileAsync(_currentFilePath, outFolder);
                    LoadProjectTree();
                }
                else
                {
                    string[] cbpakFiles =
                        Directory.GetFiles(_projectFolderPath, "*.cbpak", SearchOption.TopDirectoryOnly);
                    foreach (string file in cbpakFiles)
                    {
                        string outFolder = Path.Combine(_projectFolderPath, Path.GetFileNameWithoutExtension(file));
                        await CbpakService.Instance.UnpackFileAsync(file, outFolder);
                    }

                    LoadProjectTree();
                }
            }
            catch
            {
            }
        }
    }

    private void ClearConsole_Click(object sender, RoutedEventArgs e)
    {
        LoggerService.Instance.Clear();
    }

    private void FileTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is FileNode node && !node.IsDirectory)
        {
            if (!File.Exists(node.FullPath)) return;

            _currentFilePath = node.FullPath;
            CurrentFileNameText.Text = Path.GetFileName(_currentFilePath);
            string ext = Path.GetExtension(_currentFilePath).ToLower();

            if (ext == ".jsonc" || ext == ".json" || ext == ".ini" || ext == ".txt" || ext == ".cfg")
            {
                InspectorScrollViewer.Visibility = Visibility.Collapsed;
                EditorPanel.Visibility = Visibility.Visible;
                CodeEditor.Text = File.ReadAllText(_currentFilePath);
                DiscordRpcService.UpdateToEditing(Path.GetFileName(_currentFilePath));
            }
            else
            {
                EditorPanel.Visibility = Visibility.Collapsed;
                InspectorScrollViewer.Visibility = Visibility.Visible;

                FileInfo fileInfo = new FileInfo(node.FullPath);
                InspectorExportPath.Text = node.FullPath;
                
                double sizeKb = (double)fileInfo.Length / 1024;
                InspectorSize.Text = sizeKb > 1024
                    ? $"{sizeKb / 1024:F2} MB ({fileInfo.Length} bytes)"
                    : $"{sizeKb:F2} KB ({fileInfo.Length} bytes)";

                try
                {
                    byte[] fileBytes = File.ReadAllBytes(node.FullPath);
                    InspectorHash.Text = CryptoBuffer.GetMd5FromBytes(fileBytes).ToLowerInvariant();
                }
                catch
                {
                    InspectorHash.Text = "ERROR_CALCULATING_HASH";
                }

                string relativePath = string.Empty;
                if (!string.IsNullOrEmpty(_projectFolderPath))
                {
                    relativePath = Path.GetRelativePath(_projectFolderPath, node.FullPath).Replace('\\', '/');
                }

                string baseUrl = BaseUrlInput.Text.Trim().TrimEnd('/');
                string serverFolder = ServerFolderInput.Text;
                InspectorUrlOverride.Text = $"{baseUrl}/{serverFolder}/{relativePath}";

                StatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
                StatusBadgeText.Text = "VALID";
                DiscordRpcService.UpdateToEditing(Path.GetFileName(node.FullPath));
            }
        }
        else
        {
            InspectorScrollViewer.Visibility = Visibility.Collapsed;
            EditorPanel.Visibility = Visibility.Collapsed;
            CurrentFileNameText.Text = "No file selected";
        }
    }

    private void SaveFile_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_currentFilePath) && File.Exists(_currentFilePath))
        {
            try
            {
                File.WriteAllText(_currentFilePath, CodeEditor.Text);
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogError($"[Save Error] Failed to save file: {ex.Message}");
            }
        }
    }

    private void StatusBadge_MouseDown(object sender, MouseButtonEventArgs e)
    {
        ModernMessageBox.Show(Window.GetWindow(this), AppMetadata.AddonEditorRulesText, "Validation Requirements");
    }

    private void ContextMenu_Rename_Click(object sender, RoutedEventArgs e)
    {
        FileNode? node = GetNodeFromMenu(sender);
        if (node == null) return;
        
        string? newName = ShowInputDialog("Rename", node.Key);
        if (string.IsNullOrEmpty(newName) || newName == node.Key) return;

        try
        {
            ProjectService.Instance.RenameNode(node, newName);
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogError($"[Rename Error] {ex.Message}");
        }
    }

    private void ContextMenu_Delete_Click(object sender, RoutedEventArgs e)
    {
        FileNode? node = GetNodeFromMenu(sender);
        if (node == null) return;
        
        var result = ModernMessageBox.Show(
            Window.GetWindow(this),
            $"Delete {node.Key}?",
            "Confirmation",
            ModernBoxType.Question
        );

        if (result.Result == ModernBoxResultType.Yes)
        {
            try
            {
                ProjectService.Instance.DeleteNode(node);
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogError($"[Delete Error] {ex.Message}");
            }
        }
    }

    private void ContextMenu_Exclude_Click(object sender, RoutedEventArgs e)
    {
    }

    private void ContextMenu_RevealInExplorer_Click(object sender, RoutedEventArgs e)
    {
        FileNode? node = GetNodeFromMenu(sender);
        if (node != null && !string.IsNullOrEmpty(node.FullPath))
        {
            ShellService.Instance.RevealInExplorer(node.FullPath);
        }
    }

    private FileNode? GetNodeFromMenu(object sender)
    {
        if (sender is MenuItem menuItem && menuItem.Parent is ContextMenu contextMenu &&
            contextMenu.PlacementTarget is FrameworkElement element)
        {
            if (element is TreeViewItem tvi)
            {
                return tvi.DataContext as FileNode;
            }

            return element.DataContext as FileNode;
        }

        return FileTree.SelectedItem as FileNode;
    }

    private void CopyConsole_Click(object sender, RoutedEventArgs e)
    {
        if (ConsoleOutput.Items.Count == 0) return;

        var sb = new StringBuilder();
        foreach (var item in ConsoleOutput.Items)
        {
            if (item != null)
            {
                dynamic entry = item;
                try
                {
                    sb.AppendLine(entry.Text);
                }
                catch
                {
                    sb.AppendLine(item.ToString());
                }
            }
        }

        try
        {
            Clipboard.SetText(sb.ToString());
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogError($"[Clipboard Error] Failed to copy console output: {ex.Message}");
        }
    }

    private void AutoscrollToggle_Checked(object sender, RoutedEventArgs e)
    {
        ScrollConsoleToBottom();
    }

    private void ScrollConsoleToBottom()
    {
        if (ConsoleScrollViewer == null) return;

        ConsoleScrollViewer.Dispatcher.InvokeAsync(
            () => { ConsoleScrollViewer.ScrollToEnd(); }, DispatcherPriority.Background);
    }

    private void TerminalInput_KeyDown(object sender, KeyEventArgs e)
    {
        throw new NotImplementedException();
    }
}