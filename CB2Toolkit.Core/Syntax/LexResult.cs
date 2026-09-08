namespace CB2Toolkit.Core.Syntax;

public sealed class LexResult
{
    public IReadOnlyList<Token> Tokens { get; }
    public IReadOnlyList<LexError> Errors { get; }

    public LexResult(IReadOnlyList<Token> tokens, IReadOnlyList<LexError> errors)
    {
        Tokens = tokens;
        Errors = errors;
    }
}
