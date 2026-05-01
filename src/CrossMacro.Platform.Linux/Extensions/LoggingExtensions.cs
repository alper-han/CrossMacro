using System.Collections.Concurrent;
using CrossMacro.Core.Logging;

namespace CrossMacro.Platform.Linux.Extensions;

/// <summary>
/// Utility for logging related extensions and helpers.
/// </summary>
public static class LoggingExtensions
{
    private static readonly ConcurrentDictionary<string, bool> _loggedKeys = new();

    /// <summary>
    /// Logs an information message only once for a given key.
    /// Subsequent calls with the same key will be ignored.
    /// </summary>
    /// <param name="key">Unique key to identify the log message context.</param>
    /// <param name="messageTemplate">The message template describing the event.</param>
    /// <param name="args">Optional arguments for the message template.</param>
    public static void LogOnce(string key, string messageTemplate, params object[] args)
    {
        if (!_loggedKeys.TryAdd(key, true))
        {
            return;
        }

        Log.Information(messageTemplate, args);
    }
}
