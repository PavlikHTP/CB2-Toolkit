namespace CB2Toolkit.Core.Plugins;

internal sealed class PluginContext : IPluginContext
{
    public string AppDataPath { get; }
    public string PluginDataPath { get; }
    public ILogger Logger { get; }

    public PluginContext(string pluginName, string appDataPath)
    {
        AppDataPath = appDataPath;
        PluginDataPath = Path.Combine(appDataPath, "Plugins", pluginName);
        Logger = new PluginLogger(pluginName);
        
        if (!Directory.Exists(PluginDataPath))
        {
            Directory.CreateDirectory(PluginDataPath);
        }
    }
}
