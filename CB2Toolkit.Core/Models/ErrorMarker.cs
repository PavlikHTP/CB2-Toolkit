namespace CB2Toolkit.Core.Models;

public class SyntaxError
{
    public int Offset { get; init; }
    public int Length { get; init; }
    public string Message { get; init; }
}