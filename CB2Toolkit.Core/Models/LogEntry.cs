using CB2Toolkit.Core.Models.Enums;

namespace CB2Toolkit.Core.Models;

public class LogEntry
{
    public string Text { get; set; } = string.Empty;
    public string Color { get; set; } = "White";
    public LogType Type { get; set; } = LogType.Info;
}