namespace CrossMacro.Platform.Linux.Ipc;

/// <summary>
/// Hooks the transport invokes at precise points of its lifecycle so capture and simulation
/// state can react without the transport referencing them directly. The client wires them to
/// its components in a fixed order.
/// </summary>
/// <param name="OnMessage">Routes one received frame. Called from the read loop; throwing drops the session.</param>
/// <param name="ReplayAfterConnectAsync">Re-issues the required capture command after a successful (re)connect.</param>
/// <param name="OnTransportConnected">Signals that connection replay completed and the session is usable.</param>
/// <param name="OnTransportDropped">
/// Transport references were detached (connection dropped). Fails pending simulation batches, marks capture
/// transport stopped, and fails/notifies the pending capture start. The argument defers error notifications.
/// </param>
/// <param name="OnReadLoopFailure">Read loop failed for the live session; fail pending work and notify.</param>
/// <param name="OnSendFailure">
/// A send failed for the live session. Marks capture stopped, fails the pending start and raises a deferred
/// error notification. Runs before the transport drops the connection.
/// </param>
/// <param name="OnCleanupSubscriptions">Clears (true) or resets (false) capture subscriptions during cleanup.</param>
internal sealed record IpcTransportHandlers(
    Action<BinaryReader, IpcOpCode> OnMessage,
    Func<CancellationToken, Task> ReplayAfterConnectAsync,
    Action OnTransportConnected,
    Action<bool> OnTransportDropped,
    Action<Exception> OnReadLoopFailure,
    Action<IpcOpCode, Exception> OnSendFailure,
    Action<bool> OnCleanupSubscriptions);
