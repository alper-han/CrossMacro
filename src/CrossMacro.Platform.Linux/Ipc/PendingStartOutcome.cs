namespace CrossMacro.Platform.Linux.Ipc;

/// <summary>How waiting for a pending capture start ended.</summary>
internal enum PendingStartOutcome
{
    /// <summary>No start was in flight, so there was nothing to wait for.</summary>
    NothingToWaitFor,

    /// <summary>The start completed; the required command must be re-validated.</summary>
    Acknowledged,

    /// <summary>A start shared with other consumers failed while this consumer's subscription is unchanged.</summary>
    RetryAfterSharedFailure,
}
