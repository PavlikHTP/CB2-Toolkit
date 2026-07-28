namespace CB2Toolkit.Core.Plugins;

public interface IPlugin
{
    string Name { get; }
    string Description { get; }
    string Author { get; }
    string Version { get; }
    
    Task InitializeAsync(IPluginContext context, CancellationToken ct = default);
    Task ShutdownAsync(CancellationToken ct = default);
}
