using System.Collections.Concurrent;
using CB2Toolkit.Core.Models;
using CB2Toolkit.Core.Models.Enums;

namespace CB2Toolkit.Core.Services;

public class LoggerService
{
    private static readonly Lazy<LoggerService> _instance = new(() => new LoggerService());
    public static LoggerService Instance => _instance.Value;
    
    public event Action<LogEntry>? OnLogAdded;
    public event Action? OnLogCleared;

    private readonly ConcurrentQueue<LogEntry> _history = new();
    private const int MaxHistoryCount = 500;

    private LoggerService() { }

    public IReadOnlyCollection<LogEntry> GetHistory() => _history.ToArray();

    public void Log(string text, LogType type, string color = "White")
    {
        var entry = new LogEntry 
        { 
            Text = $"[{DateTime.Now:HH:mm:ss}] [{type}] {text}", 
            Color = color,
            Type = type
        };

        _history.Enqueue(entry);
        while (_history.Count > MaxHistoryCount)
        {
            _history.TryDequeue(out _);
        }

        OnLogAdded?.Invoke(entry);
    }

    public void LogWarn(string text) => Log(text, LogType.Warn, "Yellow");
    public void LogError(string text) => Log(text, LogType.Error, "Red");
    public void LogInfo(string text) => Log(text, LogType.Info, "Cyan");
    public void LogDebug(string text) => Log(text, LogType.Debug, "Gray");

    public void LogException(Exception ex, string context = "")
    {
        var prefix = string.IsNullOrEmpty(context) ? "" : $"[{context}] ";
        Log($"{prefix}Error: {ex.Message}\n{ex.StackTrace}", LogType.Error, "Red");
    }

    public void Clear()
    {
        _history.Clear();
        OnLogCleared?.Invoke();
    }
}