using CB2Toolkit.Core.Models;

namespace CB2Toolkit.Core.Services;

public class LoggerService
{
    private static readonly Lazy<LoggerService> _instance = new(() => new LoggerService());
    public static LoggerService Instance => _instance.Value;
    
    public event Action<LogEntry>? OnLogAdded;
    public event Action? OnLogCleared;

    private LoggerService() { }

    public void Log(string text, string color = "White")
    {
        var entry = new LogEntry 
        { 
            Text = $"[{DateTime.Now:HH:mm:ss}] {text}", 
            Color = color 
        };
        OnLogAdded?.Invoke(entry);
    }

    public void LogWarning(string text) => Log(text, "Yellow");
    public void LogError(string text) => Log(text, "Red");
    public void LogSuccess(string text) => Log(text, "Green");
    public void LogInfo(string text) => Log(text, "Cyan");

    public void Clear()
    {
        OnLogCleared?.Invoke();
    }
}