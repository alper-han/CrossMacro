using System.Collections.Concurrent;

namespace CrossMacro.Platform.Linux.Extensions;

/// <summary>
/// Utility for logging related extensions and helpers.
/// </summary>
internal static class LoggingExtensions
{
    private static readonly ConcurrentDictionary<string, bool> _loggedKeys = new(StringComparer.Ordinal);

    /// <summary>
    /// Logs an information message only once for a given key.
    /// Subsequent calls with the same key will be ignored.
    /// </summary>
    /// <param name="key">Identifies the message. Keys are remembered for the life of the process, so callers must draw them from a small fixed set such as an enum value, never from user or environment text.</param>
    /// <param name="messageTemplate">The message template describing the event.</param>
    /// <param name="args">Optional arguments for the message template.</param>
    public static void LogOnce(string key, string messageTemplate, params object[] args)
    {
        if (!_loggedKeys.TryAdd(key, value: true))
        {
            return;
        }

        Log.Information(messageTemplate, args);
    }
}
