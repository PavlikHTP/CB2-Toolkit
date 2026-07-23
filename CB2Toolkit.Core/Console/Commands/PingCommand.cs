using System.Diagnostics;
using System.Net.NetworkInformation;
using CB2Toolkit.Core.Console.Abstraction;
using CB2Toolkit.Core.Models;

namespace CB2Toolkit.Core.Console.Commands;

public class PingCommand : IConsoleCommand
{
    public string Name => "ping";
    public string Description => "Checks network latency or host reachability.";
    public string Usage => "ping [host]";

    public async Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default)
    {
        string host = context.Args.Length > 0 ? context.Args[0] : "google.com";

        var sw = Stopwatch.StartNew();
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, 3000);
            sw.Stop();

            if (reply.Status == IPStatus.Success)
            {
                context.Output.WriteInfo($"{host} -> {reply.RoundtripTime} ms");
                return CommandResult.Success();
            }

            context.Output.WriteError($"{host} unreachable: {reply.Status}");
            return CommandResult.Fail($"Host {host} is unreachable.");
        }
        catch (OperationCanceledException)
        {
            context.Output.WriteError("Operation was canceled.");
            return CommandResult.Fail("Operation canceled.");
        }
        catch (Exception ex)
        {
            context.Output.WriteError($"Error: {ex.Message}");
            return CommandResult.Fail(ex.Message);
        }
    }
}