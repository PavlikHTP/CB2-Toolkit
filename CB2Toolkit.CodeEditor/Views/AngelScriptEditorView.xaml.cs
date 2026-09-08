using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml;
using CB2Toolkit.CodeEditor.Extensions;
using CB2Toolkit.CodeEditor.Models;
using CB2Toolkit.CodeEditor.Models.Enums;
using CB2Toolkit.CodeEditor.Renderers;
using CB2Toolkit.CodeEditor.Services;
using CB2Toolkit.CodeEditor.Syntax;
using CB2Toolkit.CodeEditor.Utils;
using CB2Toolkit.Core;
using CB2Toolkit.Core.Models;
using CB2Toolkit.Core.Models.Enums;
using CB2Toolkit.Core.Models.Settings;
using CB2Toolkit.Core.Services;
using CB2Toolkit.Core.Utilities;
using CB2Toolkit.Core.Utilities.Extensions;
using ValidationResult = CB2Toolkit.Core.Models.ValidationResult;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.Indentation;
using ICSharpCode.AvalonEdit.Rendering;
using Microsoft.Win32;

namespace CB2Toolkit.CodeEditor.Views;

public partial class AngelScriptEditorView : LifecycleUserControl
{
    private string? _currentFilePath;
    private bool _isUnsaved;

    private bool IsSuppressingTextEvents
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
    private readonly ObservableCollection<EditorTab> _openTabs = new();
    private readonly DispatcherTimer _indexRebuildTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _autoSaveTimer = new() { Interval = TimeSpan.FromSeconds(15) };
    private DateTime _lastEditTime = DateTime.MinValue;

    private static readonly Regex ContextWordRegex = new("[A-Za-zА-Яа-яЁё0-9_'-]+", RegexOptions.Compiled);
    private string? _contextMenuWord;

    private FoldingManager? _foldingManager;
    private TextHistoryManager _historyManager;
    private BraceFoldingStrategy? _foldingStrategy;
    private DispatcherTimer _foldingTimer;
    private ErrorColorizer _errorColorizer;
    private AngelScriptSyntaxColorizer _syntaxColorizer;
    private DispatcherTimer _validationTimer;
    private MouseHoverLogic _errorHoverLogic;
    private ToolTip _errorToolTip;
    private bool _isErrorListOpen;
    private static readonly Brush NoErrorsBrush = CreateFrozenBrush("#10B981");
    private static readonly Brush HasErrorsBrush = CreateFrozenBrush("#EF4444");
    private ValidationResult? _validation;
    private Dictionary<string, List<SymbolDeclaration>> _symbolsByName = new();
    private AngelScriptAutocompleteManager _autocompleteManager;
    private readonly TerminalExecutionService _terminalService = new();

    public AngelScriptEditorView()
    {
        InitializeComponent();

        TabsListBox.ItemsSource = _openTabs;
        CodeEditor.TextChanged += CodeEditor_TextChanged;
        CodeEditor.PreviewMouseWheel += CodeEditor_PreviewMouseWheel;
        CodeEditor.PreviewMouseMove += CodeEditor_PreviewMouseMove;
        CodeEditor.PreviewKeyUp += CodeEditor_PreviewKeyUp;

        PathTextBoxHelper.Attach(CompilePathInput);
    }

    private bool _ctrlClickCursorSet;

    private void CodeEditor_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.LeftCtrl or Key.RightCtrl && _ctrlClickCursorSet)
        {
            _ctrlClickCursorSet = false;
            CodeEditor.TextArea.Cursor = null;
        }
    }

    private void CodeEditor_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
        {
            if (_ctrlClickCursorSet)
            {
                _ctrlClickCursorSet = false;
                CodeEditor.TextArea.Cursor = null;
            }

            return;
        }

        bool canGo = false;
        var textPos = CodeEditor.GetPositionFromPoint(e.GetPosition(CodeEditor));
        if (textPos != null)
        {
            int offset = CodeEditor.Document.GetOffset(textPos.Value.Location);
            canGo = TryGetGoToTarget(offset, out _, out _);
        }

        if (canGo != _ctrlClickCursorSet)
        {
            CodeEditor.TextArea.Cursor = canGo ? Cursors.Hand : null;
            _ctrlClickCursorSet = canGo;
        }
    }

    protected override async Task OnViewLoadedAsync()
    {
        CodeEditor.FontSize = SettingsService.Instance.Current.EditorFontSize;
        _autocompleteManager = new AngelScriptAutocompleteManager(CodeEditor);

        InitCodeFolding();
        InitAdditionalFeatures();
        ConfigureEditorSelection();

        ProjectService.Instance.OnTreeStructureChanged += ProjectService_TreeStructureChanged;
        ProjectService.Instance.OnFileChangedExternally += ProjectService_FileChangedExternally;
        ProjectService.Instance.OnActiveFileDeletedExternally += ProjectService_ActiveFileDeletedExternally;
        ProjectService.Instance.OnActiveFileRenamedExternally += ProjectService_ActiveFileRenamedExternally;

        _terminalService.OutputReceived += TerminalService_OutputReceived;
        _terminalService.ErrorReceived += TerminalService_ErrorReceived;

        LoggerService.Instance.OnLogAdded += LoggerService_OnLogAdded;
        LoggerService.Instance.OnLogCleared += LoggerService_OnLogCleared;

        _indexRebuildTimer.Tick += (_, _) =>
        {
            _indexRebuildTimer.Stop();
            string? projectDir = ProjectService.Instance.CurrentFolderPath;
            if (!string.IsNullOrEmpty(projectDir))
            {
                _ = Task.Run(() => ProjectSymbolIndexService.Instance.Rebuild(projectDir));
            }
        };

        SpellCheckDictionaryService.Instance.EnsureDictionariesExist();
        _ = Task.Run(() => SpellCheckDictionaryService.Instance.Load());

        _historyManager = new TextHistoryManager(CodeEditor);
        var settings = SettingsService.Instance.Current;
        CompilePathInput.Text = settings.CustomAngelScriptCompilePath;

        if (settings.RecentAngelScriptFolders.Count > 0)
        {
            string lastFolderPath = settings.RecentAngelScriptFolders[0];
            await OpenProject(lastFolderPath);

            if (!string.IsNullOrEmpty(settings.LastOpenedAngelScriptFilePath) &&
                File.Exists(settings.LastOpenedAngelScriptFilePath))
            {
                OpenFile(settings.LastOpenedAngelScriptFilePath);
            }
        }

        _ = LoadAngelScriptHighlightingAsync();
    }

    protected override void OnViewUnloaded()
    {
        SaveCurrentTreeState();

        ProjectService.Instance.OnTreeStructureChanged -= ProjectService_TreeStructureChanged;
        ProjectService.Instance.OnFileChangedExternally -= ProjectService_FileChangedExternally;
        ProjectService.Instance.OnActiveFileDeletedExternally -= ProjectService_ActiveFileDeletedExternally;
        ProjectService.Instance.OnActiveFileRenamedExternally -= ProjectService_ActiveFileRenamedExternally;

        _terminalService.OutputReceived -= TerminalService_OutputReceived;
        _terminalService.ErrorReceived -= TerminalService_ErrorReceived;

        LoggerService.Instance.OnLogAdded -= LoggerService_OnLogAdded;
        LoggerService.Instance.OnLogCleared -= LoggerService_OnLogCleared;
    }

    private void ProjectService_TreeStructureChanged() => Dispatcher.InvokeAsync(() =>
    {
        LoadProjectTree();
        ScheduleIndexRebuild();
    });
    private void ProjectService_FileChangedExternally(string path) => Dispatcher.InvokeAsync(() => OnFileChanged(path));

    private void ProjectService_ActiveFileDeletedExternally(string path) =>
        Dispatcher.InvokeAsync(() => OnFileDeleted(path));

    private void ProjectService_ActiveFileRenamedExternally(string oldPath, string newPath) =>
        Dispatcher.InvokeAsync(() => OnFileRenamed(oldPath, newPath));

    private void TerminalService_OutputReceived(string text) =>
        Dispatcher.InvokeAsync(() => LoggerService.Instance.Log(text, LogType.Info, "#D4D4D4"));

    private void TerminalService_ErrorReceived(string text) =>
        Dispatcher.InvokeAsync(() => LoggerService.Instance.LogError(text));

    private void LoggerService_OnLogAdded(object entry) => Dispatcher.InvokeAsync(() => ConsoleOutput.Items.Add(entry));
    private void LoggerService_OnLogCleared() => Dispatcher.InvokeAsync(() => ConsoleOutput.Items.Clear());

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

        SettingsService.Instance.Current.CustomAngelScriptCompilePath = CompilePathInput.Text.SanitizePath();
        _ = SettingsService.Instance.SaveAsync();
    }

    private void FileTree_KeyDown(object sender, KeyEventArgs e)
    {
        if (FileTree.SelectedItem is not FileNode node) return;

        HotkeySettings hotkeys = SettingsService.Instance.Current.Hotkeys;

        if (HotkeyMatcher.IsMatch(e, hotkeys.RenameKey, hotkeys.RenameModifiers))
        {
            string? newName = ShowInputDialog("Rename", node.Key);
            if (string.IsNullOrEmpty(newName) || newName == node.Key) return;

            try
            {
                string targetPath = ProjectService.Instance.RenameNode(node, newName);
                _historyManager.RenameFile(node.FullPath, targetPath);
                if (!node.IsDirectory)
                {
                    RenameTab(node.FullPath, targetPath);
                    if (_currentFilePath == node.FullPath)
                    {
                        _currentFilePath = targetPath;
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogError($"[Rename Error] {ex.Message}");
            }
        }
        else if (HotkeyMatcher.IsMatch(e, hotkeys.DeleteKey, hotkeys.DeleteModifiers))
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

    private bool GoToDefinition(int offset)
    {
        if (!TryGetGoToTarget(offset, out int target, out string includePath)) return false;

        if (includePath.Length > 0)
        {
            OpenFile(includePath);
            return true;
        }

        JumpToOffset(target);
        return true;
    }

    private bool TryGetGoToTarget(int offset, out int targetOffset, out string includeFullPath)
    {
        targetOffset = -1;
        includeFullPath = string.Empty;
        if (string.IsNullOrEmpty(_currentFilePath)) return false;

        var line = CodeEditor.Document.GetLineByOffset(offset);
        string lineText = CodeEditor.Document.GetText(line.Offset, line.Length);

        if (RegexPatterns.IncludeLine.IsMatch(lineText))
        {
            int relStart = lineText.IndexOfAny(['"', '<']);
            if (relStart >= 0)
            {
                char openDelim = lineText[relStart];
                char closeDelim = openDelim == '"' ? '"' : '>';
                int relEnd = lineText.LastIndexOf(closeDelim);
                if (relStart >= 0 && relEnd > relStart &&
                    offset >= line.Offset + relStart && offset <= line.Offset + relEnd)
                {
                    string includePath = lineText.Substring(relStart + 1, relEnd - relStart - 1);
                    string baseDir = Path.GetDirectoryName(_currentFilePath)!;
                    string fullPath = Path.GetFullPath(Path.Combine(baseDir, includePath));

                    if (File.Exists(fullPath))
                    {
                        includeFullPath = fullPath;
                        return true;
                    }
                }
            }
        }

        string text = CodeEditor.Document.Text;
        int wordStart = offset;
        int wordEnd = offset;
        while (wordStart > 0 && (char.IsLetterOrDigit(text[wordStart - 1]) || text[wordStart - 1] == '_')) wordStart--;
        while (wordEnd < text.Length && (char.IsLetterOrDigit(text[wordEnd]) || text[wordEnd] == '_')) wordEnd++;
        if (wordStart == wordEnd) return false;

        string word = text.Substring(wordStart, wordEnd - wordStart);

        if (_validation?.UsageToDeclaration.TryGetValue(wordStart, out int declOffset) == true)
        {
            targetOffset = declOffset;
            return true;
        }

        if (_symbolsByName.TryGetValue(word, out var candidates))
        {
            var best = candidates.OrderBy(c => Math.Abs(c.Offset - wordStart)).FirstOrDefault();
            if (best != null)
            {
                targetOffset = best.Offset;
                return true;
            }
        }

        return false;
    }

    private void View_PreviewKeyDown(object sender, KeyEventArgs e) => HandleInputEvent(e);
    private void View_PreviewMouseDown(object sender, MouseButtonEventArgs e) => HandleInputEvent(e);

    protected void HandleInputEvent(RoutedEventArgs e)
    {
        var hotkeys = SettingsService.Instance.Current.Hotkeys;

        if (e is MouseButtonEventArgs mouseArgs && mouseArgs.ChangedButton == MouseButton.Left &&
            Keyboard.Modifiers == ModifierKeys.Control)
        {
            var textPos = CodeEditor.GetPositionFromPoint(mouseArgs.GetPosition(CodeEditor));
            if (textPos != null && GoToDefinition(CodeEditor.Document.GetOffset(textPos.Value.Location)))
            {
                e.Handled = true;
                return;
            }
        }

        if (TryTrigger(e, hotkeys.HideSearchPanelKey, hotkeys.HideSearchPanelModifiers, HideSearchPanel,
                () => SearchPanel.Visibility == Visibility.Visible)) return;
        if (TryTrigger(e, hotkeys.ToggleCommentKey, hotkeys.ToggleCommentModifiers,
                () => CodeEditor.ToggleComment())) return;
        if (TryTrigger(e, hotkeys.GlobalSearchKey, hotkeys.GlobalSearchModifiers, ShowGlobalSearch)) return;
        if (TryTrigger(e, hotkeys.SaveFileKey, hotkeys.SaveFileModifiers, SaveCurrentFile)) return;
        if (TryTrigger(e, hotkeys.SearchPanelKey, hotkeys.SearchPanelModifiers, ShowSearchPanel)) return;
        if (TryTrigger(e, hotkeys.DuplicateKey, hotkeys.DuplicateModifiers,
                () => CodeEditor.DuplicateCurrentLine())) return;
        if (TryTrigger(e, hotkeys.SaveAllKey, hotkeys.SaveAllModifiers, SaveAllFiles)) return;
        if (TryTrigger(e, hotkeys.RunCompilerKey, hotkeys.RunCompilerModifiers, RunCompiler)) return;
        if (TryTrigger(e, hotkeys.UndoKey, hotkeys.UndoModifiers, () => _historyManager.Undo())) return;
        if (TryTrigger(e, hotkeys.NavigateBackKey, hotkeys.NavigateBackModifiers, NavigateBack)) return;
        if (TryTrigger(e, hotkeys.NavigateForwardKey, hotkeys.NavigateForwardModifiers, NavigateForward)) return;
        if (TryTrigger(e, hotkeys.FormatKey, hotkeys.FormatModifiers, () => CodeEditor.FormatSelection())) return;
        if (TryTrigger(e, hotkeys.GoToDefinitionKey, hotkeys.GoToDefinitionModifiers,
                () => GoToDefinition(CodeEditor.CaretOffset))) return;
        if (TryTrigger(e, hotkeys.AutoCompleteKey, hotkeys.AutoCompleteModifiers,
                () => _autocompleteManager.ShowCompletionManually())) return;

        if (TryTrigger(e, hotkeys.RedoKey, hotkeys.RedoModifiers, () =>
            {
                if (CodeEditor.CanRedo) CodeEditor.Redo();
                else _historyManager.Redo();
            })) return;
    }

    private bool TryTrigger(RoutedEventArgs e, string keySetting, string modifiersSetting, Action action,
        Func<bool> condition = null)
    {
        bool isMatch = false;

        if (e is KeyEventArgs keyArgs)
        {
            isMatch = HotkeyMatcher.IsMatch(keyArgs, keySetting, modifiersSetting);
        }
        else if (e is MouseButtonEventArgs mouseArgs)
        {
            isMatch = IsMouseHotkeyMatch(mouseArgs.ChangedButton, keySetting, modifiersSetting);
        }

        if (isMatch && (condition == null || condition()))
        {
            action();
            e.Handled = true;
            return true;
        }

        return false;
    }

    private bool IsMouseHotkeyMatch(MouseButton button, string keySetting, string modifiersSetting)
    {
        if (!string.Equals(button.ToString(), keySetting, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (Enum.TryParse<ModifierKeys>(modifiersSetting, out var requiredModifiers))
        {
            return Keyboard.Modifiers == requiredModifiers;
        }

        return false;
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
                ConsoleOutput.Items.Add(new LogEntry { Text = "No matches found.", Color = Brushes.Gray.ToString() });
                return;
            }

            foreach (var result in results)
            {
                string logMessage = $"[{result.FilePath}] ({result.LineNumber}): {result.LineText}";
                ConsoleOutput.Items.Add(new LogEntry { Text = logMessage, Color = Brushes.LightBlue.ToString() });
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

        _autoSaveTimer.Tick += AutoSaveTimer_Tick;
        _autoSaveTimer.Start();
    }

    private void AutoSaveTimer_Tick(object? sender, EventArgs e)
    {
        var settings = SettingsService.Instance.Current;
        if (!settings.AutoSaveEnabled) return;
        if (!_isUnsaved) return;
        if (string.IsNullOrEmpty(_currentFilePath)) return;
        if (_lastEditTime == DateTime.MinValue) return;

        if ((DateTime.UtcNow - _lastEditTime).TotalMinutes < settings.AutoSaveIntervalMinutes) return;

        SaveCurrentFile();
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
        UpdateSearchMarkers(SearchTextBox.Text);
    }

    private void HideSearchPanel()
    {
        SearchPanel.Visibility = Visibility.Collapsed;
        SearchMarkersCanvas.Children.Clear();
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
        
            UpdateSearchMarkers(textToFind);
        }
        catch (Exception ex)
        {
            LoggerService.Instance.LogError($"Search error: {ex.Message}");
        }
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isGlobalMode) return;
        UpdateSearchMarkers(SearchTextBox.Text);
        FindMatch(false);
    }

    private void SearchOption_Changed(object sender, RoutedEventArgs e)
    {
        if (_isGlobalMode) return;
        UpdateSearchMarkers(SearchTextBox.Text);
        FindMatch(false);
    }
    private void UpdateSearchMarkers(string textToFind)
{
    SearchMarkersCanvas.Children.Clear();

    if (string.IsNullOrEmpty(textToFind) || CodeEditor.LineCount == 0) return;

    string text = CodeEditor.Text;
    bool matchCase = MatchCaseToggle.IsChecked == true;
    bool wholeWord = WholeWordToggle.IsChecked == true;
    bool useRegex = RegexToggle.IsChecked == true;
    var lineNumbers = new HashSet<int>();

    try
    {
        if (useRegex)
        {
            var options = matchCase ? System.Text.RegularExpressions.RegexOptions.None : System.Text.RegularExpressions.RegexOptions.IgnoreCase;
            var matches = System.Text.RegularExpressions.Regex.Matches(text, textToFind, options);
            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                var line = CodeEditor.Document.GetLineByOffset(m.Index);
                lineNumbers.Add(line.LineNumber);
            }
        }
        else
        {
            StringComparison comp = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            int index = 0;
            while ((index = text.IndexOf(textToFind, index, comp)) != -1)
            {
                var line = CodeEditor.Document.GetLineByOffset(index);
                if (wholeWord)
                {
                    bool startOk = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
                    bool endOk = (index + textToFind.Length) >= text.Length || !char.IsLetterOrDigit(text[index + textToFind.Length]);
                    if (startOk && endOk)
                    {
                        lineNumbers.Add(line.LineNumber);
                    }
                }
                else
                {
                    lineNumbers.Add(line.LineNumber);
                }
                index += textToFind.Length;
                if (textToFind.Length == 0) break;
            }
        }

        double canvasHeight = SearchMarkersCanvas.ActualHeight;
        int totalLines = CodeEditor.LineCount;

        if (canvasHeight <= 0 || totalLines <= 0) return;

        foreach (int lineNum in lineNumbers)
        {
            double y = ((double)(lineNum - 1) / totalLines) * canvasHeight;
            var rect = new System.Windows.Shapes.Rectangle
            {
                Width = 10,
                Height = 4,
                Fill = new SolidColorBrush(Color.FromRgb(0x4D, 0x7C, 0xFE)),
                Opacity = 0.8
            };
            Canvas.SetTop(rect, y);
            Canvas.SetLeft(rect, 2);
            SearchMarkersCanvas.Children.Add(rect);
        }
    }
    catch
    {
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
                UpdateFontSizeLabel();
                SettingsService.Instance.Current.EditorFontSize = newSize;
                if (!_isWheelSaving)
                {
                    _isWheelSaving = true;
                    try
                    {
                        await SettingsService.Instance.SaveAsync();
                    }
                    catch
                    {
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

        _syntaxColorizer = new AngelScriptSyntaxColorizer(CodeEditor.Document);
        CodeEditor.TextArea.TextView.LineTransformers.Add(_syntaxColorizer);

        _validationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _validationTimer.Tick += (s, e) =>
        {
            _validationTimer.Stop();
            ValidateSyntax();
        };

        CodeEditor.TextArea.IndentationStrategy = new AngelScriptIndentationStrategy();
        CodeEditor.TextArea.Caret.PositionChanged += Caret_PositionChanged;

        InitErrorHover();
        UpdateCaretPosition();
        UpdateErrorCount();
        UpdateFileStats();
    }

    private void Caret_PositionChanged(object? sender, EventArgs e)
    {
        UpdateCaretPosition();
    }

    private void UpdateCaretPosition()
    {
        var caret = CodeEditor.TextArea.Caret;
        CaretPositionText.Text = $"Ln {caret.Line}, Col {caret.Column}";
    }

    private void UpdateFileStats()
    {
        string text = CodeEditor.Document.Text;
        bool hasCrlf = text.Contains("\r\n");
        bool hasLf = text.Contains('\n');
        string eol = hasCrlf ? "CRLF" : hasLf ? "LF" : "LF";
        string size = text.Length < 1024 ? $"{text.Length} B" : $"{text.Length / 1024.0:0.0} KB";
        FileInfoText.Text = $"{eol}  {CodeEditor.Document.LineCount} lines  {size}";
        UpdateFontSizeLabel();
    }

    private void UpdateFontSizeLabel()
    {
        FontSizeText.Text = $"{CodeEditor.FontSize:0.#} pt";
    }

    private void UpdateErrorCount()
    {
        int errors = _errorColorizer.Errors.Count(e => e.Severity == DiagnosticSeverity.Error);
        int warnings = _errorColorizer.Errors.Count(e => e.Severity == DiagnosticSeverity.Warning);

        ErrorCountText.Foreground = errors == 0 ? NoErrorsBrush : HasErrorsBrush;
        ErrorCountText.Text = errors == 0 ? "No errors" : $"{errors} error{(errors == 1 ? string.Empty : "s")}";

        WarningsCountText.Visibility = warnings == 0 ? Visibility.Collapsed : Visibility.Visible;
        if (warnings > 0)
        {
            WarningsCountText.Text = $"{warnings} warning{(warnings == 1 ? string.Empty : "s")}";
        }
    }

    private static Brush CreateFrozenBrush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    private void InitErrorHover()
    {
        _errorHoverLogic = new MouseHoverLogic(CodeEditor.TextArea);
        _errorHoverLogic.MouseHover += ErrorHover_MouseHover;
        _errorHoverLogic.MouseHoverStopped += ErrorHover_MouseHoverStopped;

        _errorToolTip = new ToolTip
        {
            Placement = PlacementMode.RelativePoint,
            PlacementTarget = CodeEditor.TextArea,
            Background = new SolidColorBrush(Color.FromRgb(0x16, 0x16, 0x1A)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xF3, 0xF4, 0xF6)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x2B, 0x2B, 0x30)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 6, 10, 6),
            FontSize = 13,
            MaxWidth = 420
        };
    }

    private void ErrorHover_MouseHover(object? sender, MouseEventArgs e)
    {
        if (_isErrorListOpen) return;

        var textView = CodeEditor.TextArea.TextView;
        var position = textView.GetPositionFloor(e.GetPosition(textView) + textView.ScrollOffset);
        if (position == null)
        {
            CloseErrorToolTip();
            return;
        }

        int offset = CodeEditor.Document.GetOffset(position.Value.Location);
        var hovered = _errorColorizer.Errors
            .Where(err => offset >= err.Offset && offset < err.Offset + err.Length)
            .ToList();

        if (hovered.Count == 0)
        {
            ShowTypeHintOrClose(offset, e);
            return;
        }

        if (hovered.Count == 1)
        {
            _errorToolTip.Content = FormatError(hovered[0]);
            var point = e.GetPosition(CodeEditor.TextArea);
            _errorToolTip.HorizontalOffset = point.X + 14;
            _errorToolTip.VerticalOffset = point.Y + 20;
            _errorToolTip.IsOpen = true;
        }
        else
        {
            CloseErrorToolTip();
            ShowErrorListWindow(hovered);
        }
    }

    private void ShowTypeHintOrClose(int offset, MouseEventArgs e)
    {
        var typeHints = _validation?.TypeHints;
        if (typeHints == null || typeHints.Count == 0)
        {
            CloseErrorToolTip();
            return;
        }

        int start = offset;
        string text = CodeEditor.Document.Text;
        while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_'))
        {
            start--;
        }

        if (typeHints.TryGetValue(start, out string? type))
        {
            _errorToolTip.Content = $"Type: {type}";
            var point = e.GetPosition(CodeEditor.TextArea);
            _errorToolTip.HorizontalOffset = point.X + 14;
            _errorToolTip.VerticalOffset = point.Y + 20;
            _errorToolTip.IsOpen = true;
        }
        else
        {
            CloseErrorToolTip();
        }
    }

    private void ErrorHover_MouseHoverStopped(object? sender, MouseEventArgs e)
    {
        CloseErrorToolTip();
    }

    private void CloseErrorToolTip()
    {
        if (_errorToolTip != null) _errorToolTip.IsOpen = false;
    }

    private void ShowErrorListWindow(List<SyntaxError> errors, string title = "Errors")
    {
        _isErrorListOpen = true;
        try
        {
            bool isWarning = errors.All(err => err.Severity == DiagnosticSeverity.Warning);

            var owner = Window.GetWindow(this);
            var window = new ErrorsListWindow(title,
                errors.Select(err => new ErrorEntry(FormatError(err), err.Offset)).ToList(),
                JumpToOffset,
                isWarning);
            if (owner != null) window.Owner = owner;
            window.ShowDialog();
        }
        finally
        {
            _isErrorListOpen = false;
        }
    }

    private void ErrorCountText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var errors = _errorColorizer.Errors
            .Where(err => err.Severity == DiagnosticSeverity.Error)
            .ToList();

        if (errors.Count == 0) return;
        ShowErrorListWindow(errors, "Errors");
    }

    private void WarningsCountText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var warnings = _errorColorizer.Errors
            .Where(err => err.Severity == DiagnosticSeverity.Warning)
            .ToList();

        if (warnings.Count == 0) return;
        ShowErrorListWindow(warnings, "Warnings");
    }

    private void JumpToOffset(int offset)
    {
        try
        {
            int target = Math.Min(offset, CodeEditor.Document.TextLength);
            CodeEditor.TextArea.Caret.Offset = target;
            CodeEditor.ScrollToLine(CodeEditor.Document.GetLineByOffset(target).LineNumber);
            CodeEditor.TextArea.Focus();
        }
        catch
        {
        }
    }

    private string FormatError(SyntaxError error)
    {
        int line = 1;
        try
        {
            line = CodeEditor.Document.GetLineByOffset(error.Offset).LineNumber;
        }
        catch
        {
        }

        return $"Line {line}: {error.Message}";
    }

    private void ValidateSyntax()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var validation = SyntaxValidationService.Instance.Analyze(CodeEditor.Document.Text, BuildSpellCheckContext());
        stopwatch.Stop();

        _validation = validation;
        _symbolsByName.Clear();
        foreach (var symbol in validation.Symbols)
        {
            if (!_symbolsByName.TryGetValue(symbol.Name, out var list))
            {
                list = new List<SymbolDeclaration>();
                _symbolsByName[symbol.Name] = list;
            }

            list.Add(symbol);
        }

        _errorColorizer.Errors.Clear();
        foreach (var err in validation.Errors)
        {
            _errorColorizer.Errors.Add(new SyntaxError
            {
                Offset = err.Offset,
                Length = err.Length,
                Message = err.Message,
                Severity = err.Severity
            });
        }

        ValidationTimeText.Text = $"{stopwatch.ElapsedMilliseconds} ms · {validation.Errors.Count} issue{(validation.Errors.Count == 1 ? string.Empty : "s")}";
        UpdateErrorCount();
        CodeEditor.TextArea.TextView.Redraw();
    }

    private SpellCheckContext? BuildSpellCheckContext()
    {
        var settings = SettingsService.Instance.Current;
        if (!settings.SpellCheckEnabled) return null;

        var dict = SpellCheckDictionaryService.Instance;
        if (!dict.IsReady) dict.Load();

        var index = ProjectSymbolIndexService.Instance;

        return new SpellCheckContext
        {
            CheckText = dict.Enabled,
            CheckCode = index.HasIndex,
            IsWordKnown = dict.IsKnown,
            WordSuggestion = dict.FindSuggestion,
            IsCodeKnown = name => index.IsKnown(name),
            CodeSuggestion = index.FindSuggestion
        };
    }

    private void CodeEditor_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        _contextMenuWord = null;
        AddWordMenuItem.Visibility = Visibility.Collapsed;

        var textPos = CodeEditor.GetPositionFromPoint(Mouse.GetPosition(CodeEditor));
        if (textPos == null) return;

        int offset = CodeEditor.Document.GetOffset(textPos.Value.Location);
        if (offset < 0 || offset > CodeEditor.Document.TextLength) return;

        string text = CodeEditor.Document.Text;
        Match? wordMatch = null;
        foreach (Match m in ContextWordRegex.Matches(text))
        {
            if (m.Index > offset) break;
            if (m.Index <= offset && offset < m.Index + m.Length)
            {
                wordMatch = m;
                break;
            }
        }

        if (wordMatch == null) return;

        string word = wordMatch.Value;
        if (word.Length < 3) return;

        var dict = SpellCheckDictionaryService.Instance;
        if (!dict.IsReady) dict.Load();

        if (dict.IsKnown(word)) return;
        if (_symbolsByName.ContainsKey(word)) return;
        if (ProjectSymbolIndexService.Instance.IsKnown(word)) return;

        _contextMenuWord = word;
        AddWordMenuItem.Visibility = Visibility.Visible;
        AddWordMenuItem.Header = $"Add \"{word}\" to dictionary";
    }

    private void AddWordMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_contextMenuWord == null) return;

        bool added = SpellCheckDictionaryService.Instance.AddCustomWord(_contextMenuWord);
        _contextMenuWord = null;

        if (!added) return;

        _validationTimer.Stop();
        _validationTimer.Start();
    }

    private void RefreshIndexFile(string filePath)
    {
        var index = ProjectSymbolIndexService.Instance;
        if (!index.HasIndex) return;

        index.RemoveFile(filePath);
        index.AddFile(filePath);
    }

    private void ScheduleIndexRebuild()
    {
        _indexRebuildTimer.Stop();
        _indexRebuildTimer.Start();
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

            string? json = await TryFetchStringAsync(settings.CompletionGitHubUrl);
            if (string.IsNullOrWhiteSpace(json)) return xshdCode;

            var completions =
                System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json);
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
                LoggerService.Instance.Log(entry.Text, entry.Type, entry.Color);
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

        ConsoleScrollViewer.Dispatcher.InvokeAsync(() => { ConsoleScrollViewer.ScrollToEnd(); },
            DispatcherPriority.Background);
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
    
    public async Task OpenProject(string path)
    {
        SaveCurrentTreeState();

        ProjectService.Instance.OpenProject(path);
        LoadProjectTree();
        _ = Task.Run(() => ProjectSymbolIndexService.Instance.Rebuild(path));
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

        RefreshIndexFile(fullPath);
        if (!_isUnsaved)
        {
            IsSuppressingTextEvents = true;
            _historyManager.IsSuspended = true;
            try
            {
                CodeEditor.Document.Text = File.ReadAllText(_currentFilePath);
            }
            catch
            {
            }
            finally
            {
                _historyManager.IsSuspended = false;
            }

            IsSuppressingTextEvents = false;
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
                IsSuppressingTextEvents = true;
                _historyManager.IsSuspended = true;
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
                finally
                {
                    _historyManager.IsSuspended = false;
                }

                IsSuppressingTextEvents = false;
            }
        }
    }

    private void OnFileDeleted(string fullPath)
    {
        _historyManager.DeleteFile(fullPath);
        ProjectSymbolIndexService.Instance.RemoveFile(fullPath);

        var deletedTab = _openTabs.FirstOrDefault(t => t.FilePath.Equals(fullPath, StringComparison.OrdinalIgnoreCase));
        if (deletedTab != null)
        {
            _openTabs.Remove(deletedTab);
            UpdateTabStrip();
        }

        if (!string.IsNullOrEmpty(_currentFilePath) &&
            _currentFilePath.Equals(fullPath, StringComparison.OrdinalIgnoreCase))
        {
            _autocompleteManager?.ClearWindow();

            if (_openTabs.Count > 0)
            {
                OpenFile(_openTabs[^1].FilePath);
                return;
            }

            TempFileService.Instance.ClearTemp(_currentFilePath);
            if (_autocompleteManager != null) _autocompleteManager.CurrentFilePath = null;
            _currentFilePath = null;
            _isUnsaved = false;

            _historyManager.ResetCurrent();

            _foldingManager?.Clear();
            _historyManager.IsSuspended = true;
            try
            {
                CodeEditor.Document.Text = string.Empty;
            }
            finally
            {
                _historyManager.IsSuspended = false;
            }

            SetUnsavedStatus(false);
            UpdateTabStrip();
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
        }

        RenameTab(oldFullPath, fullPath);
        _historyManager.RenameFile(oldFullPath, fullPath);
        ProjectSymbolIndexService.Instance.RenameFile(oldFullPath, fullPath);
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
            if (item is LogEntry entry)
            {
                sb.AppendLine(entry.Text);
            }
            else if (item != null)
            {
                sb.AppendLine(item.ToString());
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

            LoggerService.Instance.Log($"> {command}", LogType.Info, "#808080");
            if (AutoscrollToggle.IsChecked == true) ScrollConsoleToBottom();

            string workDir = ProjectService.Instance.CurrentFolderPath ?? AppDomain.CurrentDomain.BaseDirectory;
            await _terminalService.ExecuteAsync(command, workDir);

            if (AutoscrollToggle.IsChecked == true) ScrollConsoleToBottom();
        }
    }


    private void CodeEditor_TextChanged(object sender, EventArgs e)
    {
        if (IsSuppressingTextEvents || string.IsNullOrEmpty(_currentFilePath)) return;

        _lastEditTime = DateTime.UtcNow;
        _isUnsaved = true;
        SetUnsavedStatus(true);
        TempFileService.Instance.SaveTemp(_currentFilePath, CodeEditor.Text);

        _foldingTimer.Stop();
        _foldingTimer.Start();

        _validationTimer.Stop();
        _validationTimer.Start();

        UpdateFileStats();
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

    public void OpenFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

        _autocompleteManager?.ClearWindow();

        var tab = _openTabs.FirstOrDefault(t => t.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
        if (tab == null)
        {
            tab = new EditorTab(filePath);
            _openTabs.Add(tab);
        }

        UpdateTabStrip();

        if (!_isNavigatingHistory && !string.IsNullOrEmpty(_currentFilePath) && _currentFilePath != filePath)
        {
            _backHistory.Push(_currentFilePath);
            _forwardHistory.Clear();
        }

        _historyManager.SwitchFile(_currentFilePath, filePath);

        IsSuppressingTextEvents = true;
        _currentFilePath = filePath;
        _autocompleteManager.CurrentFilePath = _currentFilePath;
        _foldingManager?.Clear();

        _historyManager.IsSuspended = true;
        try
        {
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
        }
        finally
        {
            _historyManager.IsSuspended = false;
        }

        CodeEditor.Document.UndoStack.SizeLimit = 0;

        _foldingStrategy?.UpdateFoldings(_foldingManager, CodeEditor.Document);
        IsSuppressingTextEvents = false;
        DiscordRpcService.UpdateToEditing(Path.GetFileName(_currentFilePath));

        TabsListBox.SelectedItem = tab;
        UpdateTabStrip();

        UpdateCaretPosition();
        UpdateFileStats();
        ValidateSyntax();

        FileTreeStateService.Instance.SaveLastOpenedFile(filePath);
    }

    private void TabsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is EditorTab tab &&
            !string.Equals(_currentFilePath, tab.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            OpenFile(tab.FilePath);
        }
    }

    private void TabClose_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is EditorTab tab)
        {
            e.Handled = true;
            CloseTab(tab);
        }
    }

    private void CloseTab(EditorTab tab)
    {
        bool wasCurrent = !string.IsNullOrEmpty(_currentFilePath) &&
                          string.Equals(_currentFilePath, tab.FilePath, StringComparison.OrdinalIgnoreCase);

        int index = _openTabs.IndexOf(tab);
        _openTabs.Remove(tab);
        UpdateTabStrip();

        if (!wasCurrent) return;

        if (_openTabs.Count > 0)
        {
            OpenFile(_openTabs[Math.Min(index, _openTabs.Count - 1)].FilePath);
            return;
        }

        _historyManager.ResetCurrent();
        IsSuppressingTextEvents = true;
        _historyManager.IsSuspended = true;
        try
        {
            CodeEditor.Document.Text = string.Empty;
        }
        finally
        {
            _historyManager.IsSuspended = false;
        }
        IsSuppressingTextEvents = false;

        _currentFilePath = null;
        _isUnsaved = false;
        SetUnsavedStatus(false);
        UpdateFileStats();
        ValidateSyntax();
        UpdateTabStrip();
    }

    private void UpdateTabStrip()
    {
        bool hasTabs = _openTabs.Count > 0;
        TabsListBox.Visibility = hasTabs ? Visibility.Visible : Visibility.Collapsed;
        EmptyFilePlaceholder.Visibility = hasTabs ? Visibility.Collapsed : Visibility.Visible;
        if (!hasTabs && string.IsNullOrEmpty(CurrentFileNameText.Text))
        {
            CurrentFileNameText.Text = "No file open";
        }
    }

    private void RenameTab(string oldPath, string newPath)
    {
        var tab = _openTabs.FirstOrDefault(t => t.FilePath.Equals(oldPath, StringComparison.OrdinalIgnoreCase));
        if (tab != null) tab.FilePath = newPath;
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
            RefreshIndexFile(_currentFilePath);
            ValidateSyntax();
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

        if (!string.IsNullOrEmpty(_currentFilePath))
        {
            var tab = _openTabs.FirstOrDefault(t => t.FilePath.Equals(_currentFilePath, StringComparison.OrdinalIgnoreCase));
            if (tab != null) tab.IsUnsaved = unsaved;
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

    private void FileTree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        _startPoint = e.GetPosition(null);

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
        if (sender is MenuItem menuItem && menuItem.Parent is ContextMenu contextMenu &&
            contextMenu.PlacementTarget is FrameworkElement element)
        {
            return element is TreeViewItem tvi ? tvi.DataContext as FileNode : element.DataContext as FileNode;
        }

        return FileTree.SelectedItem as FileNode;
    }

    private void ConsoleOutput_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var element = e.OriginalSource as FrameworkElement;
        if (element?.DataContext is not LogEntry entry) return;

        string logText = entry.Text;

        var target = LogParser.ParseLogLine(logText);
        if (target != null)
        {
            if (File.Exists(target.FilePath) && target.FilePath != _currentFilePath)
            {
                OpenFile(target.FilePath);
            }

            Dispatcher.InvokeAsync(() =>
            {
                if (target.Line > 0 && target.Line <= CodeEditor.Document.LineCount)
                {
                    var lineSegment = CodeEditor.Document.GetLineByNumber(target.Line);
                    CodeEditor.CaretOffset = lineSegment.Offset + Math.Min(target.Column - 1, lineSegment.Length);
                    CodeEditor.ScrollTo(target.Line, target.Column);
                    CodeEditor.Focus();
                }
            }, DispatcherPriority.ContextIdle);
        }
    }

    private void MakeArchive_Click(object sender, RoutedEventArgs e)
    {
        if (FileTree.SelectedItem is FileNode node) ProjectService.Instance.ArchiveNode(node);
    }
}