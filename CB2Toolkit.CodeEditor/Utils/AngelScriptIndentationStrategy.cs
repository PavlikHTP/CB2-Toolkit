using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Indentation;

namespace CB2Toolkit.CodeEditor.Utils;


public class AngelScriptIndentationStrategy : DefaultIndentationStrategy
{
    public override void IndentLine(TextDocument document, DocumentLine line)
    {
        if (document == null || line == null) return;

        string currentText = document.GetText(line);
        string currentTrimmed = currentText.TrimStart();

        if (currentTrimmed.StartsWith("//") || currentTrimmed.StartsWith("/*") || currentTrimmed.StartsWith("*"))
            return;

        int prevLineNumber = line.LineNumber - 1;
        string prevIndent = string.Empty;
        string prevTrimmed = string.Empty;
        bool inBlockComment = false;

        for (int i = prevLineNumber; i >= 1; i--)
        {
            string text = document.GetText(document.GetLineByNumber(i));
            string trimmed = text.Trim();
            if (trimmed.Length == 0) continue;

            if (trimmed.StartsWith("/*"))
            {
                inBlockComment = true;
                prevTrimmed = trimmed;
                prevIndent = GetLeadingWhitespace(text);
                break;
            }

            prevIndent = GetLeadingWhitespace(text);
            prevTrimmed = trimmed;
            break;
        }

        if (inBlockComment) return;

        int level = CountIndentLevel(prevIndent);
        bool prevEndsWithBrace = prevTrimmed.EndsWith("{");
        bool prevOpensBlock = prevEndsWithBrace || IndentUtils.IsControlStatementOpener(prevTrimmed);
        if (prevOpensBlock) level++;

        string currentTrimmedStart = currentText.TrimStart();
        if (currentTrimmedStart.StartsWith("}") ||
            currentTrimmedStart.StartsWith("case ") ||
            currentTrimmedStart.StartsWith("default"))
        {
            if (!prevEndsWithBrace) level = Math.Max(0, level - 1);
        }

        string unit = prevIndent.Contains('\t') ? "\t" : "    ";
        string newIndent = string.Concat(Enumerable.Repeat(unit, level));

        if (currentText.StartsWith(newIndent)) return;

        document.UndoStack.StartUndoGroup();
        try
        {
            int indentLength = currentText.Length - currentText.TrimStart().Length;
            if (indentLength > 0)
                document.Replace(line.Offset, indentLength, newIndent);
            else
                document.Insert(line.Offset, newIndent);
        }
        finally
        {
            document.UndoStack.EndUndoGroup();
        }
    }

    private static string GetLeadingWhitespace(string text)
    {
        int i = 0;
        while (i < text.Length && (text[i] == ' ' || text[i] == '\t')) i++;
        return text[..i];
    }

    private static int CountIndentLevel(string indent)
    {
        int spaces = indent.Count(c => c == ' ');
        int tabs = indent.Count(c => c == '\t');
        return spaces / 4 + tabs;
    }
}
