using System;

namespace ConsoleCritic.Provider.Logging;

public static class CriticLog
{
    private static readonly CriticLogger _logger = new();

    public static void Event(string message) => _logger.LogEvent(message);

    public static void Error(Exception ex, string context) => _logger.LogError(ex, context);
}