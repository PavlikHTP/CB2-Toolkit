using CB2Toolkit.Core.Models.Enums;

namespace CB2Toolkit.Core.Console.Abstraction;

public interface IConsoleOutput
{
    void WriteLine(string message, LogType type = LogType.Info, string color = "White");
    void WriteInfo(string message);
    void WriteSuccess(string message);
    void WriteWarning(string message);
    void WriteError(string message);
    void WriteDebug(string message);
}