namespace CrossMacro.Platform.Linux.Ipc;

/// <summary>
/// Hooks the transport invokes at precise points of its lifecycle so capture and simulation
/// state can react without the transport referencing them directly. Implemented by
/// <see cref="IpcClient"/>, which fans the calls out to its components in a fixed order.
/// </summary>
internal interface IIpcTransportCallbacks
{
    /// <summary>Routes one received frame. Called from the read loop.</summary>
    public void OnMessage(BinaryReader reader, IpcOpCode opcode);

    /// <summary>Re-issues the required capture command after a successful (re)connect.</summary>
    public Task ReplayAfterConnectAsync(CancellationToken token);

    /// <summary>
    /// Transport references were detached (connection dropped). Fails pending simulation
    /// batches, marks capture transport stopped, and fails/notifies the pending capture start.
    /// </summary>
    public void OnTransportDropped(bool deferErrorNotifications);

    /// <summary>Read loop failed for the live session; fail pending work and notify.</summary>
    public void OnReadLoopFailure(Exception exception);

    /// <summary>
    /// A send failed for the live session. Marks capture stopped, fails the pending start and
    /// raises a deferred error notification. Runs before the transport drops the connection.
    /// </summary>
    public void OnSendFailure(IpcOpCode opcode, Exception exception);

    /// <summary>Clears or resets capture subscriptions during cleanup.</summary>
    public void OnCleanupSubscriptions(bool clearSubscriptions);
}
