namespace CB2Toolkit.Core.Models;

public record class LogEntry
{
    public string Text { get; set; } = string.Empty;
    public string Color { get; set; } = "Cyan";
}