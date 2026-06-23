using System;
using System.Threading;

namespace CrossMacro.Platform.Abstractions;

public readonly record struct ScreenReadOptions
{
    public static readonly ScreenReadOptions Default = new();

    public ScreenReadOptions(TimeSpan? timeout = null, TimeSpan? pollInterval = null, CancellationToken cancellationToken = default)
    {
        if (timeout is { } timeoutValue && timeoutValue < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Screen read timeout cannot be negative.");
        }

        if (pollInterval is { } pollIntervalValue && pollIntervalValue < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(pollInterval), pollInterval, "Screen read poll interval cannot be negative.");
        }

        Timeout = timeout;
        PollInterval = pollInterval;
        CancellationToken = cancellationToken;
    }

    public TimeSpan? Timeout { get; }

    public TimeSpan? PollInterval { get; }

    public CancellationToken CancellationToken { get; }
}
