using System.Text.RegularExpressions;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;

namespace CB2Toolkit.Core.Services;

public class BracketService
{
    private static readonly Lazy<BracketService> _instance = new(() => new BracketService());
    public static BracketService Instance => _instance.Value;

    private BracketService()
    {
    }

    public bool ProcessBracketInput(TextEditor editor, string text)
    {
        if (string.IsNullOrEmpty(text)) return false;

        int offset = editor.CaretOffset;
        TextDocument doc = editor.Document;

        if (text == "{")
        {
            var line = doc.GetLineByOffset(offset);
            string lineText = doc.GetText(line.Offset, offset - line.Offset);
            string indentation = Regex.Match(lineText, @"^\s*").Value;

            bool isNewLine = string.IsNullOrWhiteSpace(lineText.Replace("{", ""));

            if (isNewLine)
            {
                doc.Replace(offset - 1, 1, "{\n" + indentation + "\t\n" + indentation + "}");
                editor.CaretOffset = offset - 1 + 3 + indentation.Length;
            }
            else
            {
                doc.Replace(offset - 1, 1, "\n" + indentation + "{\n" + indentation + "\t\n" + indentation + "}");
                editor.CaretOffset = offset - 1 + 4 + (indentation.Length * 2);
            }
            return true;
        }

        if (text == "(")
        {
            doc.Insert(offset, ")");
            editor.CaretOffset = offset;
            return true;
        }

        if (text == "[")
        {
            doc.Insert(offset, "]");
            editor.CaretOffset = offset;
            return true;
        }

        if (text == ")" || text == "]")
        {
            if (offset < doc.TextLength && doc.GetCharAt(offset) == text[0])
            {
                doc.Remove(offset, 1);
                return true;
            }
        }

        return false;
    }
}