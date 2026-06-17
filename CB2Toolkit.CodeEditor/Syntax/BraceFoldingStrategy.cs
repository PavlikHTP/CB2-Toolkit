using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;

namespace CB2Toolkit.CodeEditor.Syntax;

public class BraceFoldingStrategy
{
    public void UpdateFoldings(FoldingManager manager, TextDocument document)
    {
        int firstErrorOffset;
        var newFoldings = CreateNewFoldings(document, out firstErrorOffset);
        manager.UpdateFoldings(newFoldings, firstErrorOffset);
    }

    public IEnumerable<NewFolding> CreateNewFoldings(TextDocument document, out int firstErrorOffset)
    {
        firstErrorOffset = -1;
        var list = new List<NewFolding>();
        var startOffsets = new Stack<int>();
        bool inString = false;
        bool inSingleComment = false;
        bool inMultiComment = false;

        for (int i = 0; i < document.TextLength; i++)
        {
            char c = document.GetCharAt(i);

            if (inSingleComment)
            {
                if (c == '\n') inSingleComment = false;
                continue;
            }
            if (inMultiComment)
            {
                if (c == '*' && i + 1 < document.TextLength && document.GetCharAt(i + 1) == '/')
                {
                    inMultiComment = false;
                    i++;
                }
                continue;
            }
            if (inString)
            {
                if (c == '"' && i > 0 && document.GetCharAt(i - 1) != '\\') inString = false;
                continue;
            }

            if (c == '/' && i + 1 < document.TextLength)
            {
                if (document.GetCharAt(i + 1) == '/') { inSingleComment = true; i++; continue; }
                if (document.GetCharAt(i + 1) == '*') { inMultiComment = true; i++; continue; }
            }

            if (c == '"') { inString = true; continue; }

            if (c == '{')
            {
                startOffsets.Push(i);
            }
            else if (c == '}' && startOffsets.Count > 0)
            {
                int start = startOffsets.Pop();
                list.Add(new NewFolding(start, i + 1) { Name = "{...}" });
            }
        }

        list.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        return list;
    }
}