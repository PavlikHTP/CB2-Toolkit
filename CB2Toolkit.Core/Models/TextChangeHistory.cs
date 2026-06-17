namespace CB2Toolkit.Core.Models;

public class TextChangeHistory
{
    public int Offset { get; init; }
    public string RemovedText { get; init; }
    public string AddedText { get; init; }
}