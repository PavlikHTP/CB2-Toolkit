using CB2Toolkit.Core.Console.Abstraction;
using CB2Toolkit.Core.Models;
using CB2Toolkit.Core.Services;

namespace CB2Toolkit.Core.Console.Commands;

public class HelpCommand : IConsoleCommand
{
    private readonly CommandRegistry _registry;

    public string Name => "help";
    public string Description => "Lists all available commands or shows details for a specific command.";
    public string Usage => "help [command_name]";
    public IReadOnlyList<string> Aliases => new[] { "?", "privet" };

    public HelpCommand(CommandRegistry registry)
    {
        _registry = registry;
    }

    public Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default)
    {
        var targetCommand = context.GetArg(0);

        if (string.IsNullOrEmpty(targetCommand))
        {
            context.Output.WriteInfo("=== Available Commands ===");
            foreach (var cmd in _registry.GetAllCommands().OrderBy(c => c.Name))
            {
                var aliasesStr = cmd.Aliases.Count > 0 ? $" ({string.Join(", ", cmd.Aliases)})" : "";
                context.Output.WriteLine($"{cmd.Name}{aliasesStr} - {cmd.Description}");
            }
            return Task.FromResult(CommandResult.Success());
        }

        if (_registry.TryGetCommand(targetCommand, out var command) && command != null)
        {
            context.Output.WriteInfo($"Command: {command.Name}");
            context.Output.WriteLine($"Description: {command.Description}");
            context.Output.WriteLine($"Usage: {command.Usage}");
            if (command.Aliases.Count > 0)
            {
                context.Output.WriteLine($"Aliases: {string.Join(", ", command.Aliases)}");
            }
            return Task.FromResult(CommandResult.Success());
        }

        context.Output.WriteError($"Command '{targetCommand}' not found.");
        return Task.FromResult(CommandResult.Fail("Command not found"));
    }
}