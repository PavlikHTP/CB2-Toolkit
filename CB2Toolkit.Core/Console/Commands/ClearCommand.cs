using CB2Toolkit.Core.Console.Abstraction;
using CB2Toolkit.Core.Models;
using CB2Toolkit.Core.Services;

namespace CB2Toolkit.Core.Console.Commands;

public class ClearCommand : IConsoleCommand
{
    public string Name => "clear";
    public string Description => "Clears the console log output.";
    public string Usage => "clear";
    public IReadOnlyList<string> Aliases => new[] { "clr" };

    public Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default)
    {
        LoggerService.Instance.Clear();
        return Task.FromResult(CommandResult.Success());
    }
}