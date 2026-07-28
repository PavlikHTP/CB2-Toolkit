using System.Text.RegularExpressions;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;

namespace CB2Toolkit.CodeEditor.Extensions;

public static class AvalonEditExtensions
{
    public static void DuplicateCurrentLine(this TextEditor editor)
    {
        if (editor.SelectionLength > 0)
        {
            string selectedText = editor.SelectedText;
            editor.Document.Insert(editor.SelectionStart + editor.SelectionLength, selectedText);
        }
        else
        {
            var line = editor.Document.GetLineByOffset(editor.CaretOffset);
            string lineText = editor.Document.GetText(line.Offset, line.TotalLength);
            editor.Document.Insert(line.Offset + line.TotalLength, lineText);
        }
    }

    public static void ToggleComment(this TextEditor editor)
    {
        int startLine = editor.Document.GetLineByOffset(editor.SelectionStart).LineNumber;
        int endLine = editor.Document.GetLineByOffset(editor.SelectionStart + editor.SelectionLength)
            .LineNumber;

        using (editor.Document.RunUpdate())
        {
            for (int i = startLine; i <= endLine; i++)
            {
                var line = editor.Document.GetLineByNumber(i);
                string text = editor.Document.GetText(line.Offset, line.Length);
                string trimmed = text.TrimStart();

                if (trimmed.StartsWith("//"))
                {
                    int index = text.IndexOf("//");
                    editor.Document.Remove(line.Offset + index, 2);
                }
                else
                {
                    editor.Document.Insert(line.Offset, "//");
                }
            }
        }
    }

    public static void FormatSelection(this TextEditor editor)
    {
        int startOffset = editor.SelectionStart;
        int endOffset = editor.SelectionStart + editor.SelectionLength;

        if (editor.SelectionLength == 0)
        {
            endOffset = editor.Document.TextLength;
        }

        int startLine = editor.Document.GetLineByOffset(startOffset).LineNumber;
        int endLine = editor.Document.GetLineByOffset(
            endOffset > startOffset ? endOffset - 1 : startOffset
        ).LineNumber;

        int braceDepth = 0;
        for (int i = 1; i < startLine; i++)
        {
            var line = editor.Document.GetLineByNumber(i);
            string text = editor.Document.GetText(line.Offset, line.Length);
            braceDepth += CountBraceDelta(text);
            if (braceDepth < 0) braceDepth = 0;
        }

        var sb = new System.Text.StringBuilder();
        for (int i = startLine; i <= endLine; i++)
        {
            var line = editor.Document.GetLineByNumber(i);
            string text = editor.Document.GetText(line.Offset, line.Length);

            string trimmed = text.TrimStart();
            string trimmedEnd = trimmed.TrimEnd();

            if (string.IsNullOrWhiteSpace(trimmedEnd))
            {
                if (i < endLine) sb.AppendLine();
                continue;
            }

            int delta = CountBraceDelta(trimmedEnd);
            int effectiveDepth = braceDepth;

            if (trimmedEnd.EndsWith("}") || trimmedEnd.EndsWith("]"))
            {
                effectiveDepth = braceDepth + delta;
                if (effectiveDepth < 0) effectiveDepth = 0;
            }

            string indent = new string('\t', effectiveDepth);
            sb.Append(indent + trimmedEnd);
            if (i < endLine) sb.AppendLine();

            braceDepth += delta;
            if (braceDepth < 0) braceDepth = 0;
        }

        int replaceOffset = editor.Document.GetLineByNumber(startLine).Offset;
        int replaceLength = editor.Document.GetLineByNumber(endLine).EndOffset - replaceOffset;

        if (editor.Document.GetText(replaceOffset, replaceLength) != sb.ToString())
        {
            editor.Document.Replace(replaceOffset, replaceLength, sb.ToString());
        }
    }

    private static int CountBraceDelta(string line)
    {
        int delta = 0;
        foreach (char c in line)
        {
            if (c == '{' || c == '[') delta++;
            else if (c == '}' || c == ']') delta--;
        }
        return delta;
    }
}