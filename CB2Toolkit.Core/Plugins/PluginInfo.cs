namespace CB2Toolkit.Core.Plugins;

public sealed class PluginInfo
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Author { get; init; }
    public required string Version { get; init; }
    public required string AssemblyPath { get; init; }
    public required DateTime LoadedAt { get; init; }
    
    public IPlugin? Instance { get; internal set; }
    public bool IsEnabled { get; set; } = true;
}
