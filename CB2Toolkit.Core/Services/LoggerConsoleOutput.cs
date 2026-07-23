using CB2Toolkit.Core.Console.Abstraction;
using CB2Toolkit.Core.Models.Enums;

namespace CB2Toolkit.Core.Services;

public class LoggerConsoleOutput : IConsoleOutput
{
    public void WriteLine(string message, LogType type = LogType.Info, string color = "White")
    {
        LoggerService.Instance.Log(message, type, color);
    }

    public void WriteInfo(string message) => LoggerService.Instance.LogInfo(message);
    public void WriteSuccess(string message) => LoggerService.Instance.Log(message, LogType.Info, "#10B981");
    public void WriteWarning(string message) => LoggerService.Instance.LogWarn(message);
    public void WriteError(string message) => LoggerService.Instance.LogError(message);
    public void WriteDebug(string message) => LoggerService.Instance.LogDebug(message);
}