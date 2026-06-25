using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using CB2Toolkit.CodeEditor.Models.Enums;
using CB2Toolkit.CodeEditor.Syntax;
using CB2Toolkit.CodeEditor.Themes;
using CB2Toolkit.Core.Services;
using CB2Toolkit.Core.Utilities;

namespace CB2Toolkit.CodeEditor.Services;

public class AngelScriptAutocompleteManager : IDisposable
{
    private static readonly SolidColorBrush DarkBackground = new((Color)ColorConverter.ConvertFromString("#1E1E1E"));
    private static readonly SolidColorBrush DarkBorder = new((Color)ColorConverter.ConvertFromString("#3E3E42"));
    private static readonly SolidColorBrush LightForeground = new((Color)ColorConverter.ConvertFromString("#D4D4D4"));
    private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    private readonly TextEditor _editor;
    private CompletionWindow? _completionWindow;
    private readonly List<AngelScriptCompletionData> _baseKeywords = new();
    private Dictionary<string, Dictionary<string, string>>? _remoteCompletions;
    private List<AngelScriptCompletionData>? _currentContextMethods;
    private static Style? _cachedListBoxStyle;
    private static Style? _cachedItemContainerStyle;
    private System.Windows.Controls.Primitives.Popup? _descriptionPopup;

    private System.Windows.Controls.Primitives.Popup? _signaturePopup;
    private TextBlock? _signatureTextBlock;
    private int _signatureStartOffset = -1;
    private string? _signatureFunctionText;
    private List<string> _signatureParameters = new();
    private string _signaturePrefix = string.Empty;

    private readonly object _cacheLock = new();
    private List<AngelScriptCompletionData> _cachedLocalSuggestions = new();
    private CancellationTokenSource? _debounceCts;

    public AngelScriptAutocompleteManager(TextEditor editor)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        InitBaseKeywords();
        _editor.TextArea.TextEntered += TextArea_TextEntered;
        _editor.TextArea.TextEntering += TextArea_TextEntering;
        _editor.TextArea.Caret.PositionChanged += Caret_PositionChanged;
        _editor.Document.TextChanged += Document_TextChanged;
        _ = LoadRemoteCompletionsAsync();
        TriggerInitialParse();
    }

    private void InitBaseKeywords()
    {
        _baseKeywords.Clear();
        string xshd = AngelScriptSyntax.GetFallbackXshd();
        foreach (Match match in RegexPatterns.WordTag.Matches(xshd))
        {
            string word = match.Groups[1].Value;
            _baseKeywords.Add(new AngelScriptCompletionData(word, CompletionType.Keyword));
        }
    }

    private async Task LoadRemoteCompletionsAsync()
    {
        var settings = SettingsService.Instance.Current;
        string primaryUrl = settings.CompletionGitHubUrl;
        string? json = await TryFetchStringAsync(primaryUrl);
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var raw = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json);
                if (raw != null)
                {
                    _remoteCompletions = new Dictionary<string, Dictionary<string, string>>(raw, StringComparer.OrdinalIgnoreCase);
                    LoggerService.Instance.LogInfo($"Base loaded successfully.");
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
                return await SharedHttpClient.GetStringAsync(url);
            }
            catch
            {
                if (attempt == 5) return null;
                await Task.Delay(200);
            }
        }
        return null;
    }

    private void TriggerInitialParse()
    {
        string textSnapshot = _editor.Text;
        Task.Run(() =>
        {
            var localSuggestions = ParseDocumentText(textSnapshot);
            lock (_cacheLock)
            {
                _cachedLocalSuggestions = localSuggestions;
            }
        });
    }

    private void Document_TextChanged(object? sender, EventArgs e)
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        string textSnapshot = _editor.Text;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(400, token);
                if (token.IsCancellationRequested) return;

                var localSuggestions = ParseDocumentText(textSnapshot);

                lock (_cacheLock)
                {
                    _cachedLocalSuggestions = localSuggestions;
                }
            }
            catch (TaskCanceledException)
            {
            }
        }, token);
    }

    private List<AngelScriptCompletionData> ParseDocumentText(string docText)
    {
        var suggestions = new List<AngelScriptCompletionData>();
        var addedTexts = new HashSet<string>(StringComparer.Ordinal);

        string cleanText = RegexPatterns.CleanText.Replace(docText, " ");

        foreach (Match match in RegexPatterns.FunctionName.Matches(cleanText))
        {
            string func = match.Value;
            if (!addedTexts.Contains(func))
            {
                suggestions.Add(new AngelScriptCompletionData(func, CompletionType.Function));
                addedTexts.Add(func);
            }
        }

        foreach (Match match in RegexPatterns.ClassDeclaration.Matches(cleanText))
        {
            string cls = match.Value;
            if (!addedTexts.Contains(cls))
            {
                suggestions.Add(new AngelScriptCompletionData(cls, CompletionType.Class));
                addedTexts.Add(cls);
            }
        }

        foreach (Match match in RegexPatterns.WordBoundary.Matches(cleanText))
        {
            string word = match.Value;
            if (!addedTexts.Contains(word) && word.Length > 2)
            {
                suggestions.Add(new AngelScriptCompletionData(word, CompletionType.Field));
                addedTexts.Add(word);
            }
        }

        return suggestions;
    }

    private void TextArea_TextEntered(object sender, TextCompositionEventArgs e)
    {
        if (BracketService.Instance.ProcessBracketInput(_editor, e.Text))
        {
            return;
        }

        int offset = _editor.CaretOffset;
        bool isInsideStringOrComment = IsInStringOrComment(offset);

        if (e.Text == "\"")
        {
            if (isInsideStringOrComment && !string.IsNullOrEmpty(ProjectService.Instance.CurrentFolderPath))
            {
                var line = _editor.Document.GetLineByOffset(offset);
                string lineText = _editor.Document.GetText(line.Offset, offset - line.Offset);

                if (RegexPatterns.IncludeIncomplete.IsMatch(lineText))
                {
                    TriggerIncludeAutocomplete(offset);
                    return;
                }
            }
            return;
        }

        if (isInsideStringOrComment) return;

        if (e.Text == "(")
        {
            TriggerSignatureHelp(offset);
            return;
        }

        if (_completionWindow != null) return;

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

        var line = _editor.Document.GetLineByOffset(offset);
        int lineStart = line.Offset;
        string lineText = text.Substring(lineStart, offset - lineStart);

        bool inString = false;
        bool inChar = false;
        bool inSingleComment = false;

        for (int i = 0; i < lineText.Length; i++)
        {
            if (inSingleComment) continue;
            if (inString)
            {
                if (lineText[i] == '\\') i++;
                else if (lineText[i] == '"') inString = false;
            }
            else if (inChar)
            {
                if (lineText[i] == '\\') i++;
                else if (lineText[i] == '\'') inChar = false;
            }
            else
            {
                if (i + 1 < lineText.Length && lineText[i] == '/' && lineText[i + 1] == '/')
                {
                    inSingleComment = true;
                    i++;
                }
                else if (lineText[i] == '"') inString = true;
                else if (lineText[i] == '\'') inChar = true;
            }
        }

        if (inSingleComment || inString || inChar) return true;

        int searchStart = Math.Max(0, offset - 25000);
        string lookbackText = text.Substring(searchStart, offset - searchStart);
        int lastOpen = lookbackText.LastIndexOf("/*", StringComparison.Ordinal);
        int lastClose = lookbackText.LastIndexOf("*/", StringComparison.Ordinal);

        if (lastOpen > lastClose)
        {
            return true;
        }

        return false;
    }

    private void TriggerIncludeAutocomplete(int caretOffset)
    {
        var paths = new List<string>();
        try
        {
            if (!string.IsNullOrEmpty(ProjectService.Instance.CurrentFolderPath) && Directory.Exists(ProjectService.Instance.CurrentFolderPath))
            {
                string[] files = Directory.GetFiles(ProjectService.Instance.CurrentFolderPath, "*.*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    paths.Add(Path.GetRelativePath(ProjectService.Instance.CurrentFolderPath, file).Replace('\\', '/'));
                }
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

        lock (_cacheLock)
        {
            foreach (var item in _cachedLocalSuggestions)
            {
                if (!addedTexts.Contains(item.Text))
                {
                    suggestions.Add(item);
                    addedTexts.Add(item.Text);
                }
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
        var members = new List<AngelScriptCompletionData>();
        string lookupType = varName;

        if (_remoteCompletions != null && _remoteCompletions.ContainsKey(varName))
        {
            lookupType = varName;
        }
        else
        {
            int scanStart = Math.Max(0, start - 15000);
            string textBeforeCaret = _editor.Document.GetText(scanStart, start - scanStart);
            string textAfterCaret = _editor.Document.GetText(start, Math.Min(15000, _editor.Document.TextLength - start));
            string? typeName = ResolveVariableType(varName, textBeforeCaret, textAfterCaret);

            if (!string.IsNullOrEmpty(typeName))
            {
                members.AddRange(GetClassMembers(typeName));
                lookupType = typeName;
            }
        }

        if (_remoteCompletions != null && _remoteCompletions.TryGetValue(lookupType, out var remoteMethods))
        {
            foreach (var kvp in remoteMethods)
            {
                if (!members.Any(m => m.Text == kvp.Key))
                {
                    members.Add(new AngelScriptCompletionData(kvp.Key, CompletionType.Function) { Description = kvp.Value });
                }
            }
        }
        else if (_remoteCompletions == null)
        {
            LoggerService.Instance.LogError("[Autocomplete Fail] _remoteCompletions is NULL. JSON database was not loaded.");
        }

        if (members.Count > 0)
        {
            _currentContextMethods = members;

            var finalItems = string.IsNullOrEmpty(filterWord)
                ? members
                : members.Where(m => m.Text.StartsWith(filterWord, StringComparison.OrdinalIgnoreCase)).ToList();

            if (finalItems.Count > 0)
            {
                OpenCompletionWindow(dotOffset + 1, _editor.CaretOffset, finalItems);
            }
        }
    }

    private string? ResolveVariableType(string varName, string textBeforeCaret, string textAfterCaret)
    {
        string[] primitives = { "if", "for", "while", "return", "new", "void", "int", "float", "double", "bool", "uint", "string" };
        var matchesBefore = RegexPatterns.VariableTypeDeclaration(varName).Matches(textBeforeCaret);
        if (matchesBefore.Count > 0)
        {
            string typeName = matchesBefore[^1].Groups[1].Value;
            if (!primitives.Contains(typeName)) return typeName;
        }

        var matchesAfter = RegexPatterns.VariableTypeDeclaration(varName).Matches(textAfterCaret);
        if (matchesAfter.Count > 0)
        {
            string typeName = matchesAfter[0].Groups[1].Value;
            if (!primitives.Contains(typeName)) return typeName;
        }

        return null;
    }

    private List<AngelScriptCompletionData> GetClassMembers(string typeName)
    {
        var members = new List<AngelScriptCompletionData>();
        string fullText = _editor.Text;

        var match = RegexPatterns.ClassBodySearch(typeName).Match(fullText);
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
        string cleanClassBody = RegexPatterns.CleanText.Replace(classBody, " ");

        foreach (Match m in RegexPatterns.FunctionName.Matches(cleanClassBody))
        {
            string methodName = m.Value;
            if (methodName == "if" || methodName == "while" || methodName == "for" || methodName == "switch") continue;

            if (!members.Any(x => x.Text.StartsWith(methodName)))
            {
                members.Add(new AngelScriptCompletionData(methodName, CompletionType.Function));
            }
        }

        var fieldMatches = RegexPatterns.ClassField.Matches(cleanClassBody);
        foreach (Match m in fieldMatches)
        {
            string typeStr = m.Groups[1].Value;
            string[] primitives = { "if", "for", "while", "return", "new", "void", "switch", "case" };
            if (primitives.Contains(typeStr)) continue;

            string fieldsPart = m.Groups[2].Value;
            string[] parts = fieldsPart.Split(',');
            foreach (var part in parts)
            {
                var nameMatch = RegexPatterns.WordBoundary.Match(part);
                if (nameMatch.Success)
                {
                    string fieldName = nameMatch.Value;
                    if (!members.Any(x => x.Text == fieldName))
                    {
                        members.Add(new AngelScriptCompletionData(fieldName, CompletionType.Field));
                    }
                }
            }
        }

        return members;
    }

    private void OpenCompletionWindow(int startOffset, int endOffset, IEnumerable<AngelScriptCompletionData> items)
    {
        _editor.TextChanged -= CodeEditor_TextChanged_ForCompletion;

        if (_completionWindow != null)
        {
            _completionWindow.Close();
        }

        var itemList = items.ToList();
        int maxLen = itemList.Select(i => i.Text?.Length ?? 0).DefaultIfEmpty(0).Max();
        
        double fontFactor = _editor.FontSize * 0.62;
        double estimatedWidth = (maxLen * fontFactor) + 55;
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
            FontSize = _editor.FontSize,
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

    private void TriggerSignatureHelp(int caretOffset)
    {
        CloseSignatureHelp();

        if (_remoteCompletions == null) return;

        int start = caretOffset - 1;
        while (start > 0 && char.IsWhiteSpace(_editor.Document.GetCharAt(start - 1)))
        {
            start--;
        }

        int end = start;
        while (start > 0 && (char.IsLetterOrDigit(_editor.Document.GetCharAt(start - 1)) || _editor.Document.GetCharAt(start - 1) == '_'))
        {
            start--;
        }

        if (start == end) return;

        string funcName = _editor.Document.GetText(start, end - start).Trim();
        string? matchSignature = null;

        if (_remoteCompletions.TryGetValue("Global", out var globals) && globals.TryGetValue(funcName, out var globalSig))
        {
            matchSignature = globalSig;
        }
        else
        {
            foreach (var kvp in _remoteCompletions)
            {
                if (kvp.Value.TryGetValue(funcName, out var classSig))
                {
                    matchSignature = classSig;
                    break;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(matchSignature)) return;

        int openParen = matchSignature.IndexOf('(');
        int closeParen = matchSignature.LastIndexOf(')');
        if (openParen == -1 || closeParen == -1 || closeParen <= openParen) return;

        _signatureStartOffset = caretOffset;
        _signatureFunctionText = matchSignature;
        _signaturePrefix = matchSignature.Substring(0, openParen + 1);

        string paramContent = matchSignature.Substring(openParen + 1, closeParen - openParen - 1);
        _signatureParameters = string.IsNullOrWhiteSpace(paramContent)
            ? new List<string>()
            : paramContent.Split(',').Select(p => p.Trim()).ToList();

        InitSignaturePopup();
        UpdateSignatureHighlight();
    }

    private void InitSignaturePopup()
    {
        _signatureTextBlock = new TextBlock
        {
            Foreground = LightForeground,
            FontFamily = new FontFamily("Consolas"),
            FontSize = _editor.FontSize,
            TextWrapping = TextWrapping.NoWrap
        };

        var border = new Border
        {
            Background = DarkBackground,
            BorderBrush = DarkBorder,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 4, 8, 4),
            CornerRadius = new CornerRadius(2),
            Child = _signatureTextBlock
        };

        _signaturePopup = new System.Windows.Controls.Primitives.Popup
        {
            AllowsTransparency = true,
            PlacementTarget = _editor.TextArea,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Relative,
            StaysOpen = true,
            Child = border
        };

        var caretRect = _editor.TextArea.Caret.CalculateCaretRectangle();
        _signaturePopup.HorizontalOffset = caretRect.Left;
        _signaturePopup.VerticalOffset = caretRect.Bottom + 4;
        _signaturePopup.IsOpen = true;
    }

    private void UpdateSignatureHighlight()
    {
        if (_signaturePopup == null || _signatureTextBlock == null || _signatureStartOffset == -1) return;

        int currentOffset = _editor.CaretOffset;
        if (currentOffset < _signatureStartOffset)
        {
            CloseSignatureHelp();
            return;
        }

        string textSinceStart = _editor.Document.GetText(_signatureStartOffset, currentOffset - _signatureStartOffset);
        int paramIndex = 0;
        int parenDepth = 0;

        foreach (char c in textSinceStart)
        {
            if (c == '(') parenDepth++;
            else if (c == ')') parenDepth--;
            else if (c == ',' && parenDepth == 0) paramIndex++;
        }

        if (parenDepth < 0)
        {
            CloseSignatureHelp();
            return;
        }

        _signatureTextBlock.Inlines.Clear();
        _signatureTextBlock.Inlines.Add(new Run(_signaturePrefix));

        for (int i = 0; i < _signatureParameters.Count; i++)
        {
            if (i == paramIndex)
            {
                _signatureTextBlock.Inlines.Add(new Run(_signatureParameters[i])
                {
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White
                });
            }
            else
            {
                _signatureTextBlock.Inlines.Add(new Run(_signatureParameters[i]));
            }

            if (i < _signatureParameters.Count - 1)
            {
                _signatureTextBlock.Inlines.Add(new Run(", "));
            }
        }

        _signatureTextBlock.Inlines.Add(new Run(")"));

        var caretRect = _editor.TextArea.Caret.CalculateCaretRectangle();
        _signaturePopup.HorizontalOffset = caretRect.Left;
        _signaturePopup.VerticalOffset = caretRect.Bottom + 4;
    }

    private void Caret_PositionChanged(object? sender, EventArgs e)
    {
        if (_signaturePopup != null && _signaturePopup.IsOpen)
        {
            UpdateSignatureHighlight();
        }
    }

    private void CloseSignatureHelp()
    {
        _signatureStartOffset = -1;
        _signatureFunctionText = null;
        _signatureParameters.Clear();
        _signaturePrefix = string.Empty;

        if (_signaturePopup != null)
        {
            _signaturePopup.IsOpen = false;
            _signaturePopup = null;
        }
        _signatureTextBlock = null;
    }

    public void ClearWindow()
    {
        _completionWindow?.Close();
        CloseSignatureHelp();
    }

    public void Dispose()
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _editor.TextArea.TextEntered -= TextArea_TextEntered;
        _editor.TextArea.TextEntering -= TextArea_TextEntering;
        _editor.TextArea.Caret.PositionChanged -= Caret_PositionChanged;
        _editor.TextChanged -= CodeEditor_TextChanged_ForCompletion;
        _editor.Document.TextChanged -= Document_TextChanged;
        _completionWindow?.Close();
        CloseSignatureHelp();
    }
}