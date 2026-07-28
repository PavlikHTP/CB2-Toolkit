namespace CB2Toolkit.Core.Plugins;

public interface IPluginContext
{
    string AppDataPath { get; }
    string PluginDataPath { get; }
    ILogger Logger { get; }
}
