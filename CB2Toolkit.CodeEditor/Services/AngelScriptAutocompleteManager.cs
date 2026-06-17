using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CB2Toolkit.CodeEditor.Models.Enums;
using CB2Toolkit.CodeEditor.Syntax;
using CB2Toolkit.CodeEditor.Themes;
using CB2Toolkit.Core.Services;
using CB2Toolkit.Core.Utilities;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;

namespace CB2Toolkit.CodeEditor.Services;

public class AngelScriptAutocompleteManager : IDisposable
{
    private readonly TextEditor _editor;
    private CompletionWindow? _completionWindow;
    private readonly List<AngelScriptCompletionData> _baseKeywords = new();
    private static Style? _cachedListBoxStyle;
    private static Style? _cachedItemContainerStyle;

    public AngelScriptAutocompleteManager(TextEditor editor)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        InitBaseKeywords();
        _editor.TextArea.TextEntered += TextArea_TextEntered;
        _editor.TextArea.TextEntering += TextArea_TextEntering;
    }

    private void InitBaseKeywords()
    {
        _baseKeywords.Clear();
        string xshd = AngelScriptSyntax.GetFallbackXshd();
        foreach (Match match in Regex.Matches(xshd, @"<Word>(.*?)</Word>"))
        {
            string word = match.Groups[1].Value;
            _baseKeywords.Add(new AngelScriptCompletionData(word, CompletionType.Keyword));
        }
    }

    private void TextArea_TextEntered(object sender, TextCompositionEventArgs e)
    {
        if (BracketService.Instance.ProcessBracketInput(_editor, e.Text))
        {
            return;
        }

        if (_completionWindow != null) return;

        if (e.Text == ".")
        {
            ShowMethodsForContext();
            return;
        }

        if (e.Text == "\"" && !string.IsNullOrEmpty(ProjectService.Instance.CurrentFolderPath))
        {
            int caretOffset = _editor.CaretOffset;
            var line = _editor.Document.GetLineByOffset(caretOffset);
            string lineText = _editor.Document.GetText(line.Offset, caretOffset - line.Offset);

            if (Regex.IsMatch(lineText, @"^\s*#include\s*""$"))
            {
                TriggerIncludeAutocomplete(caretOffset);
                return;
            }
        }

        if (e.Text.Length > 0 && (char.IsLetterOrDigit(e.Text[0]) || e.Text == "_"))
        {
            int offset = _editor.CaretOffset;
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
            var suggestions = GetContextualSuggestions(currentWord);

            if (suggestions.Count == 0) return;

            OpenCompletionWindow(start, offset, suggestions);
        }
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
        var filtered = GetContextualSuggestions(currentWord);

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

    private List<AngelScriptCompletionData> GetContextualSuggestions(string currentWord)
    {
        var suggestions = new List<AngelScriptCompletionData>(_baseKeywords);
        var addedTexts = new HashSet<string>(suggestions.Select(s => s.Text), StringComparer.OrdinalIgnoreCase);
        string docText = _editor.Text;

        foreach (Match match in Regex.Matches(docText, @"\b[A-Za-z_]\w*(?=\s*\()"))
        {
            string func = match.Value;
            if (!addedTexts.Contains(func))
            {
                suggestions.Add(new AngelScriptCompletionData(func, CompletionType.Function));
                addedTexts.Add(func);
            }
        }

        foreach (Match match in Regex.Matches(docText, @"(?<=\b(class|interface|enum)\s+)[A-Za-z_]\w*"))
        {
            string cls = match.Value;
            if (!addedTexts.Contains(cls))
            {
                suggestions.Add(new AngelScriptCompletionData(cls, CompletionType.Class));
                addedTexts.Add(cls);
            }
        }

        foreach (Match match in Regex.Matches(docText, @"\b[A-Za-z_]\w*\b"))
        {
            string word = match.Value;
            if (!addedTexts.Contains(word) && word.Length > 2)
            {
                suggestions.Add(new AngelScriptCompletionData(word, CompletionType.Field));
                addedTexts.Add(word);
            }
        }

        return suggestions
            .Where(s => s.Text.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase) &&
                        !s.Text.Equals(currentWord, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Priority)
            .ThenBy(s => s.Text)
            .ToList();
    }

    private void ShowMethodsForContext()
    {
        int offset = _editor.CaretOffset;
        if (offset < 2) return;

        int start = offset - 2;
        while (start > 0 && (char.IsLetterOrDigit(_editor.Document.GetCharAt(start)) || _editor.Document.GetCharAt(start) == '_'))
        {
            start--;
        }

        if (start < offset - 2) start++;

        string varName = _editor.Document.GetText(start, (offset - 1) - start).Trim();
        var methods = new List<AngelScriptCompletionData>();

        if (varName.Equals("string", StringComparison.OrdinalIgnoreCase) || 
            string.Equals(varName, "text", StringComparison.OrdinalIgnoreCase) || 
            string.Equals(varName, "name", StringComparison.OrdinalIgnoreCase))
        {
            methods.Add(new AngelScriptCompletionData("length()", CompletionType.Function));
            methods.Add(new AngelScriptCompletionData("substr(uint start, int count = -1)", CompletionType.Function));
            methods.Add(new AngelScriptCompletionData("isEmpty()", CompletionType.Function));
            methods.Add(new AngelScriptCompletionData("findFirst(const string &in subStr)", CompletionType.Function));
        }
        else if (varName.Equals("array", StringComparison.OrdinalIgnoreCase) || varName.EndsWith("list", StringComparison.OrdinalIgnoreCase))
        {
            methods.Add(new AngelScriptCompletionData("length()", CompletionType.Function));
            methods.Add(new AngelScriptCompletionData("insertLast(const T &in value)", CompletionType.Function));
            methods.Add(new AngelScriptCompletionData("removeAt(uint index)", CompletionType.Function));
            methods.Add(new AngelScriptCompletionData("sortAsc()", CompletionType.Function));
            methods.Add(new AngelScriptCompletionData("reverse()", CompletionType.Function));
        }
        else if (varName.Equals("dictionary", StringComparison.OrdinalIgnoreCase) || varName.EndsWith("dict", StringComparison.OrdinalIgnoreCase))
        {
            methods.Add(new AngelScriptCompletionData("set(const string &in key, const T &in value)", CompletionType.Function));
            methods.Add(new AngelScriptCompletionData("exists(const string &in key)", CompletionType.Function));
            methods.Add(new AngelScriptCompletionData("delete(const string &in key)", CompletionType.Function));
            methods.Add(new AngelScriptCompletionData("clear()", CompletionType.Function));
        }
        else
        {
            methods.Add(new AngelScriptCompletionData("length()", CompletionType.Function));
            methods.Add(new AngelScriptCompletionData("toString()", CompletionType.Function));
        }

        if (methods.Count > 0)
        {
            OpenCompletionWindow(offset, offset, methods);
        }
    }

    private void OpenCompletionWindow(int startOffset, int endOffset, IEnumerable<AngelScriptCompletionData> items)
    {
        var darkBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E1E"));
        var darkBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3E3E42"));
        var lightForeground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D4D4D4"));

        _completionWindow = new CompletionWindow(_editor.TextArea)
        {
            StartOffset = startOffset,
            EndOffset = endOffset,
            Background = darkBackground,
            Foreground = lightForeground,
            BorderBrush = darkBorder,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(1),
            Width = 260,
            MaxHeight = 300,
            AllowsTransparency = true
        };

        _completionWindow.CompletionList.Background = darkBackground;

        var listBox = _completionWindow.CompletionList.ListBox;
        
        _cachedListBoxStyle ??= (Style)System.Windows.Markup.XamlReader.Parse(AutoCompleteStyles.GetListBoxStyleXml());
        _cachedItemContainerStyle ??= (Style)System.Windows.Markup.XamlReader.Parse(AutoCompleteStyles.GetItemStyleXml());

        listBox.Style = _cachedListBoxStyle;
        listBox.ItemContainerStyle = _cachedItemContainerStyle;

        listBox.SelectionChanged += ListBox_SelectionChanged;

        var data = _completionWindow.CompletionList.CompletionData;
        foreach (var item in items)
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
        if (_completionWindow == null) return;
        foreach (Window w in _completionWindow.OwnedWindows)
        {
            w.Visibility = Visibility.Collapsed;
            w.Width = 0;
            w.Height = 0;
        }
    }

    private void CompletionWindow_Closed(object? sender, EventArgs e)
    {
        _editor.TextChanged -= CodeEditor_TextChanged_ForCompletion;
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