using System.Text.RegularExpressions;

namespace CB2Toolkit.Core.Utilities;

public static class RegexPatterns
{
    public static readonly Regex LeadingWhitespace = new(@"^\s*", RegexOptions.Compiled);

    public static readonly Regex ArgumentsGroup = new(@"\(([^)]+)\)", RegexOptions.Compiled);

    public static readonly Regex MissingComma3Words = new(@"\b\w+\s+\w+\s+\w+\b", RegexOptions.Compiled);

    public static readonly Regex MissingComma4Words = new(@"\b\w+\s+\w+(?!\s*,\s*)\s+\w+\s+\w+\b", RegexOptions.Compiled);

    public static readonly Regex LogFilePath = new(@"^\[(.*?)\]", RegexOptions.Compiled);

    public static readonly Regex LogLineCol = new(@"\((\d+)(?:,\s*(\d+))?\)", RegexOptions.Compiled);

    public static readonly Regex WordBoundary = new(@"\b[A-Za-z_]\w*\b", RegexOptions.Compiled);

    public static readonly Regex FunctionName = new(@"\b[A-Za-z_]\w*(?=\s*\()", RegexOptions.Compiled);

    public static readonly Regex ClassDeclaration = new(@"(?<=\b(class|interface|enum)\s+)[A-Za-z_]\w*", RegexOptions.Compiled);

    public static readonly Regex ClassField = new(@"\b([A-Za-z_]\w*)\s+([^;]+);", RegexOptions.Compiled);

    public static readonly Regex CleanText = new(@"(//[^\r\n]*)|(/\*[\s\S]*?\*/)|(""(?:\\.|[^""\\])*"")|('(?:\\.|[^'\\])*')", RegexOptions.Compiled);

    public static readonly Regex WordTag = new(@"<Word>(.*?)</Word>", RegexOptions.Compiled);

    public static readonly Regex IncludeIncomplete = new(@"^\s*#include\s*""$", RegexOptions.Compiled);

    public static readonly Regex UiElementCreate = new(@"^(?<name>\w+)\[idx\]\s*=\s*gfx\.Create(?<type>\w+)\((?<args>.*)\);", RegexOptions.Compiled);

    public static readonly Regex UiPropertySet = new(@"^(?<name>\w+)\[idx\]\.(?<method>SetColor|SetOpacity|SetScale|SetCallback)\((?<args>.*)\);", RegexOptions.Compiled);

    public static Regex VariableTypeDeclaration(string varName)
    {
        return new Regex(@"\b([A-Za-z_]\w*)\s*@?\s*" + Regex.Escape(varName) + @"\b", RegexOptions.Compiled);
    }

    public static Regex ClassBodySearch(string typeName)
    {
        return new Regex(@"\bclass\s+" + Regex.Escape(typeName) + @"\s*\{", RegexOptions.Compiled);
    }
}
