namespace CB2Toolkit.CodeEditor.Utils;

public static class IndentUtils
{
    private static readonly HashSet<string> ControlKeywords = new(StringComparer.Ordinal)
    {
        "if", "while", "for", "switch", "catch"
    };
    
    public static bool IsControlStatementOpener(string trimmedLine)
    {
        if (string.IsNullOrWhiteSpace(trimmedLine)) return false;

        int commentIndex = trimmedLine.IndexOf("//");
        if (commentIndex >= 0) trimmedLine = trimmedLine[..commentIndex];
        commentIndex = trimmedLine.IndexOf("/*");
        if (commentIndex >= 0) trimmedLine = trimmedLine[..commentIndex];
        trimmedLine = trimmedLine.TrimEnd();

        if (trimmedLine.Length == 0) return false;
        if (trimmedLine.EndsWith("{") || trimmedLine.EndsWith(";")) return false;

        if (trimmedLine.StartsWith("case ") || trimmedLine.StartsWith("default")) return true;

        int parenIndex = trimmedLine.IndexOf('(');
        if (parenIndex > 0)
        {
            string prefix = trimmedLine[..parenIndex].TrimEnd();
            string lastWord = prefix.Split(' ', '\t').LastOrDefault() ?? string.Empty;
            if (ControlKeywords.Contains(lastWord)) return true;
        }

        string[] words = trimmedLine.Split(' ', '\t');
        return words[^1] is "else" or "do" or "try" or "finally";
    }
    
    public static int CountIndentLevel(string text)
    {
        int spaces = 0;
        int tabs = 0;
        foreach (char c in text)
        {
            if (c == ' ') spaces++;
            else if (c == '\t') tabs++;
            else break;
        }

        return tabs + spaces / 4;
    }
    
    public static int CountIndentUnits(string text)
    {
        int units = 0;
        foreach (char c in text)
        {
            if (c == '\t') units += 4;
            else if (c == ' ') units++;
            else break;
        }

        return units;
    }
}
