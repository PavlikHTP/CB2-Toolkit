using CB2Toolkit.Core.Services;

namespace CB2Toolkit.Core.Plugins;

internal sealed class PluginLogger : ILogger
{
    private readonly string _pluginName;

    public PluginLogger(string pluginName)
    {
        _pluginName = pluginName;
    }

    public void Info(string message) => LoggerService.Instance.LogInfo($"[Plugin:{_pluginName}] {message}");
    public void Warn(string message) => LoggerService.Instance.LogWarn($"[Plugin:{_pluginName}] {message}");
    public void Error(string message) => LoggerService.Instance.LogError($"[Plugin:{_pluginName}] {message}");
    public void Debug(string message) => LoggerService.Instance.LogDebug($"[Plugin:{_pluginName}] {message}");
}
