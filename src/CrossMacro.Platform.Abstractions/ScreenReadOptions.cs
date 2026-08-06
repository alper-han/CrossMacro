
namespace CrossMacro.Platform.Abstractions;

public readonly record struct ScreenReadOptions
{
    public static readonly ScreenReadOptions Default;

    public ScreenReadOptions(
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null,
        CancellationToken cancellationToken = default)
        : this(timeout, pollInterval, pollUntilMatch: false, cancellationToken)
    {
    }

    public ScreenReadOptions(
        TimeSpan? timeout,
        TimeSpan? pollInterval,
        bool pollUntilMatch,
        CancellationToken cancellationToken = default)
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
        PollUntilMatch = pollUntilMatch;
    }

    public TimeSpan? Timeout { get; }

    public TimeSpan? PollInterval { get; }

    public CancellationToken CancellationToken { get; }

    /// <summary>
    /// Requests repeated matching attempts until <see cref="Timeout"/> expires.
    /// The default is false so existing search commands remain one-shot.
    /// </summary>
    public bool PollUntilMatch { get; }
}
