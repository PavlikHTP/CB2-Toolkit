namespace CB2Toolkit.Core.Models;

public sealed class SpellCheckContext
{
    public bool CheckText { get; init; } = true;
    public bool CheckCode { get; init; } = true;

    public Func<string, bool> IsWordKnown { get; init; } = _ => true;
    public Func<string, string?> WordSuggestion { get; init; } = _ => null;

    public Func<string, bool> IsCodeKnown { get; init; } = _ => true;
    public Func<string, string?> CodeSuggestion { get; init; } = _ => null;
}
