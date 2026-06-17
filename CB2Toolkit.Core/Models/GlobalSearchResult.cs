namespace CB2Toolkit.Core.Models;

public class GlobalSearchResult
{
    public string FilePath { get; init; } = string.Empty;
    public int LineNumber { get; init; }
    public string LineText { get; init; } = string.Empty;
}