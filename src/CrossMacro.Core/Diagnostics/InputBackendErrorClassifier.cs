using System;

namespace CrossMacro.Core.Diagnostics;

/// <summary>
/// Classifies known platform/backend availability errors so callers can
/// log expected environment limitations at lower severity.
/// </summary>
public static class InputBackendErrorClassifier
{
    private static readonly string[] KnownUnavailableFragments =
    [
        "No usable Linux input capture backend is available.",
        "No usable Linux input backend is available."
    ];

    public static bool IsKnownUnavailable(Exception? exception)
    {
        if (exception == null)
        {
            return false;
        }

        var current = exception;
        while (current != null)
        {
            if (IsKnownUnavailableMessage(current.Message))
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }

    public static bool IsKnownUnavailableMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        foreach (var fragment in KnownUnavailableFragments)
        {
            if (message.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
