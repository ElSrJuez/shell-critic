using System;
using System.IO;

namespace ConsoleCritic.Provider.Logging;

public interface ICriticLogger
{
    void LogEvent(string message);
    void LogError(Exception ex, string contextMessage);
}

// Simple file-based logger using Microsoft.Extensions.Logging abstractions
public sealed class CriticLogger : ICriticLogger
{
    private readonly string _eventPath;
    private readonly string _diagPath;
    private readonly object _lock = new();

    public CriticLogger()
    {
        var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ConsoleCritic");
        Directory.CreateDirectory(baseDir);
        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        _eventPath = Path.Combine(baseDir, $"events-{today}.jsonl");
        _diagPath = Path.Combine(baseDir, $"diagnostics-{today}.log");
    }

    public void LogEvent(string message)
    {
        var ts = DateTime.UtcNow.ToString("o");
        var safe = message.Replace("\"", "'");
        var jsonLine = $"{{\"ts\":\"{ts}\",\"event\":\"{safe}\"}}";
        lock (_lock)
        {
            File.AppendAllText(_eventPath, jsonLine + Environment.NewLine);
        }
    }

    public void LogError(Exception ex, string contextMessage)
    {
        lock (_lock)
        {
            File.AppendAllText(_diagPath, $"{DateTime.UtcNow:o}\t{contextMessage}\t{ex}\n");
        
        // Also write an event line so successes & failures appear sequentially
        LogEvent($"ERROR: {contextMessage} -> {ex.Message}");
        }
    }
}