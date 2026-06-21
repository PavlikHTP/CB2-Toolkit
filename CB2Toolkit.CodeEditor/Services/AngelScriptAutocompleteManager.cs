using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using CB2Toolkit.CodeEditor.Models.Enums;
using CB2Toolkit.CodeEditor.Syntax;
using CB2Toolkit.CodeEditor.Themes;
using CB2Toolkit.Core.Services;
using CB2Toolkit.Core.Utilities;
using CB2Toolkit.Core.Models.Settings.Enums;

namespace CB2Toolkit.CodeEditor.Services;

public class AngelScriptAutocompleteManager : IDisposable
{
    private static readonly Regex WordTagRegex = new(@"<Word>(.*?)</Word>", RegexOptions.Compiled);
    private static readonly Regex IncludeLineRegex = new(@"^\s*#include\s*""$", RegexOptions.Compiled);
    private static readonly Regex FunctionRegex = new(@"\b[A-Za-z_]\w*(?=\s*\()", RegexOptions.Compiled);
    private static readonly Regex ClassDeclarationRegex = new(@"(?<=\b(class|interface|enum)\s+)[A-Za-z_]\w*", RegexOptions.Compiled);
    private static readonly Regex WordBoundaryRegex = new(@"\b[A-Za-z_]\w*\b", RegexOptions.Compiled);
    private static readonly Regex ClassFieldRegex = new(@"\b([A-Za-z_]\w*)\s+([A-Za-z_]\w*)\s*;", RegexOptions.Compiled);
    private static readonly Regex CleanTextRegex = new(@"(//[^\r\n]*)|(/\*[\s\S]*?\*/)|(""(?:\\.|[^""\\])*"")|('(?:\\.|[^'\\])*')", RegexOptions.Compiled);

    private static readonly SolidColorBrush DarkBackground = new((Color)ColorConverter.ConvertFromString("#1E1E1E"));
    private static readonly SolidColorBrush DarkBorder = new((Color)ColorConverter.ConvertFromString("#3E3E42"));
    private static readonly SolidColorBrush LightForeground = new((Color)ColorConverter.ConvertFromString("#D4D4D4"));

    private readonly TextEditor _editor;
    private CompletionWindow? _completionWindow;
    private readonly List<AngelScriptCompletionData> _baseKeywords = new();
    private Dictionary<string, Dictionary<string, string>>? _remoteCompletions;
    private List<AngelScriptCompletionData>? _currentContextMethods;
    private static Style? _cachedListBoxStyle;
    private static Style? _cachedItemContainerStyle;
    private System.Windows.Controls.Primitives.Popup? _descriptionPopup;
    
    public AngelScriptAutocompleteManager(TextEditor editor)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        InitBaseKeywords();
        _editor.TextArea.TextEntered += TextArea_TextEntered;
        _editor.TextArea.TextEntering += TextArea_TextEntering;
        _ = LoadRemoteCompletionsAsync();
    }

    private void InitBaseKeywords()
    {
        _baseKeywords.Clear();
        string xshd = AngelScriptSyntax.GetFallbackXshd();
        foreach (Match match in WordTagRegex.Matches(xshd))
        {
            string word = match.Groups[1].Value;
            _baseKeywords.Add(new AngelScriptCompletionData(word, CompletionType.Keyword));
        }
    }

    private async Task LoadRemoteCompletionsAsync()
    {
        var settings = SettingsService.Instance.Current;
        bool isGitHub = settings.FetchPriority == FetchPrioritySource.GitHub;
        string primaryUrl = isGitHub ? settings.CompletionGitHubUrl : settings.CompletionPastebinUrl;
        string secondaryUrl = isGitHub ? settings.CompletionPastebinUrl : settings.CompletionGitHubUrl;

        string? json = await TryFetchStringAsync(primaryUrl) ?? await TryFetchStringAsync(secondaryUrl);
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var raw = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json);
                if (raw != null)
                {
                    _remoteCompletions = new Dictionary<string, Dictionary<string, string>>(raw, StringComparer.OrdinalIgnoreCase);
                    LoggerService.Instance.LogInfo($"Base loaded successfully. Classes count: {_remoteCompletions.Count}");
                    return;
                }
            }
            catch (Exception ex)
            {
                LoggerService.Instance.LogError($"JSON parsing failed: {ex.Message}");
            }
        }
        else
        {
            LoggerService.Instance.LogError("Failed to download JSON from all configured sources.");
        }
    }

    private async Task<string?> TryFetchStringAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        
        for (int attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                return await client.GetStringAsync(url);
            }
            catch
            {
                if (attempt == 5) return null;
                await Task.Delay(200);
            }
        }
        return null;
    }

    private void TextArea_TextEntered(object sender, TextCompositionEventArgs e)
    {
        if (BracketService.Instance.ProcessBracketInput(_editor, e.Text))
        {
            return;
        }

        if (_completionWindow != null) return;

        int offset = _editor.CaretOffset;
        bool isInsideStringOrComment = IsInStringOrComment(offset);

        if (e.Text == "\"")
        {
            if (isInsideStringOrComment && !string.IsNullOrEmpty(ProjectService.Instance.CurrentFolderPath))
            {
                var line = _editor.Document.GetLineByOffset(offset);
                string lineText = _editor.Document.GetText(line.Offset, offset - line.Offset);

                if (IncludeLineRegex.IsMatch(lineText))
                {
                    TriggerIncludeAutocomplete(offset);
                    return;
                }
            }
            return;
        }

        if (isInsideStringOrComment) return;

        if (e.Text == ".")
        {
            ShowMethodsForContext(offset - 1, string.Empty);
            return;
        }

        if (e.Text.Length > 0 && (char.IsLetterOrDigit(e.Text[0]) || e.Text == "_"))
        {
            int start = offset;

            while (start > 0)
            {
                char ch = _editor.Document.GetCharAt(start - 1);
                if (char.IsLetterOrDigit(ch) || ch == '_')
                    start--;
                else
                    break;
            }

            string currentWord = _editor.Document.GetText(start, offset - start);
            
            if (currentWord.Length < 1) return;

            if (start > 0 && _editor.Document.GetCharAt(start - 1) == '.')
            {
                ShowMethodsForContext(start - 1, currentWord);
                return;
            }

            var suggestions = GetContextualSuggestions(currentWord, start);

            if (suggestions.Count == 0) return;

            _currentContextMethods = null;
            OpenCompletionWindow(start, offset, suggestions);
        }
    }

    private bool IsInStringOrComment(int offset)
    {
        string text = _editor.Text;
        if (offset > text.Length) offset = text.Length;

        bool inSingleComment = false;
        bool inMultiComment = false;
        bool inString = false;
        bool inChar = false;

        for (int i = 0; i < offset; i++)
        {
            if (inSingleComment)
            {
                if (text[i] == '\n' || text[i] == '\r') inSingleComment = false;
            }
            else if (inMultiComment)
            {
                if (i + 1 < offset && text[i] == '*' && text[i + 1] == '/')
                {
                    inMultiComment = false;
                    i++;
                }
            }
            else if (inString)
            {
                if (text[i] == '\\') i++;
                else if (text[i] == '"') inString = false;
            }
            else if (inChar)
            {
                if (text[i] == '\\') i++;
                else if (text[i] == '\'') inChar = false;
            }
            else
            {
                if (i + 1 < offset && text[i] == '/' && text[i + 1] == '/')
                {
                    inSingleComment = true;
                    i++;
                }
                else if (i + 1 < offset && text[i] == '/' && text[i + 1] == '*')
                {
                    inMultiComment = true;
                    i++;
                }
                else if (text[i] == '"') inString = true;
                else if (text[i] == '\'') inChar = true;
            }
        }

        return inSingleComment || inMultiComment || inString || inChar;
    }

    private void TriggerIncludeAutocomplete(int caretOffset)
    {
        var paths = new List<string>();
        try
        {
            string[] files = Directory.GetFiles(ProjectService.Instance.CurrentFolderPath, "*.*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                paths.Add(Path.GetRelativePath(ProjectService.Instance.CurrentFolderPath, file).Replace('\\', '/'));
            }
        }
        catch
        {
        }

        if (paths.Count > 0)
        {
            _currentContextMethods = null;
            OpenCompletionWindow(caretOffset, caretOffset, paths.Select(p => new AngelScriptCompletionData(p, CompletionType.Field)));
        }
    }

    private void CodeEditor_TextChanged_ForCompletion(object? sender, EventArgs e)
    {
        if (_completionWindow == null) return;

        int offset = _editor.CaretOffset;
        int start = _completionWindow.StartOffset;

        if (offset < start)
        {
            _completionWindow.Close();
            return;
        }

        string currentWord = _editor.Document.GetText(start, offset - start);
        
        List<AngelScriptCompletionData> filtered;
        if (_currentContextMethods != null)
        {
            filtered = _currentContextMethods
                .Where(s => s.Text.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        else
        {
            filtered = GetContextualSuggestions(currentWord, start);
        }

        if (filtered.Count == 0)
        {
            _completionWindow.Close();
            return;
        }

        var data = _completionWindow.CompletionList.CompletionData;
        data.Clear();
        foreach (var item in filtered)
        {
            data.Add(item);
        }

        _completionWindow.CompletionList.SelectItem(currentWord);
    }

    private List<AngelScriptCompletionData> GetContextualSuggestions(string currentWord, int wordStartOffset)
    {
        var suggestions = new List<AngelScriptCompletionData>(_baseKeywords);
        var addedTexts = new HashSet<string>(suggestions.Select(s => s.Text), StringComparer.Ordinal);
        
        if (_remoteCompletions != null)
        {
            foreach (var className in _remoteCompletions.Keys)
            {
                if (className != "Global" && !addedTexts.Contains(className))
                {
                    suggestions.Add(new AngelScriptCompletionData(className, CompletionType.Class));
                    addedTexts.Add(className);
                }
            }

            if (_remoteCompletions.TryGetValue("Global", out var globals))
            {
                foreach (var kvp in globals)
                {
                    if (!addedTexts.Contains(kvp.Key))
                    {
                        suggestions.Add(new AngelScriptCompletionData(kvp.Key, CompletionType.Function) { Description = kvp.Value });
                        addedTexts.Add(kvp.Key);
                    }
                }
            }
        }

        string docText = _editor.Document.GetText(0, wordStartOffset);
        string cleanText = CleanTextRegex.Replace(docText, " ");

        foreach (Match match in FunctionRegex.Matches(cleanText))
        {
            string func = match.Value;
            if (!addedTexts.Contains(func))
            {
                suggestions.Add(new AngelScriptCompletionData(func, CompletionType.Function));
                addedTexts.Add(func);
            }
        }

        foreach (Match match in ClassDeclarationRegex.Matches(cleanText))
        {
            string cls = match.Value;
            if (!addedTexts.Contains(cls))
            {
                suggestions.Add(new AngelScriptCompletionData(cls, CompletionType.Class));
                addedTexts.Add(cls);
            }
        }

        foreach (Match match in WordBoundaryRegex.Matches(cleanText))
        {
            string word = match.Value;
            if (!addedTexts.Contains(word) && word.Length > 2)
            {
                suggestions.Add(new AngelScriptCompletionData(word, CompletionType.Field));
                addedTexts.Add(word);
            }
        }
        
        return suggestions
            .Where(s => s.Text.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Priority)
            .ThenBy(s => s.Text)
            .ToList();
    }

    private void ShowMethodsForContext(int dotOffset, string filterWord)
    {
        if (dotOffset < 1) return;

        int start = dotOffset;
        while (start > 0 && (char.IsLetterOrDigit(_editor.Document.GetCharAt(start - 1)) || _editor.Document.GetCharAt(start - 1) == '_'))
        {
            start--;
        }

        if (start == dotOffset) return; 

        string varName = _editor.Document.GetText(start, dotOffset - start).Trim();
        var methods = new List<AngelScriptCompletionData>();
        string lookupType = varName;

        if (_remoteCompletions != null && _remoteCompletions.ContainsKey(varName))
        {
            lookupType = varName;
        }
        else
        {
            string textBeforeCaret = _editor.Document.GetText(0, start);
            string? typeName = ResolveVariableType(varName, textBeforeCaret);

            if (!string.IsNullOrEmpty(typeName))
            {
                methods.AddRange(GetClassMembers(typeName));
                lookupType = typeName;
            }
        }

        if (_remoteCompletions != null && _remoteCompletions.TryGetValue(lookupType, out var remoteMethods))
        {
            foreach (var kvp in remoteMethods)
            {
                if (!methods.Any(m => m.Text == kvp.Key))
                {
                    methods.Add(new AngelScriptCompletionData(kvp.Key, CompletionType.Function) { Description = kvp.Value });
                }
            }
        }
        else if (_remoteCompletions == null)
        {
            LoggerService.Instance.LogError("[Autocomplete Fail] _remoteCompletions is NULL. JSON database was not loaded.");
        }

        if (methods.Count > 0)
        {
            _currentContextMethods = methods;

            var finalItems = string.IsNullOrEmpty(filterWord)
                ? methods
                : methods.Where(m => m.Text.StartsWith(filterWord, StringComparison.OrdinalIgnoreCase)).ToList();

            if (finalItems.Count > 0)
            {
                OpenCompletionWindow(dotOffset + 1, _editor.CaretOffset, finalItems);
            }
        }
    }

    private string? ResolveVariableType(string varName, string textBeforeCaret)
    {
        string pattern = @"\b([A-Za-z_]\w*)\s*@?\s*" + Regex.Escape(varName) + @"\b";
        var matches = Regex.Matches(textBeforeCaret, pattern);
        
        if (matches.Count > 0)
        {
            string typeName = matches[^1].Groups[1].Value;
            string[] primitives = { "if", "for", "while", "return", "new", "void", "int", "float", "double", "bool", "uint", "string" };
            
            if (primitives.Contains(typeName)) return null;
            return typeName;
        }
        return null;
    }

    private List<AngelScriptCompletionData> GetClassMembers(string typeName)
    {
        var members = new List<AngelScriptCompletionData>();
        string fullText = _editor.Text;
        
        string classPattern = @"\bclass\s+" + Regex.Escape(typeName) + @"\s*\{";
        var match = Regex.Match(fullText, classPattern);
        if (!match.Success) return members;

        int startPos = match.Index + match.Length;
        int braceCount = 1;
        int endPos = startPos;

        while (endPos < fullText.Length && braceCount > 0)
        {
            if (fullText[endPos] == '{') braceCount++;
            else if (fullText[endPos] == '}') braceCount--;
            endPos++;
        }

        if (braceCount > 0) return members;

        string classBody = fullText.Substring(startPos, endPos - startPos - 1);
        string cleanClassBody = CleanTextRegex.Replace(classBody, " ");

        foreach (Match m in FunctionRegex.Matches(cleanClassBody))
        {
            string methodName = m.Value;
            if (methodName == "if" || methodName == "while" || methodName == "for" || methodName == "switch") continue;
            
            if (!members.Any(x => x.Text.StartsWith(methodName)))
            {
                members.Add(new AngelScriptCompletionData(methodName + "()", CompletionType.Function));
            }
        }

        var fieldMatches = ClassFieldRegex.Matches(cleanClassBody);
        foreach (Match m in fieldMatches)
        {
            string fieldName = m.Groups[2].Value;
            if (!members.Any(x => x.Text == fieldName))
            {
                members.Add(new AngelScriptCompletionData(fieldName, CompletionType.Field));
            }
        }

        return members;
    }

   private void OpenCompletionWindow(int startOffset, int endOffset, IEnumerable<AngelScriptCompletionData> items)
{
    var itemList = items.ToList();
    int maxLen = itemList.Select(i => i.Text?.Length ?? 0).DefaultIfEmpty(0).Max();
    double estimatedWidth = (maxLen * 8.2) + 55;
    double calculatedWidth = Math.Min(Math.Max(estimatedWidth, 200), 550);

    _completionWindow = new CompletionWindow(_editor.TextArea)
    {
        StartOffset = startOffset,
        EndOffset = endOffset,
        Background = DarkBackground,
        Foreground = LightForeground,
        BorderBrush = DarkBorder,
        Padding = new Thickness(0),
        BorderThickness = new Thickness(1),
        Width = calculatedWidth,
        MaxHeight = 300,
        AllowsTransparency = true
    };

    _completionWindow.CompletionList.Background = DarkBackground;

    _descriptionPopup = new System.Windows.Controls.Primitives.Popup
    {
        AllowsTransparency = true,
        PlacementTarget = _completionWindow,
        Placement = System.Windows.Controls.Primitives.PlacementMode.Right,
        HorizontalOffset = 4,
        VerticalOffset = 0,
        StaysOpen = true
    };

    var popupBorder = new Border
    {
        Background = DarkBackground,
        BorderBrush = DarkBorder,
        BorderThickness = new Thickness(1),
        Padding = new Thickness(10, 8, 10, 8),
        CornerRadius = new CornerRadius(3),
        MinWidth = 150,
        MaxWidth = 600
    };

    var popupText = new TextBlock
    {
        Foreground = LightForeground,
        FontFamily = new FontFamily("Consolas"),
        FontSize = 12,
        TextWrapping = TextWrapping.Wrap
    };

    popupBorder.Child = popupText;
    _descriptionPopup.Child = popupBorder;

    var listBox = _completionWindow.CompletionList.ListBox;
    
    _cachedListBoxStyle ??= (Style)System.Windows.Markup.XamlReader.Parse(AutoCompleteStyles.GetListBoxStyleXml());
    _cachedItemContainerStyle ??= (Style)System.Windows.Markup.XamlReader.Parse(AutoCompleteStyles.GetItemStyleXml());

    listBox.Style = _cachedListBoxStyle;
    listBox.ItemContainerStyle = _cachedItemContainerStyle;

    listBox.SelectionChanged += ListBox_SelectionChanged;

    var data = _completionWindow.CompletionList.CompletionData;
    foreach (var item in itemList)
    {
        data.Add(item);
    }

    _completionWindow.Show();

    string currentWord = _editor.Document.GetText(startOffset, endOffset - startOffset);
    if (!string.IsNullOrEmpty(currentWord) && currentWord != ".")
    {
        _completionWindow.CompletionList.SelectItem(currentWord);
    }

    _editor.TextChanged += CodeEditor_TextChanged_ForCompletion;
    _completionWindow.Closed += CompletionWindow_Closed;
}

    private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_completionWindow == null || _descriptionPopup == null) return;

        foreach (Window w in _completionWindow.OwnedWindows)
        {
            w.Visibility = Visibility.Collapsed;
            w.Width = 0;
            w.Height = 0;
        }

        if (sender is ListBox listBox && listBox.SelectedItem is AngelScriptCompletionData selectedItem)
        {
            if (selectedItem.Description != null && !string.IsNullOrWhiteSpace(selectedItem.Description.ToString()))
            {
                if (_descriptionPopup.Child is Border border && border.Child is TextBlock textBlock)
                {
                    textBlock.Text = selectedItem.Description.ToString();
                }
                _descriptionPopup.IsOpen = true;
            }
            else
            {
                _descriptionPopup.IsOpen = false;
            }
        }
        else
        {
            _descriptionPopup.IsOpen = false;
        }
    }

    private void CompletionWindow_Closed(object? sender, EventArgs e)
    {
        _editor.TextChanged -= CodeEditor_TextChanged_ForCompletion;
        _currentContextMethods = null;

        if (_descriptionPopup != null)
        {
            _descriptionPopup.IsOpen = false;
            _descriptionPopup = null;
        }

        if (_completionWindow != null)
        {
            _completionWindow.CompletionList.ListBox.SelectionChanged -= ListBox_SelectionChanged;
            _completionWindow = null;
        }
    }

    private void TextArea_TextEntering(object sender, TextCompositionEventArgs e)
    {
        if (e.Text.Length > 0 && _completionWindow != null)
        {
            if (e.Text == " ")
            {
                _completionWindow.Close();
                return;
            }

            if (!char.IsLetterOrDigit(e.Text[0]) && e.Text[0] != '_')
            {
                _completionWindow.CompletionList.RequestInsertion(e);
            }
        }
    }

    public void ClearWindow()
    {
        _completionWindow?.Close();
    }

    public void Dispose()
    {
        _editor.TextArea.TextEntered -= TextArea_TextEntered;
        _editor.TextArea.TextEntering -= TextArea_TextEntering;
        _editor.TextChanged -= CodeEditor_TextChanged_ForCompletion;
        _completionWindow?.Close();
    }
}