using ICSharpCode.AvalonEdit;

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
}