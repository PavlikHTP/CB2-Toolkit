namespace CB2Toolkit.Core.Plugins;

public abstract class PluginBase : IPlugin
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract string Author { get; }
    public abstract string Version { get; }
    
    protected IPluginContext Context { get; private set; } = null!;
    protected ILogger Log { get; private set; } = null!;
    protected string DataPath => Context.PluginDataPath;

    private bool _isInitialized;

    public async Task InitializeAsync(IPluginContext context, CancellationToken ct = default)
    {
        Context = context;
        Log = context.Logger;
        
        await OnInitialize(ct);
        _isInitialized = true;
    }

    public async Task ShutdownAsync(CancellationToken ct = default)
    {
        if (!_isInitialized) return;
        
        await OnShutdown(ct);
        _isInitialized = false;
    }

    protected virtual Task OnInitialize(CancellationToken ct) => Task.CompletedTask;
    protected virtual Task OnShutdown(CancellationToken ct) => Task.CompletedTask;
}
