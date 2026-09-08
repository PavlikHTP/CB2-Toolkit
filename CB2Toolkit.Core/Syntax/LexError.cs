namespace CB2Toolkit.Core.Syntax;

public sealed class LexError
{
    public int Offset { get; init; }
    public int Length { get; init; }
    public int Line { get; init; }
    public int Column { get; init; }
    public string Message { get; init; } = string.Empty;
}
