using CB2Toolkit.Core.Console.Abstraction;
using CB2Toolkit.Core.Models;
using CB2Toolkit.Core.Utilities;

namespace CB2Toolkit.Core.Console.Commands;

public class SystemInfoCommand : IConsoleCommand
{
    public string Name => "systeminfo";
    public string Description => "Displays system and process memory metrics or runs garbage collection.";
    public string Usage => "systeminfo [--gc]";
    public IReadOnlyList<string> Aliases => new[] { "sysinfo", "si" };

    public Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default)
    {
        bool triggerGc = context.HasFlag("gc") 
                         || context.HasFlag("g") 
                         || context.CommandName.Equals("gc", StringComparison.OrdinalIgnoreCase);

        if (triggerGc)
        {
            long beforeBytes = GC.GetTotalMemory(false);
            
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long afterBytes = GC.GetTotalMemory(true);
            long freedKb = (beforeBytes - afterBytes) / 1024;

            context.Output.WriteSuccess($"[GC] Collect completed. Freed: {freedKb} KB | Current Heap: {afterBytes / (1024 * 1024)} MB");
        }

        var metrics = SystemInfoCollector.GetMetrics();

        context.Output.WriteInfo("=== System & Process Metrics ===");
        context.Output.WriteLine($"Working Set (RAM):   {metrics.WorkingSetMb} MB");
        context.Output.WriteLine($"GC Managed Heap:     {metrics.GcHeapMb} MB");
        context.Output.WriteLine($"System Memory:       {metrics.FreeMemoryMb} MB free / {metrics.TotalMemoryMb} MB total");
        context.Output.WriteLine($"OS Version:          {metrics.OsVersion}");
        context.Output.WriteLine($"Process Arch:        {metrics.ProcessArch}");
        context.Output.WriteLine($".NET Runtime:        {metrics.RuntimeVersion}");

        if (metrics.Error != "None")
        {
            context.Output.WriteWarning($"Collector Warning:  {metrics.Error}");
        }

        return Task.FromResult(CommandResult.Success());
    }
}