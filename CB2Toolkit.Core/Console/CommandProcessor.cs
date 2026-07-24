using System.Diagnostics;
using CB2Toolkit.Core.Console.Abstraction;
using CB2Toolkit.Core.Models;
using CB2Toolkit.Core.Services;
using CB2Toolkit.Core.Utilities;

namespace CB2Toolkit.Core.Console;

public class CommandProcessor
{
    private readonly CommandRegistry _registry;
    private readonly IConsoleOutput _output;

    public CommandRegistry Registry => _registry;

    public CommandProcessor(CommandRegistry registry, IConsoleOutput output)
    {
        _registry = registry;
        _output = output;
    }

    public async Task<CommandResult> ExecuteAsync(string input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return CommandResult.Success();
        }

        var (commandName, args, flags) = CommandTokenizer.Parse(input);

        if (!_registry.TryGetCommand(commandName, out var command) || command == null)
        {
            _output.WriteError($"Command '{commandName}' not found. Type 'help' for available commands.");
            return CommandResult.Fail($"Unknown command '{commandName}'");
        }

        var context = new CommandContext
        {
            RawInput = input,
            CommandName = commandName,
            Args = args,
            Flags = flags,
            Output = _output
        };

        var sw = Stopwatch.StartNew();

        try
        {
            var result = await command.ExecuteAsync(context, ct);
            sw.Stop();

            if (!result.IsSuccess && !string.IsNullOrEmpty(result.Message))
            {
                _output.WriteError(result.Message);
            }

            if (context.HasFlag("verbose") || context.HasFlag("v"))
            {
                _output.WriteDebug($"[Execution Time: {sw.ElapsedMilliseconds}ms]");
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            _output.WriteWarning($"Command '{commandName}' was canceled.");
            return CommandResult.Fail("Execution canceled by user.");
        }
        catch (Exception ex)
        {
            sw.Stop();
            _output.WriteError($"Execution error in '{commandName}': {ex.Message}");
            _output.WriteDebug(ex.StackTrace ?? string.Empty);
            return CommandResult.Fail(ex.Message);
        }
    }
}