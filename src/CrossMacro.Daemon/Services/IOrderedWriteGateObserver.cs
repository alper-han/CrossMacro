namespace CrossMacro.Daemon.Services;

/// <summary>
/// Observes the points of <see cref="OrderedWriteGate"/> whose interleaving decides its behavior,
/// so a test can wait for them instead of sleeping. Implementations must not block or re-enter the gate.
/// </summary>
internal interface IOrderedWriteGateObserver
{
    /// <summary>A writer joined the queue. <paramref name="queueLength"/> counts the current holder and every waiter.</summary>
    public void WriterQueued(int queueLength);

    /// <summary>A queued writer was woken by a grant and has not yet taken the gate.</summary>
    public void WriterGranted();
}
