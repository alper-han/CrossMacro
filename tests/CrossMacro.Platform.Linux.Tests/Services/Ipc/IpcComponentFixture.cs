namespace CrossMacro.Platform.Linux.Tests.Services.Ipc;

/// <summary>
/// Wires <see cref="IpcTransport"/> to <see cref="IpcCaptureController"/> the way <see cref="IpcClient"/> does,
/// so tests can drive the gates and failure paths of the components directly.
/// </summary>
internal sealed class IpcComponentFixture : IAsyncDisposable
{
    private int _disposed;

    public IpcComponentFixture(string socketPath, bool autoReconnect = false, TimeSpan? writeTimeout = null)
    {
        Transport = new IpcTransport(
            () => socketPath,
            autoReconnect,
            new IpcTransportHandlers(
                OnMessage,
                ReplayAfterConnectAsync,
                OnTransportConnected,
                OnTransportDropped,
                OnReadLoopFailure,
                OnSendFailure,
                OnCleanupSubscriptions),
            writeTimeout ?? IpcTransport.DefaultWriteTimeout);
        Capture = new IpcCaptureController(Transport, ThrowIfDisposed, RaiseError, RaiseErrorDeferred);
    }

    public IpcTransport Transport { get; }

    public IpcCaptureController Capture { get; }

    /// <summary>Raised for daemon and transport errors the client would surface through its ErrorOccurred event.</summary>
    public event Action<string>? ErrorReported;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) is not 0)
        {
            return;
        }

        Transport.MarkDisposed();
        await Transport.ShutdownAsync();
        await Capture.WaitForDeferredReconcilesAsync();
        Capture.Dispose();
        Transport.Dispose();
    }

    private void OnMessage(BinaryReader reader, IpcOpCode opcode)
    {
        switch (opcode)
        {
            case IpcOpCode.CaptureStarted:
                Capture.HandleCaptureStartedMessage(IpcMessageCodec.ReadRequestId(reader));
                break;
            case IpcOpCode.CaptureStartFailed:
                var failure = IpcMessageCodec.ReadRequestFailurePayload(reader);
                Capture.HandleCaptureStartFailedMessage(failure.RequestId, failure.Message);
                break;
            default:
                throw new InvalidDataException($"Unexpected opcode {opcode} in a capture-only fixture.");
        }
    }

    private Task ReplayAfterConnectAsync(CancellationToken token) => Capture.ReplayAfterConnectAsync(token);

    private static void OnTransportConnected()
    {
        // Connection signalling is exercised through IpcClient.
    }

    private void OnTransportDropped(bool deferErrorNotifications) => Capture.OnTransportDropped(deferErrorNotifications);

    private void OnReadLoopFailure(Exception exception) => Capture.OnReadLoopFailure(exception);

    private void OnSendFailure(IpcOpCode opcode, Exception exception) => Capture.OnSendFailure(opcode, exception);

    private void OnCleanupSubscriptions(bool clearSubscriptions) => Capture.OnCleanupSubscriptions(clearSubscriptions);

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(Transport.IsDisposed, typeof(IpcClient));

    private void RaiseError(string message) => ErrorReported?.Invoke(message);

    private void RaiseErrorDeferred(string message) => Capture.PublishAfterCommands(() => ErrorReported?.Invoke(message));
}
