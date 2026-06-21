using System.IO;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml;
using CB2Toolkit.CodeEditor.Extensions;
using CB2Toolkit.CodeEditor.Models.Enums;
using CB2Toolkit.CodeEditor.Renderers;
using CB2Toolkit.CodeEditor.Services;
using CB2Toolkit.CodeEditor.Syntax;
using CB2Toolkit.CodeEditor.Utils;
using CB2Toolkit.Core;
using CB2Toolkit.Core.Models;
using CB2Toolkit.Core.Models.Settings;
using CB2Toolkit.Core.Models.Settings.Enums;
using CB2Toolkit.Core.Services;
using CB2Toolkit.Core.Utilities;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Rendering;
using Microsoft.Win32;

namespace CB2Toolkit.CodeEditor.Views;

public partial class AngelScriptEditorView : UserControl
{
    private CompletionWindow? _completionWindow;
    private string? _currentFilePath;
    private bool _isUnsaved;
    private bool _isSuppressingTextEvents
    {
        get => _historyManager.IsSuspended;
        set => _historyManager.IsSuspended = value;
    }
    private Point _startPoint;
    private FileNode? _draggedNode;
    private bool _isWheelSaving;
    private bool _isGlobalMode = false;
    private readonly Stack<string> _backHistory = new();
    private readonly Stack<string> _forwardHistory = new();
    private bool _isNavigatingHistory;
    private readonly HashSet<string> _approvedWarningFiles = new(StringComparer.OrdinalIgnoreCase);
    
    private FoldingManager? _foldingManager;
    private EditorHistoryManager _historyManager;
    private BraceFoldingStrategy? _foldingStrategy;
    private DispatcherTimer? _foldingTimer;
    private ErrorColorizer _errorColorizer;
    private DispatcherTimer _validationTimer;
    private AngelScriptAutocompleteManager _autocompleteManager;
    private TerminalExecutionService _terminalService = new();
    
   public AngelScriptEditorView()
{
    InitializeComponent();
    Loaded += async (s, e) =>
    {
        CodeEditor.FontSize = SettingsService.Instance.Current.EditorFontSize;
        _autocompleteManager = new AngelScriptAutocompleteManager(CodeEditor);
        
        InitCodeFolding();
        InitAdditionalFeatures();
        ConfigureEditorSelection();

        ProjectService.Instance.OnTreeStructureChanged += () => Dispatcher.Invoke(LoadProjectTree);
        ProjectService.Instance.OnFileChangedExternally += path => Dispatcher.Invoke(() => OnFileChanged(path));
        ProjectService.Instance.OnActiveFileDeletedExternally += path => Dispatcher.Invoke(() => OnFileDeleted(path));
        ProjectService.Instance.OnActiveFileRenamedExternally += (oldPath, newPath) => Dispatcher.Invoke(() => OnFileRenamed(oldPath, newPath));
        _terminalService.OutputReceived += text => Dispatcher.Invoke(() => LoggerService.Instance.Log(text, "#D4D4D4"));
        _terminalService.ErrorReceived += text => Dispatcher.Invoke(() => LoggerService.Instance.Log(text, "#CD5C5C"));
        LoggerService.Instance.OnLogAdded += entry => Dispatcher.Invoke(() => ConsoleOutput.Items.Add(entry));
        LoggerService.Instance.OnLogCleared += () => Dispatcher.Invoke(() => ConsoleOutput.Items.Clear());
        _historyManager = new EditorHistoryManager(CodeEditor);
        var settings = SettingsService.Instance.Current;
        CompilePathInput.Text = settings.CustomAngelScriptCompilePath ?? string.Empty;

        if (settings.RecentAngelScriptFolders.Count > 0)
        {
            string lastFolderPath = settings.RecentAngelScriptFolders[0];
            await OpenProject(lastFolderPath);

            if (!string.IsNullOrEmpty(settings.LastOpenedAngelScriptFilePath) && File.Exists(settings.LastOpenedAngelScriptFilePath))
            {
                OpenFile(settings.LastOpenedAngelScriptFilePath);
            }
        }

        _ = LoadAngelScriptHighlightingAsync();
    };

    Unloaded += (s, e) =>
    {
        SaveCurrentTreeState();
    };
    
    CodeEditor.TextChanged += CodeEditor_TextChanged;
    CodeEditor.PreviewMouseWheel += CodeEditor_PreviewMouseWheel;
}
   
    private void ConfigureEditorSelection()
    {
        CodeEditor.TextArea.SelectionForeground = null;
        CodeEditor.TextArea.SelectionBrush = new SolidColorBrush(Color.FromArgb(0x3D, 0x4D, 0x7C, 0xFE));
        CodeEditor.TextArea.SelectionBorder = null;
    }
    
    
    private void SaveCurrentTreeState()
    {
        var currentNodes = FileTree.Items.Cast<FileNode>().ToList();
        if (currentNodes.Any())
        {
            FileTreeStateService.Instance.SaveExpansionState(currentNodes, false);
        }

        SettingsService.Instance.Current.CustomAngelScriptCompilePath = CompilePathInput.Text;
        _ = SettingsService.Instance.SaveAsync();
    }

    private void FileTree_KeyDown(object sender, KeyEventArgs e)
    {
        if (FileTree.SelectedItem is not FileNode node) return;

        HotkeySettings hotkeys = SettingsService.Instance.Current.Hotkeys;
        
        if (HotkeyMatcher.IsMatch(e, hotkeys.RenameFile, hotkeys.RenameFileModifiers))
        {
            string? newName = ShowInputDialog("Rename", node.Key);
            if (string.IsNullOrEmpty(newName) || newName == node.Key) return;

            try
            {
                string targetPath = ProjectService.Instance.RenameNode(node, newName);
                if (!node.IsDirectory && _currentFilePath == node.FullPath)
                {
                    _currentFilePath = targetPath;
                    CurrentFileNameText.Text = newName;
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogError($"[Rename Error] {ex.Message}");
            }
        }
        else if (HotkeyMatcher.IsMatch(e, hotkeys.DeleteFile, hotkeys.DeleteFileModifiers))
        {
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
    }

    private void View_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        HotkeySettings hotkeys = SettingsService.Instance.Current.Hotkeys;

        if (HotkeyMatcher.IsMatch(e, hotkeys.HideSearchPanelKey, hotkeys.HideSearchPanelModifiers) 
            && SearchPanel.Visibility == Visibility.Visible)
        {
            HideSearchPanel();
            e.Handled = true;
            return;
        }

        if (HotkeyMatcher.IsMatch(e, hotkeys.ToggleCommentKey, hotkeys.ToggleCommentModifiers))
        {
            CodeEditor.ToggleComment();
            e.Handled = true;
            return;
        }

        if (HotkeyMatcher.IsMatch(e, hotkeys.GlobalSearchKey, hotkeys.GlobalSearchModifiers))
        {
            ShowGlobalSearch();
            e.Handled = true;
            return;
        }

        if (HotkeyMatcher.IsMatch(e, hotkeys.SaveFileKey, hotkeys.SaveFileModifiers))
        {
            SaveCurrentFile();
            e.Handled = true;
            return;
        }

        if (HotkeyMatcher.IsMatch(e, hotkeys.SearchPanelKey, hotkeys.SearchPanelModifiers))
        {
            ShowSearchPanel();
            e.Handled = true;
            return;
        }

        if (HotkeyMatcher.IsMatch(e, hotkeys.DuplicateLineKey, hotkeys.DuplicateLineModifiers))
        {
            CodeEditor.DuplicateCurrentLine();
            e.Handled = true;
            return;
        }

        if (HotkeyMatcher.IsMatch(e, hotkeys.SaveAllKey, hotkeys.SaveAllModifiers))
        {
            SaveAllFiles();
            e.Handled = true;
            return;
        }

        if (HotkeyMatcher.IsMatch(e, hotkeys.RunCompilerKey, hotkeys.RunCompilerModifiers))
        {
            RunCompiler();
            e.Handled = true;
            return;
        }

        if (HotkeyMatcher.IsMatch(e, hotkeys.RedoKey, hotkeys.RedoModifiers))
        {
            if (CodeEditor.CanRedo)
            {
                CodeEditor.Redo();
            }
            else
            {
                _historyManager.Redo();
            }
            e.Handled = true;
            return;
        }

        if (HotkeyMatcher.IsMatch(e, hotkeys.UndoKey, hotkeys.UndoModifiers))
        {
            _historyManager.Undo();
            e.Handled = true;
            return;
        }
    }

    private void SaveAll_Click(object sender, RoutedEventArgs e)
    {
        SaveAllFiles();
    }

    private void SaveAllFiles()
    {
        SaveCurrentFile();

        try
        {
            ProjectService.Instance.SuspendWatcher();

            var pendingFiles = TempFileService.Instance.GetPendingFiles();

            foreach (var filePath in pendingFiles)
            {
                if (!File.Exists(filePath)) continue;

                string content = TempFileService.Instance.GetTemp(filePath);
                if (content != null)
                {
                    File.WriteAllText(filePath, content, Encoding.UTF8);
                    TempFileService.Instance.ClearTemp(filePath);
                }
            }

            _isUnsaved = false;
            SetUnsavedStatus(false);
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogError($"[Save All Error] {ex.Message}");
        }
        finally
        {
            ProjectService.Instance.ResumeWatcher();
        }
    }

    private void NavigateBack()
    {
        if (_backHistory.Count == 0 || string.IsNullOrEmpty(_currentFilePath)) return;

        _isNavigatingHistory = true;
        _forwardHistory.Push(_currentFilePath);
        string prevFile = _backHistory.Pop();
        OpenFile(prevFile);
        _isNavigatingHistory = false;
    }

    private void NavigateForward()
    {
        if (_forwardHistory.Count == 0 || string.IsNullOrEmpty(_currentFilePath)) return;

        _isNavigatingHistory = true;
        _backHistory.Push(_currentFilePath);
        string nextFile = _forwardHistory.Pop();
        OpenFile(nextFile);
        _isNavigatingHistory = false;
    }

    private void View_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.XButton1)
        {
            NavigateBack();
            e.Handled = true;
        }
        else if (e.ChangedButton == MouseButton.XButton2)
        {
            NavigateForward();
            e.Handled = true;
        }
    }

    private void ShowGlobalSearch()
    {
        _isGlobalMode = true;
        SearchModeTitle.Text = "GLOBAL";
        SearchModeTitle.Foreground = new SolidColorBrush(Color.FromRgb(0x4D, 0x7C, 0xFE));
        SearchPanel.Visibility = Visibility.Visible;
        SearchTextBox.Focus();

        if (!string.IsNullOrEmpty(CodeEditor.SelectedText))
        {
            SearchTextBox.Text = CodeEditor.SelectedText;
        }

        SearchTextBox.SelectAll();
        ExecuteGlobalSearch();
    }

    private void ExecuteGlobalSearch()
    {
        string textToFind = SearchTextBox.Text;
        ConsoleOutput.Items.Clear();

        if (string.IsNullOrEmpty(textToFind))
        {
            ConsoleOutput.Items.Add(new { Text = "Search text is empty.", Color = Brushes.Gray });
            return;
        }

        string projectDir = ProjectService.Instance.CurrentFolderPath;
        if (string.IsNullOrEmpty(projectDir) && !string.IsNullOrEmpty(_currentFilePath))
        {
            projectDir = Path.GetDirectoryName(_currentFilePath);
        }

        if (string.IsNullOrEmpty(projectDir))
        {
            ConsoleOutput.Items.Add(new { Text = "No active project directory found.", Color = Brushes.Red });
            return;
        }

        try
        {
            var searchEngine = new EditorSearchEngine();
            var results = searchEngine.RunGlobalSearch(
                projectDir,
                textToFind,
                RegexToggle.IsChecked == true,
                WholeWordToggle.IsChecked == true,
                MatchCaseToggle.IsChecked == true,
                AppMetadata.SupportedExtensions
            );

            if (results.Count == 0)
            {
                ConsoleOutput.Items.Add(new { Text = "No matches found.", Color = Brushes.Gray });
                return;
            }

            foreach (var result in results)
            {
                string logMessage = $"[{result.FilePath}] ({result.LineNumber}): {result.LineText}";
                ConsoleOutput.Items.Add(new { Text = logMessage, Color = Brushes.LightBlue });
            }
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogError($"Global search error: {ex.Message}");
        }
    }

    private void InitAdditionalFeatures()
    {
        CodeEditor.TextArea.TextView.BackgroundRenderers.Add(new CurrentLineBackgroundRenderer(CodeEditor));
        CodeEditor.TextArea.TextView.BackgroundRenderers.Add(new IndentationGuideRenderer(CodeEditor));
        CodeEditor.TextArea.TextView.BackgroundRenderers.Add(new SelectionMatchRenderer(CodeEditor));

        CodeEditor.TextArea.Caret.PositionChanged += (s, e) =>
        {
            CodeEditor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
        };

        CodeEditor.PreviewMouseWheel += (o, e) =>
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true;
            }
        };
    }

    private void RefreshRecentFoldersSubmenu()
    {
        RecentFoldersMenu.Items.Clear();

        var settings = SettingsService.Instance.Current;
        if (settings.RecentAngelScriptFolders == null || settings.RecentAngelScriptFolders.Count == 0)
        {
            RecentFoldersMenu.Items.Add(new MenuItem { Header = "No recent projects", IsEnabled = false });
        }
        else
        {
            foreach (var path in settings.RecentAngelScriptFolders)
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

    private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        var hotkeys = SettingsService.Instance.Current.Hotkeys;

        if (HotkeyMatcher.IsMatch(e, hotkeys.HideSearchPanelKey, hotkeys.HideSearchPanelModifiers))
        {
            HideSearchPanel();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            if (_isGlobalMode)
            {
                ExecuteGlobalSearch();
            }
            else
            {
                FindMatch(Keyboard.Modifiers == ModifierKeys.Shift);
            }

            e.Handled = true;
        }
    }

    private void PrevMatch_Click(object sender, RoutedEventArgs e) => FindMatch(true);
    private void NextMatch_Click(object sender, RoutedEventArgs e) => FindMatch(false);
    private void CloseSearch_Click(object sender, RoutedEventArgs e) => HideSearchPanel();

    private void ShowSearchPanel()
    {
        _isGlobalMode = false;
        SearchModeTitle.Text = "LOCAL";
        SearchModeTitle.Foreground = new SolidColorBrush(Color.FromRgb(0xE2, 0xE2, 0xE9));
        SearchPanel.Visibility = Visibility.Visible;
        SearchTextBox.Focus();

        if (!string.IsNullOrEmpty(CodeEditor.SelectedText))
        {
            SearchTextBox.Text = CodeEditor.SelectedText;
        }

        SearchTextBox.SelectAll();
    }

    private void HideSearchPanel()
    {
        SearchPanel.Visibility = Visibility.Collapsed;
        CodeEditor.Focus();
    }

    private void FindMatch(bool backward)
    {
        string textToFind = SearchTextBox.Text;
        if (string.IsNullOrEmpty(textToFind)) return;

        try
        {
            var searchEngine = new EditorSearchEngine();
            int matchIndex = searchEngine.FindMatchOffset(
                CodeEditor.Text,
                textToFind,
                CodeEditor.SelectionStart,
                CodeEditor.SelectionLength,
                backward,
                RegexToggle.IsChecked == true,
                WholeWordToggle.IsChecked == true,
                MatchCaseToggle.IsChecked == true,
                out int matchLength
            );

            if (matchIndex != -1)
            {
                CodeEditor.Select(matchIndex, matchLength);
                var line = CodeEditor.Document.GetLineByOffset(matchIndex);
                CodeEditor.ScrollTo(line.LineNumber, CodeEditor.TextArea.Caret.VisualColumn);
            }
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogError($"Search error: {ex.Message}");
        }
    }

    private async void CodeEditor_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            double newSize = CodeEditor.FontSize + (e.Delta > 0 ? 1 : -1);
            if (newSize >= 8 && newSize <= 36)
            {
                CodeEditor.FontSize = newSize;
                SettingsService.Instance.Current.EditorFontSize = newSize;

                if (!_isWheelSaving)
                {
                    _isWheelSaving = true;
                    try
                    {
                        await SettingsService.Instance.SaveAsync();
                    }
                    finally
                    {
                        _isWheelSaving = false;
                    }
                }
            }

            e.Handled = true;
        }
    }

    private void InitCodeFolding()
    {
        _foldingManager = FoldingManager.Install(CodeEditor.TextArea);

        var defaultMargin = CodeEditor.TextArea.LeftMargins.OfType<FoldingMargin>().FirstOrDefault();
        if (defaultMargin != null)
        {
            int index = CodeEditor.TextArea.LeftMargins.IndexOf(defaultMargin);
            CodeEditor.TextArea.LeftMargins.RemoveAt(index);

            var customMargin = new ArrowFoldingMargin(_foldingManager);
            CodeEditor.TextArea.LeftMargins.Insert(index, customMargin);
        }

        _foldingStrategy = new BraceFoldingStrategy();
        _foldingStrategy.UpdateFoldings(_foldingManager, CodeEditor.Document);

        _foldingTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _foldingTimer.Tick += (s, e) => { _foldingStrategy.UpdateFoldings(_foldingManager, CodeEditor.Document); };
        _foldingTimer.Start();

        _errorColorizer = new ErrorColorizer();
        CodeEditor.TextArea.TextView.LineTransformers.Add(_errorColorizer);

        _validationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _validationTimer.Tick += (s, e) =>
        {
            _validationTimer.Stop();
            ValidateSyntax();
        };
    }

    private void ValidateSyntax()
    {
        _errorColorizer.Errors.Clear();

        var linesData = new List<(string Text, int Offset)>();
        foreach (var line in CodeEditor.Document.Lines)
        {
            linesData.Add((CodeEditor.Document.GetText(line), line.Offset));
        }

        var serviceErrors = SyntaxValidationService.Instance.Validate(linesData);

        foreach (var err in serviceErrors)
        {
            _errorColorizer.Errors.Add(new SyntaxError
            {
                Offset = err.Offset,
                Length = err.Length,
                Message = err.Message
            });
        }

        CodeEditor.TextArea.TextView.Redraw();
    }

    private async Task LoadAngelScriptHighlightingAsync()
    {
        var settings = SettingsService.Instance.Current;
        bool isGitHub = settings.FetchPriority == FetchPrioritySource.GitHub;

        string primaryUrl = isGitHub ? settings.SyntaxGitHubUrl : settings.SyntaxPastebinUrl;
        string secondaryUrl = isGitHub ? settings.SyntaxPastebinUrl : settings.SyntaxGitHubUrl;

        string? xshdCode = await TryFetchStringAsync(primaryUrl) ?? await TryFetchStringAsync(secondaryUrl);

        if (!string.IsNullOrWhiteSpace(xshdCode))
        {
            xshdCode = await EnhanceXshdWithRemoteClassesAsync(xshdCode);
            if (TryApplyHighlighting(xshdCode))
            {
                return;
            }
        }

        await ApplyFallbackHighlightingAsync();
    }

    private async Task ApplyFallbackHighlightingAsync()
{
    try
    {
        string fallbackXshd = AngelScriptSyntax.GetFallbackXshd();
        fallbackXshd = await EnhanceXshdWithRemoteClassesAsync(fallbackXshd);
        using var reader = new XmlTextReader(new StringReader(fallbackXshd));
        CodeEditor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }
    catch (Exception ex)
    {
        LoggerService.Instance.LogError($"[Editor Error] Failed to load local fallback: {ex.Message}");
    }
}

private bool TryApplyHighlighting(string xshdContent)
{
    try
    {
        using var reader = new XmlTextReader(new StringReader(xshdContent));
        CodeEditor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
        return true;
    }
    catch
    {
        return false;
    }
}

private async Task<string> EnhanceXshdWithRemoteClassesAsync(string xshdCode)
{
    try
    {
        var settings = SettingsService.Instance.Current;
        bool isGitHub = settings.FetchPriority == FetchPrioritySource.GitHub;
        string primaryJsonUrl = isGitHub ? settings.CompletionGitHubUrl : settings.CompletionPastebinUrl;
        string secondaryJsonUrl = isGitHub ? settings.CompletionPastebinUrl : settings.CompletionGitHubUrl;

        string? json = await TryFetchStringAsync(primaryJsonUrl) ?? await TryFetchStringAsync(secondaryJsonUrl);
        if (string.IsNullOrWhiteSpace(json)) return xshdCode;

        var completions = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json);
        if (completions == null || completions.Count == 0) return xshdCode;

        var doc = new XmlDocument();
        doc.LoadXml(xshdCode);

        var root = doc.DocumentElement;
        if (root == null) return xshdCode;
        string ns = root.NamespaceURI;

        var firstRuleSet = doc.SelectSingleNode("//*[local-name()='RuleSet']");
        if (firstRuleSet == null) return xshdCode;

        var classes = completions.Keys.Where(k => k != "Global").ToList();
        if (classes.Count > 0)
        {
            var classColorNode = doc.CreateElement("Color", ns);
            classColorNode.SetAttribute("name", "DynamicClasses");
            classColorNode.SetAttribute("foreground", "#4EC9B0");
            classColorNode.SetAttribute("fontWeight", "bold");
            root.InsertBefore(classColorNode, firstRuleSet);

            var classKeywordsNode = doc.CreateElement("Keywords", ns);
            classKeywordsNode.SetAttribute("color", "DynamicClasses");
            foreach (var cls in classes)
            {
                var wordNode = doc.CreateElement("Word", ns);
                wordNode.InnerText = cls;
                classKeywordsNode.AppendChild(wordNode);
            }
            firstRuleSet.AppendChild(classKeywordsNode);
        }

        if (completions.TryGetValue("Global", out var globals) && globals.Count > 0)
        {
            var methodColorNode = doc.CreateElement("Color", ns);
            methodColorNode.SetAttribute("name", "DynamicMethods");
            methodColorNode.SetAttribute("foreground", "#DCDCAA");
            root.InsertBefore(methodColorNode, firstRuleSet);

            var methodKeywordsNode = doc.CreateElement("Keywords", ns);
            methodKeywordsNode.SetAttribute("color", "DynamicMethods");
            foreach (var methodKey in globals.Keys)
            {
                string cleanMethodName = methodKey.Split('(')[0].Trim();
                if (!string.IsNullOrEmpty(cleanMethodName))
                {
                    var wordNode = doc.CreateElement("Word", ns);
                    wordNode.InnerText = cleanMethodName;
                    methodKeywordsNode.AppendChild(wordNode);
                }
            }
            firstRuleSet.AppendChild(methodKeywordsNode);
        }

        using var sw = new StringWriter();
        using var xw = XmlWriter.Create(sw);
        doc.Save(xw);
        return sw.ToString();
    }
    catch
    {
        return xshdCode;
    }
}

    private async Task<string?> TryFetchStringAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            return await client.GetStringAsync(url);
        }
        catch
        {
            return null;
        }
    }
    
    private void ApplyFallbackHighlighting()
    {
        try
        {
            using var reader = new XmlTextReader(new StringReader(AngelScriptSyntax.GetFallbackXshd()));
            CodeEditor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogError($"[Editor Error] Failed to load local fallback: {ex.Message}");
        }
    }

    private void BackToMenu_Click(object sender, RoutedEventArgs e)
    {
        BackToMenu();
    }

    public void BackToMenu()
    {
        if (_isUnsaved)
        {
            var result = ModernMessageBox.Show(
                Window.GetWindow(this),
                "You have unsaved changes. Are you sure you want to exit?",
                "Unsaved Changes",
                ModernBoxType.Question
            );

            if (result.Result != ModernBoxResultType.Yes)
            {
                return;
            }
        }

        Window currentWindow = Window.GetWindow(this);
        if (currentWindow != null)
        {
            dynamic mainWindow = currentWindow;
            mainWindow.NavigateToMenu();
        }
    }

    private void ClearConsole_Click(object sender, RoutedEventArgs e)
    {
        LoggerService.Instance.Clear();
    }

    private async void RunCompiler_Click(object sender, RoutedEventArgs e)
    {
        RunCompiler();
    }

    private async void RunCompiler()
    {
        LoggerService.Instance.Clear();
        LoggerService.Instance.LogInfo("Compilation started...");

        try
        {
            if (!string.IsNullOrWhiteSpace(_currentFilePath))
            {
                await File.WriteAllTextAsync(_currentFilePath, CodeEditor.Text, Encoding.UTF8);
                _isUnsaved = false;
            }

            string input = OutputNameInput.Text;
            string outputName = string.IsNullOrWhiteSpace(input)
                ? "output"
                : Path.GetFileNameWithoutExtension(input.Trim());

            string? compilePath = !string.IsNullOrWhiteSpace(CompilePathInput.Text) 
                ? CompilePathInput.Text 
                : _currentFilePath;

            string? codeToSend = null;
            if (string.IsNullOrWhiteSpace(CompilePathInput.Text) || 
                string.Equals(CompilePathInput.Text, _currentFilePath, StringComparison.OrdinalIgnoreCase))
            {
                codeToSend = CodeEditor.Text;
            }

            var result = await CompilerService.Instance.RunCompilerAsync(codeToSend, compilePath, outputName);

            foreach (var entry in result)
            {
                LoggerService.Instance.Log(entry.Text, entry.Color);
            }

            if (AutoscrollToggle.IsChecked == true)
            {
                ScrollConsoleToBottom();
            }
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogError($"Critical UI Error: {ex.Message}");
        }
    }
    
    private void AutoscrollToggle_Checked(object sender, RoutedEventArgs e)
    {
        ScrollConsoleToBottom();
    }

    private void ScrollConsoleToBottom()
    {
        if (ConsoleScrollViewer == null) return;

        ConsoleScrollViewer.Dispatcher.BeginInvoke(DispatcherPriority.Background,
            new Action(() => { ConsoleScrollViewer.ScrollToEnd(); }));
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Project Folder"
        };

        if (dialog.ShowDialog() == true)
        {
            OpenProject(dialog.FolderName);
        }
    }

    private async Task OpenProject(string path)
    {
        SaveCurrentTreeState();

        ProjectService.Instance.OpenProject(path);
        LoadProjectTree();
        var settings = SettingsService.Instance.Current;
        settings.RecentAngelScriptFolders.Remove(path);
        settings.RecentAngelScriptFolders.Insert(0, path);

        if (settings.RecentAngelScriptFolders.Count > 5)
        {
            settings.RecentAngelScriptFolders.RemoveAt(settings.RecentAngelScriptFolders.Count - 1);
        }

        await SettingsService.Instance.SaveAsync();
    }

    private void OnFileChanged(string fullPath)
    {
        if (string.IsNullOrEmpty(_currentFilePath) ||
            !_currentFilePath.Equals(fullPath, StringComparison.OrdinalIgnoreCase)) return;

        if (!_isUnsaved)
        {
            _isSuppressingTextEvents = true;
            try
            {
                CodeEditor.Document.Text = File.ReadAllText(_currentFilePath);
            }
            catch
            {
            }

            _isSuppressingTextEvents = false;
        }
        else
        {
            var result = ModernMessageBox.Show(
                Window.GetWindow(this),
                $"The file '{Path.GetFileName(fullPath)}' has been modified by another program. Reload and lose changes?",
                "File Modified",
                ModernBoxType.Question
            );

            if (result.Result == ModernBoxResultType.Yes)
            {
                _isSuppressingTextEvents = true;
                try
                {
                    CodeEditor.Document.Text = File.ReadAllText(_currentFilePath);
                    _isUnsaved = false;
                    SetUnsavedStatus(false);
                    TempFileService.Instance.ClearTemp(_currentFilePath);
                }
                catch
                {
                }

                _isSuppressingTextEvents = false;
            }
        }
    }

    private void OnFileDeleted(string fullPath)
    {
        if (!string.IsNullOrEmpty(_currentFilePath) &&
            _currentFilePath.Equals(fullPath, StringComparison.OrdinalIgnoreCase))
        {
            _autocompleteManager?.ClearWindow(); 
        
            TempFileService.Instance.ClearTemp(_currentFilePath);
            _currentFilePath = null;
            _isUnsaved = false;

            _foldingManager?.Clear();
            CodeEditor.Document.Text = string.Empty;

            CurrentFileNameText.Text = string.Empty;
            SetUnsavedStatus(false);
        }
    }

    private void OnFileRenamed(string oldFullPath, string fullPath)
    {
        if (!string.IsNullOrEmpty(_currentFilePath) &&
            _currentFilePath.Equals(oldFullPath, StringComparison.OrdinalIgnoreCase))
        {
            string tempText = TempFileService.Instance.GetTemp(oldFullPath);
            if (tempText != null)
            {
                TempFileService.Instance.SaveTemp(fullPath, tempText);
                TempFileService.Instance.ClearTemp(oldFullPath);
            }

            _currentFilePath = fullPath;
            CurrentFileNameText.Text = Path.GetFileName(fullPath);
        }

        DiscordRpcService.UpdateToEditing(Path.GetFileName(fullPath));
    }

    private void LoadProjectTree()
    {
        var currentNodes = FileTree.Items.Cast<FileNode>().ToList();
        if (currentNodes.Any())
        {
            FileTreeStateService.Instance.SaveExpansionState(currentNodes, false);
        }

        FileTree.Items.Clear();
        var nodes = ProjectService.Instance.BuildProjectTree();

        FileTreeStateService.Instance.RestoreTreeState(nodes, false);

        foreach (var child in nodes)
        {
            FileTree.Items.Add(child);
        }
    }

    private void FileTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is FileNode node && !node.IsDirectory)
        {
            if (!File.Exists(node.FullPath)) return;

            if (!_approvedWarningFiles.Contains(node.FullPath))
            {
                FileInfo fileInfo = new FileInfo(node.FullPath);
                bool warningTriggered = false;

                if (fileInfo.Length > AppMetadata.AddonEditorMaxFileSizeBytes)
                {
                    warningTriggered = true;
                    double fileSizeMb = (double)fileInfo.Length / (1024 * 1024);
                    var boxResult = ModernMessageBox.Show(
                        Window.GetWindow(this),
                        $"The file '{node.Key}' is very large ({fileSizeMb:F1} MB). Opening it may cause the editor to freeze or crash. Are you sure you want to proceed?",
                        "Warning",
                        ModernBoxType.Question
                    );

                    if (boxResult.Result != ModernBoxResultType.Yes)
                    {
                        return;
                    }
                }

                string extension = Path.GetExtension(node.FullPath);
                if (!AppMetadata.SupportedExtensions.Contains(extension))
                {
                    warningTriggered = true;
                    var boxResult = ModernMessageBox.Show(
                        Window.GetWindow(this),
                        $"The file '{node.Key}' has an unsupported format ({extension}). Are you sure you want to open it in the text editor?",
                        "Warning",
                        ModernBoxType.Question
                    );

                    if (boxResult.Result != ModernBoxResultType.Yes)
                    {
                        return;
                    }
                }

                if (warningTriggered)
                {
                    _approvedWarningFiles.Add(node.FullPath);
                }
            }

            OpenFile(node.FullPath);
        }
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

    private async void TerminalInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            string command = TerminalInput.Text.Trim();
            if (string.IsNullOrEmpty(command)) return;

            TerminalInput.Text = string.Empty;

            LoggerService.Instance.Log($"> {command}", "#808080");
            if (AutoscrollToggle.IsChecked == true) ScrollConsoleToBottom();

            string workDir = ProjectService.Instance.CurrentFolderPath ?? AppDomain.CurrentDomain.BaseDirectory;
            await _terminalService.ExecuteAsync(command, workDir);

            if (AutoscrollToggle.IsChecked == true) ScrollConsoleToBottom();
        }
    }
    

    private void CodeEditor_TextChanged(object sender, EventArgs e)
    {
        if (_isSuppressingTextEvents || string.IsNullOrEmpty(_currentFilePath)) return;

        _isUnsaved = true;
        SetUnsavedStatus(true);
        TempFileService.Instance.SaveTemp(_currentFilePath, CodeEditor.Text);

        _foldingTimer.Stop();
        _foldingTimer.Start();

        _validationTimer.Stop();
        _validationTimer.Start();
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

    private void OpenFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

        _autocompleteManager?.ClearWindow();
        
        if (!_isNavigatingHistory && !string.IsNullOrEmpty(_currentFilePath) && _currentFilePath != filePath)
        {
            _backHistory.Push(_currentFilePath);
            _forwardHistory.Clear();
        }

        _historyManager.SwitchFile(_currentFilePath, filePath);

        _isSuppressingTextEvents = true;
        _currentFilePath = filePath;
        _foldingManager?.Clear();

        string tempText = TempFileService.Instance.GetTemp(_currentFilePath);
        if (tempText != null)
        {
            CodeEditor.Document.Text = tempText;
            _isUnsaved = true;
            SetUnsavedStatus(true);
        }
        else
        {
            CodeEditor.Document.Text = File.ReadAllText(_currentFilePath);
            _isUnsaved = false;
            SetUnsavedStatus(false);
        }

        CodeEditor.Document.UndoStack.SizeLimit = 0;

        _foldingStrategy?.UpdateFoldings(_foldingManager, CodeEditor.Document);
        CurrentFileNameText.Text = Path.GetFileName(_currentFilePath);
        _isSuppressingTextEvents = false;
        DiscordRpcService.UpdateToEditing(Path.GetFileName(_currentFilePath));

        FileTreeStateService.Instance.SaveLastOpenedFile(filePath);
    }

    private void SaveFile_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentFile();
    }

    private void SaveCurrentFile()
    {
        if (string.IsNullOrEmpty(_currentFilePath)) return;

        try
        {
            ProjectService.Instance.SuspendWatcher();

            File.WriteAllText(_currentFilePath, CodeEditor.Document.Text);
            _isUnsaved = false;
            SetUnsavedStatus(false);
            TempFileService.Instance.ClearTemp(_currentFilePath);
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogError($"[Save Error] Failed to save file: {ex.Message}");
        }
        finally
        {
            ProjectService.Instance.ResumeWatcher();
        }
    }

    private void SetUnsavedStatus(bool unsaved)
    {
        if (UnsavedDot != null)
        {
            UnsavedDot.Visibility = unsaved ? Visibility.Visible : Visibility.Collapsed;
        }

        if (string.IsNullOrEmpty(_currentFilePath) || FileTree.Items.Count == 0) return;

        string targetPath = Path.GetFullPath(_currentFilePath);
        
        foreach (var item in FileTree.Items)
        {
            if (item is FileNode rootNode)
            {
                var foundNode = FindFileNode(rootNode, targetPath);
                if (foundNode != null)
                {
                    foundNode.IsUnsaved = unsaved; 
                    break;
                }
            }
        }
    }
    
    private FileNode? FindFileNode(FileNode node, string targetPath)
    {
        if (!node.IsDirectory && !string.IsNullOrEmpty(node.FullPath))
        {
            if (string.Equals(Path.GetFullPath(node.FullPath), targetPath, StringComparison.OrdinalIgnoreCase))
            {
                return node;
            }
        }

        foreach (var child in node.Value)
        {
            var found = FindFileNode(child, targetPath);
            if (found != null) return found;
        }

        return null;
    }

    private void RefreshTree_Click(object sender, RoutedEventArgs e) => LoadProjectTree();

    private void NewFile_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(ProjectService.Instance.CurrentFolderPath)) return;

        string? name = ShowInputDialog("New File", "untitled.as");
        if (string.IsNullOrEmpty(name)) return;

        ProjectService.Instance.CreateFile(ProjectService.Instance.CurrentFolderPath, name);
    }

    private void NewFolder_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(ProjectService.Instance.CurrentFolderPath)) return;

        string? name = ShowInputDialog("New Folder", "NewFolder");
        if (string.IsNullOrEmpty(name)) return;

        ProjectService.Instance.CreateDirectory(ProjectService.Instance.CurrentFolderPath, name);
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

    private void ContextMenu_NewFile_Click(object sender, RoutedEventArgs e)
    {
        FileNode? node = GetNodeFromMenu(sender);
        string? targetDir = node != null
            ? (node.IsDirectory ? node.FullPath : Path.GetDirectoryName(node.FullPath))
            : ProjectService.Instance.CurrentFolderPath;
        if (string.IsNullOrEmpty(targetDir)) return;

        string? name = ShowInputDialog("New File", "untitled.as");
        if (string.IsNullOrEmpty(name)) return;

        ProjectService.Instance.CreateFile(targetDir, name);
    }

    private void ContextMenu_NewFolder_Click(object sender, RoutedEventArgs e)
    {
        FileNode? node = GetNodeFromMenu(sender);
        string? targetDir = node != null
            ? (node.IsDirectory ? node.FullPath : Path.GetDirectoryName(node.FullPath))
            : ProjectService.Instance.CurrentFolderPath;
        if (string.IsNullOrEmpty(targetDir)) return;

        string? name = ShowInputDialog("New Folder", "NewFolder");
        if (string.IsNullOrEmpty(name)) return;

        ProjectService.Instance.CreateDirectory(targetDir, name);
    }

    private void ContextMenu_Rename_Click(object sender, RoutedEventArgs e)
    {
        FileNode? node = GetNodeFromMenu(sender);
        if (node == null) return;
        
        string? newName = ShowInputDialog("Rename", node.Key);
        if (string.IsNullOrEmpty(newName) || newName == node.Key) return;

        try
        {
            string targetPath = ProjectService.Instance.RenameNode(node, newName);
            if (!node.IsDirectory && _currentFilePath == node.FullPath)
            {
                _currentFilePath = targetPath;
                CurrentFileNameText.Text = newName;
            }
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogError($"[Rename Error] {ex.Message}");
        }
    }

    private void ContextMenu_RevealInExplorer_Click(object sender, RoutedEventArgs e)
    {
        FileNode? node = GetNodeFromMenu(sender);
        if (node != null && !string.IsNullOrEmpty(node.FullPath))
        {
            ShellService.Instance.RevealInExplorer(node.FullPath);
        }
    }

    private void ContextMenu_CopyPath_Click(object sender, RoutedEventArgs e)
    {
        FileNode? node = GetNodeFromMenu(sender);
        if (node == null || string.IsNullOrEmpty(node.FullPath)) return;
        
        try
        {
            Clipboard.SetText(node.FullPath);
        }
        catch
        {
        }
    }

    private void ContextMenu_CopyRelativePath_Click(object sender, RoutedEventArgs e)
    {
        FileNode? node = GetNodeFromMenu(sender);
        if (node != null && !string.IsNullOrEmpty(node.FullPath) &&
            !string.IsNullOrEmpty(ProjectService.Instance.CurrentFolderPath))
        {
            try
            {
                string relativePath = Path.GetRelativePath(ProjectService.Instance.CurrentFolderPath, node.FullPath);
                Clipboard.SetText(relativePath);
            }
            catch
            {
            }
        }
    }

    private void ContextMenu_Duplicate_Click(object sender, RoutedEventArgs e)
    {
        FileNode? node = GetNodeFromMenu(sender);
        if (node == null || string.IsNullOrEmpty(node.FullPath)) return;

        try
        {
            ProjectService.Instance.DuplicateNode(node);
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogError($"[Duplicate Error] {ex.Message}");
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

    private void CurrentFileNameText_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentFilePath) || !File.Exists(_currentFilePath)) return;

        string currentName = Path.GetFileName(_currentFilePath);
        string? newName = ShowInputDialog("Rename Open File", currentName);
        if (string.IsNullOrEmpty(newName) || newName == currentName) return;

        string? parentDir = Path.GetDirectoryName(_currentFilePath);
        if (string.IsNullOrEmpty(parentDir)) return;

        string targetPath = Path.Combine(parentDir, newName);
        try
        {
            File.Move(_currentFilePath, targetPath);
            _currentFilePath = targetPath;
            CurrentFileNameText.Text = newName;
            ProjectService.Instance.BuildProjectTree();
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogError($"[Rename Error] {ex.Message}");
        }
        finally
        {
            DiscordRpcService.UpdateToEditing(Path.GetFileName(_currentFilePath));
        }
    }

    private void FileTree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => _startPoint = e.GetPosition(null);
    
    private void FileTree_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        
        Point mousePos = e.GetPosition(null);
        Vector diff = _startPoint - mousePos;

        if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            DependencyObject? dep = e.OriginalSource as DependencyObject;
            while (dep != null && !(dep is TreeViewItem))
            {
                dep = VisualTreeHelper.GetParent(dep);
            }

            if (dep is TreeViewItem item)
            {
                _draggedNode = item.DataContext as FileNode;
                if (_draggedNode != null)
                {
                    DataObject dragData = new DataObject("FileNodeFormat", _draggedNode);
                    DragDrop.DoDragDrop(item, dragData, DragDropEffects.Move);
                }
            }
        }
    }

    private void FileTree_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = !e.Data.GetDataPresent("FileNodeFormat") ? DragDropEffects.None : DragDropEffects.Move;
        e.Handled = true;
    }

    private void FileTree_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent("FileNodeFormat"))
        {
            FileNode? droppedNode = e.Data.GetData("FileNodeFormat") as FileNode;
            DependencyObject? dep = e.OriginalSource as DependencyObject;
            while (dep != null && !(dep is TreeViewItem))
            {
                dep = VisualTreeHelper.GetParent(dep);
            }

            string? targetDir = ProjectService.Instance.CurrentFolderPath;
            if (dep is TreeViewItem item)
            {
                FileNode? targetNode = item.DataContext as FileNode;
                if (targetNode != null)
                {
                    targetDir = targetNode.IsDirectory
                        ? targetNode.FullPath
                        : Path.GetDirectoryName(targetNode.FullPath);
                }
            }

            if (droppedNode != null && !string.IsNullOrEmpty(targetDir))
            {
                try
                {
                    ProjectService.Instance.MoveNode(droppedNode.FullPath, targetDir, droppedNode.Key,
                        droppedNode.IsDirectory);
                }
                catch (Exception ex)
                {
                    LoggerService.Instance.LogError($"[Move Error] {ex.Message}");
                }
            }
        }
    }

    private FileNode? GetNodeFromMenu(object sender)
    {
        if (sender is MenuItem menuItem && menuItem.Parent is ContextMenu contextMenu && contextMenu.PlacementTarget is FrameworkElement element)
        {
            return element is TreeViewItem tvi ? tvi.DataContext as FileNode : element.DataContext as FileNode;
        }
        return FileTree.SelectedItem as FileNode;
    }

    private void ConsoleOutput_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var element = e.OriginalSource as FrameworkElement;
        if (element?.DataContext == null) return;

        dynamic entry = element.DataContext;
        string logText = entry.Text;

        var target = LogParser.ParseLogLine(logText);
        if (target != null)
        {
            if (File.Exists(target.FilePath) && target.FilePath != _currentFilePath)
            {
                OpenFile(target.FilePath);
            }

            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                if (target.Line > 0 && target.Line <= CodeEditor.Document.LineCount)
                {
                    var lineSegment = CodeEditor.Document.GetLineByNumber(target.Line);
                    CodeEditor.CaretOffset = lineSegment.Offset + Math.Min(target.Column - 1, lineSegment.Length);
                    CodeEditor.ScrollTo(target.Line, target.Column);
                    CodeEditor.Focus();
                }
            }));
        }
    }

    private void MakeArchive_Click(object sender, RoutedEventArgs e)
    {
        if (FileTree.SelectedItem is FileNode node) ProjectService.Instance.ArchiveNode(node);
    }
}