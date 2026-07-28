namespace CB2Toolkit.Core.Plugins;

public interface ILogger
{
    void Info(string message);
    void Warn(string message);
    void Error(string message);
    void Debug(string message);
}
