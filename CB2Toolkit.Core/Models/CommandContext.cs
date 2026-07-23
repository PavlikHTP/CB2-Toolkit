using CB2Toolkit.Core.Console.Abstraction;

namespace CB2Toolkit.Core.Models;

public sealed class CommandContext
{
    public required string RawInput { get; init; }
    public required string CommandName { get; init; }
    public required string[] Args { get; init; }
    public required IReadOnlyDictionary<string, string?> Flags { get; init; }
    public required IConsoleOutput Output { get; init; }

    public string? GetArg(int index) => Args.Length > index ? Args[index] : null;
    public bool HasFlag(string name) => Flags.ContainsKey(name);
    public string? GetFlagValue(string name) => Flags.GetValueOrDefault(name);
}