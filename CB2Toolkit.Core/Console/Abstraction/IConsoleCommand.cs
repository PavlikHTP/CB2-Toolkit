using CB2Toolkit.Core.Models;

namespace CB2Toolkit.Core.Console.Abstraction;

public interface IConsoleCommand
{
    string Name { get; }
    string Description { get; }
    string Usage { get; }
    IReadOnlyList<string> Aliases => Array.Empty<string>();
    Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default);
}