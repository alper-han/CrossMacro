namespace CrossMacro.Platform.Linux.Ipc;

/// <summary>
/// Owns the daemon connection: socket lifecycle, handshake, session generations, the read
/// loop, send primitives and the auto-reconnect loop.
/// Lock hierarchy (outer to inner): <c>WriteGate</c> → <c>_lifecycleLock</c>. Callback
/// implementations may take the capture lock, therefore no callback is ever invoked while
/// <c>WriteGate</c> or <c>_lifecycleLock</c> is held.
/// </summary>
internal sealed class IpcTransport(
    Func<string> socketPathResolver,
    bool autoReconnect,
    IIpcTransportCallbacks callbacks,
    object owner) : IDisposable
{
    private const int HandshakeTimeoutMs = 5000;

    private readonly Func<string> _socketPathResolver = socketPathResolver;
    private readonly IIpcTransportCallbacks _callbacks = callbacks;
    private readonly object _owner = owner;

    private Socket? _socket;
    private NetworkStream? _stream;
    private BinaryReader? _reader;
    private BinaryWriter? _writer;
    private CancellationTokenSource? _cts;
    private Task? _readTask;
    private int _sessionGeneration;
    private readonly CancellationTokenSource _reconnectCts = new();
    private readonly Lock _lifecycleLock = new();
    private readonly Lock _reconnectLock = new();
    private Task? _reconnectTask;
    private readonly CancellationTokenSource _disposeCts = new();
    private bool _reconnectEnabled = true;
    private bool _disposed;

    private sealed class ConnectionSession(
        Socket socket,
        NetworkStream stream,
        BinaryReader reader,
        BinaryWriter writer,
        CancellationTokenSource cancellationSource,
        int generation)
    {
        public Socket Socket { get; } = socket;
        public NetworkStream Stream { get; } = stream;
        public BinaryReader Reader { get; } = reader;
        public BinaryWriter Writer { get; } = writer;
        public CancellationTokenSource CancellationSource { get; } = cancellationSource;
        public int Generation { get; } = generation;
    }

    public bool IsConnected => _socket?.Connected ?? false;
    public bool AutoReconnectEnabled { get; } = autoReconnect;
    public bool IsDisposed => Volatile.Read(ref _disposed);
    public bool IsDisposeRequested => Volatile.Read(ref _disposed) || _disposeCts.IsCancellationRequested;
    public CancellationToken DisposeToken => _disposeCts.Token;
    public CancellationToken SessionOrReconnectToken => _cts?.Token ?? _reconnectCts.Token;
    public CancellationToken SessionTokenOrNone => _cts?.Token ?? CancellationToken.None;
    public bool IsSessionCancellationRequested => _cts?.IsCancellationRequested ?? false;

    internal SemaphoreSlim WriteGate { get; } = new(1, 1);
    internal SemaphoreSlim ConnectGate { get; } = new(1, 1);

    public void MarkDisposed()
    {
        lock (_lifecycleLock)
        {
            _disposed = true;
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, _owner);
    }

    /// <summary>
    /// Returns the live transport token unless the client is disposed. Used by deferred work
    /// that must observe the session that scheduled it.
    /// </summary>
    public bool TryGetLiveToken(out CancellationToken token)
    {
        lock (_lifecycleLock)
        {
            if (_disposed)
            {
                token = CancellationToken.None;
                return false;
            }

            token = _cts?.Token ?? _reconnectCts.Token;
            return true;
        }
    }

    public async Task ConnectAsync(CancellationToken token)
    {
        ThrowIfDisposed();
        var gateAcquired = false;
        try
        {
            await ConnectGate.WaitAsync(token).ConfigureAwait(false);
            gateAcquired = true;
            ThrowIfDisposed();
            await ConnectCoreAsync(token).ConfigureAwait(false);
        }
        finally
        {
            if (gateAcquired)
            {
                _ = ConnectGate.Release();
            }
        }
    }

    private async Task ConnectCoreAsync(CancellationToken token)
    {
        if (IsConnected)
        {
            return;
        }

        var socketPath = _socketPathResolver();
        try
        {
            await ConnectAndHandshakeAsync(socketPath, token).ConfigureAwait(false);
            await _callbacks.ReplayAfterConnectAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (!token.IsCancellationRequested)
        {
            await CleanupAsync(clearSubscriptions: false, disableReconnect: false).ConfigureAwait(false);
            throw new IpcClientException(IpcClientFailureReason.Timeout,
                "Timed out while connecting to or handshaking with CrossMacro daemon.", ex);
        }
        catch (OperationCanceledException)
        {
            await CleanupAsync(clearSubscriptions: false, disableReconnect: false).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex) when (IsPermissionDeniedException(ex))
        {
            await CleanupAsync(clearSubscriptions: false, disableReconnect: false).ConfigureAwait(false);
            throw new IpcClientException(IpcClientFailureReason.PermissionDenied,
                "Permission denied while connecting to or handshaking with CrossMacro daemon.", ex);
        }
        catch (IpcClientException)
        {
            await CleanupAsync(clearSubscriptions: false, disableReconnect: false).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex) when (IsTimeoutException(ex))
        {
            await CleanupAsync(clearSubscriptions: false, disableReconnect: false).ConfigureAwait(false);
            throw new IpcClientException(IpcClientFailureReason.Timeout,
                "Timed out while connecting to or handshaking with CrossMacro daemon.", ex);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            await CleanupAsync(clearSubscriptions: false, disableReconnect: false).ConfigureAwait(false);
            throw new IpcClientException(IpcClientFailureReason.ConnectFailed, "Failed to connect to daemon.", ex);
        }
    }

    private async Task ConnectAndHandshakeAsync(string socketPath, CancellationToken token)
    {
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        NetworkStream? stream = null;
        var installed = false;
        try
        {
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), token).ConfigureAwait(false);
            socket.ReceiveTimeout = HandshakeTimeoutMs;
            socket.SendTimeout = HandshakeTimeoutMs;
            stream = new NetworkStream(socket);
            var reader = new BinaryReader(stream);
            var writer = new BinaryWriter(stream);

            using var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            handshakeCts.CancelAfter(HandshakeTimeoutMs);
            var handshakeToken = handshakeCts.Token;
            var handshakePayload = new byte[sizeof(byte) + sizeof(int)];
            handshakePayload[0] = (byte)IpcOpCode.Handshake;
            BinaryPrimitives.WriteInt32LittleEndian(handshakePayload.AsSpan(1), IpcProtocol.ProtocolVersion);
            await stream.WriteAsync(handshakePayload, handshakeToken).ConfigureAwait(false);
            await stream.FlushAsync(handshakeToken).ConfigureAwait(false);
            var opcode = (IpcOpCode)await IpcHandshakeCodec.ReadByteAsync(stream, handshakeToken).ConfigureAwait(false);
            if (opcode is IpcOpCode.Error)
            {
                var msg = await IpcHandshakeCodec.ReadStringAsync(stream, handshakeToken).ConfigureAwait(false);
                throw new IpcClientException(IpcClientFailureReason.HandshakeFailed, $"Daemon handshake error: {msg}");
            }
            if (opcode is not IpcOpCode.Handshake)
            {
                throw new IpcClientException(IpcClientFailureReason.HandshakeFailed, $"Unexpected handshake opcode: {opcode}");
            }

            var version = await IpcHandshakeCodec.ReadInt32Async(stream, handshakeToken).ConfigureAwait(false);
            if (version != IpcProtocol.ProtocolVersion)
            {
                throw new IpcClientException(IpcClientFailureReason.ProtocolMismatch,
                    $"Protocol version mismatch. Daemon: {version.ToString(CultureInfo.InvariantCulture)}, Client: {IpcProtocol.ProtocolVersion}");
            }

            socket.ReceiveTimeout = 0;
            socket.SendTimeout = 0;
            var readCts = new CancellationTokenSource();
            lock (_lifecycleLock)
            {
                ObjectDisposedException.ThrowIf(_disposed, _owner);
                var generation = unchecked(++_sessionGeneration);
                var session = new ConnectionSession(socket, stream, reader, writer, readCts, generation);
                _socket = session.Socket;
                _stream = session.Stream;
                _reader = session.Reader;
                _writer = session.Writer;
                _cts = session.CancellationSource;
                _readTask = Task.Run(() => ReadLoop(session), CancellationToken.None);
                installed = true;
            }
            Log.Information("Connected to CrossMacro Daemon");
        }
        finally
        {
            if (!installed)
            {
                SafeDispose(stream);
                SafeDispose(socket);
            }
        }
    }

    private void ReadLoop(ConnectionSession session)
    {
        var token = session.CancellationSource.Token;
        try
        {
            while (!token.IsCancellationRequested)
            {
                var opcode = (IpcOpCode)session.Reader.ReadByte();
                _callbacks.OnMessage(session.Reader, opcode);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            HandleReadLoopError(ex, session);
        }
    }

    private void HandleReadLoopError(Exception ex, ConnectionSession session)
    {
        if (IsDisposeRequested)
        {
            return;
        }

        if (session.CancellationSource.IsCancellationRequested ||
            Volatile.Read(ref _sessionGeneration) != session.Generation)
        {
            return;
        }

        Log.LogError(ex, "[IpcClient] Read loop error");
        _callbacks.OnReadLoopFailure(ex);
        Cleanup(clearSubscriptions: false, disableReconnect: false, sessionGeneration: session.Generation);
        StartReconnectLoop();
    }

    public bool Send(
        IpcOpCode op,
        Action<BinaryWriter>? writerAction = null,
        bool throwOnFailure = false)
    {
        if (!IsConnected)
        {
            if (throwOnFailure)
            {
                throw new IpcClientException(
                    IpcClientFailureReason.ConnectFailed,
                    $"Failed to send '{op}' because the daemon connection is not available.");
            }
            return false;
        }

        Exception? sendFailure = null;
        var sessionGeneration = 0;
        WriteGate.Wait(CancellationToken.None);
        try
        {
            lock (_lifecycleLock)
            {
                sessionGeneration = _sessionGeneration;
                if (_socket is null || !_socket.Connected || _writer is null || _stream is null)
                {
                    if (throwOnFailure)
                    {
                        throw new IpcClientException(
                            IpcClientFailureReason.ConnectFailed,
                            $"Failed to send '{op}' because the daemon connection is not available.");
                    }

                    return false;
                }

                try
                {
                    _writer.Write((byte)op);
                    writerAction?.Invoke(_writer);
                    return true;
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    sendFailure = ex;
                }
            }
        }
        finally
        {
            _ = WriteGate.Release();
        }

        HandleSendFailure(sendFailure ?? throw new InvalidOperationException("Send failed but no exception was captured."), op, throwOnFailure, sessionGeneration);
        return false;
    }

    public async Task<bool> SendAsync(
        IpcOpCode op,
        Action<BinaryWriter>? writerAction = null,
        bool throwOnFailure = false,
        CancellationToken cancellationToken = default)
    {
        Exception? sendFailure = null;
        var sessionGeneration = 0;
        await WriteGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!IsConnected)
            {
                if (throwOnFailure)
                {
                    throw new IpcClientException(
                        IpcClientFailureReason.ConnectFailed,
                        $"Failed to send '{op}' because the daemon connection is not available.");
                }

                return false;
            }

            NetworkStream? stream = null;
            lock (_lifecycleLock)
            {
                sessionGeneration = _sessionGeneration;
                if (_socket is null || !_socket.Connected || _writer is null || _stream is null)
                {
                    if (throwOnFailure)
                    {
                        throw new IpcClientException(
                            IpcClientFailureReason.ConnectFailed,
                            $"Failed to send '{op}' because the daemon connection is not available.");
                    }

                    return false;
                }

                try
                {
                    _writer.Write((byte)op);
                    writerAction?.Invoke(_writer);
                    stream = _stream;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    sendFailure = ex;
                }
            }

            if (sendFailure is null)
            {
                try
                {
                    await (stream ?? throw new InvalidOperationException("Stream is not available after successful write.")).FlushAsync(cancellationToken).ConfigureAwait(false);
                    return true;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    sendFailure = ex;
                }
            }
        }
        finally
        {
            _ = WriteGate.Release();
        }

        // Must run after releasing WriteGate: it takes the capture lock via callbacks, and
        // capture paths holding that lock may wait on WriteGate (lock-order inversion otherwise).
        HandleSendFailure(sendFailure ?? throw new InvalidOperationException("Send failed but no exception was captured."), op, throwOnFailure, sessionGeneration);
        return false;
    }

    /// <summary>
    /// Writes raw frames directly through the session writer under the write gate.
    /// Returns the failure (if any) and the session generation the write targeted.
    /// </summary>
    public (Exception? Failure, int SessionGeneration) WriteFrames(Action<BinaryWriter> write)
    {
        Exception? sendFailure = null;
        var sessionGeneration = 0;
        WriteGate.Wait(CancellationToken.None);
        try
        {
            lock (_lifecycleLock)
            {
                sessionGeneration = _sessionGeneration;
                try
                {
                    write(_writer ?? throw new InvalidOperationException("Writer is not available."));
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    sendFailure = ex;
                }
            }
        }
        finally
        {
            _ = WriteGate.Release();
        }

        return (sendFailure, sessionGeneration);
    }

    /// <inheritdoc cref="WriteFrames"/>
    public async Task<(Exception? Failure, int SessionGeneration)> WriteFramesAsync(
        Action<BinaryWriter> write,
        CancellationToken cancellationToken)
    {
        Exception? sendFailure = null;
        var sessionGeneration = 0;
        await WriteGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (_lifecycleLock)
            {
                sessionGeneration = _sessionGeneration;
                try
                {
                    write(_writer ?? throw new InvalidOperationException("Writer is not available."));
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    sendFailure = ex;
                }
            }
        }
        finally
        {
            _ = WriteGate.Release();
        }

        return (sendFailure, sessionGeneration);
    }

    public void HandleSendFailure(Exception ex, IpcOpCode op, bool throwOnFailure, int? sessionGeneration)
    {
        if (IsDisposeRequested)
        {
            if (throwOnFailure)
            {
                throw new IpcClientException(
                    IpcClientFailureReason.ConnectFailed,
                    $"Failed to send IPC command '{op}'.",
                    ex);
            }

            return;
        }

        if (sessionGeneration is not null && Volatile.Read(ref _sessionGeneration) != sessionGeneration)
        {
            if (throwOnFailure)
            {
                throw new IpcClientException(
                    IpcClientFailureReason.ConnectFailed,
                    $"Failed to send IPC command '{op}'.",
                    ex);
            }

            return;
        }

        Log.LogError(ex, "Failed to send IPC message: {OpCode}", op);
        _callbacks.OnSendFailure(op, ex);

        DropTransport(deferErrorNotifications: true, sessionGeneration: sessionGeneration);
        StartReconnectLoop();

        if (throwOnFailure)
        {
            throw new IpcClientException(
                IpcClientFailureReason.ConnectFailed,
                $"Failed to send IPC command '{op}'.",
                ex);
        }
    }

    public void Cleanup(bool clearSubscriptions, bool disableReconnect, int? sessionGeneration = null)
    {
        DropTransport(sessionGeneration: sessionGeneration);

        if (disableReconnect)
        {
            lock (_reconnectLock)
            {
                _reconnectEnabled = false;
            }
            _reconnectCts.Cancel();
        }

        _callbacks.OnCleanupSubscriptions(clearSubscriptions);
    }

    public async Task CleanupAsync(bool clearSubscriptions, bool disableReconnect, int? sessionGeneration = null)
    {
        await DropTransportAsync(sessionGeneration: sessionGeneration).ConfigureAwait(false);

        if (disableReconnect)
        {
            lock (_reconnectLock)
            {
                _reconnectEnabled = false;
            }

            await _reconnectCts.CancelAsync().ConfigureAwait(false);
        }

        _callbacks.OnCleanupSubscriptions(clearSubscriptions);
    }

    private void DropTransport(bool deferErrorNotifications = false, int? sessionGeneration = null)
    {
        CancellationTokenSource? cts;
        BinaryReader? reader;
        BinaryWriter? writer;
        NetworkStream? stream;
        Socket? socket;
        Task? readTask;
        lock (_lifecycleLock)
        {
            if (sessionGeneration is not null && Volatile.Read(ref _sessionGeneration) != sessionGeneration)
            {
                return;
            }

            cts = Interlocked.Exchange(ref _cts, value: null);
            reader = Interlocked.Exchange(ref _reader, value: null);
            writer = Interlocked.Exchange(ref _writer, value: null);
            stream = Interlocked.Exchange(ref _stream, value: null);
            socket = Interlocked.Exchange(ref _socket, value: null);
            readTask = Interlocked.Exchange(ref _readTask, value: null);
            _ = Interlocked.Increment(ref _sessionGeneration);
        }

        _callbacks.OnTransportDropped(deferErrorNotifications);

        // DropTransport can run concurrently from read/send failure paths and Dispose().
        // Detaching references first avoids double-cancel/double-dispose races.
        CancelSafely(cts);
        SafeDispose(reader);
        SafeDispose(writer);
        SafeDispose(stream);
        SafeDispose(socket);
        if (readTask is null || readTask.IsCompleted)
        {
            SafeDispose(cts);
            return;
        }

        _ = ObserveReadTaskCompletionAsync(readTask, cts);
    }

    private async Task DropTransportAsync(bool deferErrorNotifications = false, int? sessionGeneration = null)
    {
        CancellationTokenSource? cts;
        BinaryReader? reader;
        BinaryWriter? writer;
        NetworkStream? stream;
        Socket? socket;
        Task? readTask;
        lock (_lifecycleLock)
        {
            if (sessionGeneration is not null && Volatile.Read(ref _sessionGeneration) != sessionGeneration)
            {
                return;
            }

            cts = Interlocked.Exchange(ref _cts, value: null);
            reader = Interlocked.Exchange(ref _reader, value: null);
            writer = Interlocked.Exchange(ref _writer, value: null);
            stream = Interlocked.Exchange(ref _stream, value: null);
            socket = Interlocked.Exchange(ref _socket, value: null);
            readTask = Interlocked.Exchange(ref _readTask, value: null);
            _ = Interlocked.Increment(ref _sessionGeneration);
        }

        _callbacks.OnTransportDropped(deferErrorNotifications);

        // DropTransport can run concurrently from read/send failure paths and Dispose().
        // Detaching references first avoids double-cancel/double-dispose races.
        if (cts is not null)
        {
            try
            {
                await cts.CancelAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                // expected when CTS was already disposed concurrently during shutdown.
            }
        }

        SafeDispose(reader);
        SafeDispose(writer);
        SafeDispose(stream);
        SafeDispose(socket);
        if (readTask is null || readTask.IsCompleted)
        {
            SafeDispose(cts);
            return;
        }

        _ = ObserveReadTaskCompletionAsync(readTask, cts);
    }

    private static async Task ObserveReadTaskCompletionAsync(
        Task readTask,
        CancellationTokenSource? cts)
    {
        try
        {
            await readTask.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // ReadLoop observes transport failures and does not propagate them.
        }
        finally
        {
            SafeDispose(cts);
        }
    }

    public void StartReconnectLoop()
    {
        if (!AutoReconnectEnabled)
        {
            return;
        }

        lock (_reconnectLock)
        {
            if (!_reconnectEnabled || _disposed)
            {
                return;
            }

            if (_reconnectTask is { IsCompleted: false })
            {
                return;
            }

            _reconnectTask = Task.Run(() => ReconnectLoopAsync(_reconnectCts.Token), _reconnectCts.Token);
        }
    }

    private async Task ReconnectLoopAsync(CancellationToken token)
    {
        var delay = TimeSpan.FromMilliseconds(250);
        var maxDelay = TimeSpan.FromSeconds(5);

        try
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await ConnectAsync(token).ConfigureAwait(false);
                    Log.Information("[IpcClient] Reconnected to daemon");
                    return;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    Log.Warning(ex, "[IpcClient] Reconnect attempt failed");
                }

                await Task.Delay(delay, TimeProvider.System, token).ConfigureAwait(false);
                delay = TimeSpan.FromMilliseconds(Math.Min(maxDelay.TotalMilliseconds, delay.TotalMilliseconds * 2));
            }
        }
        catch (OperationCanceledException)
        {
            // expected when the reconnect loop is cancelled during shutdown.
        }
        finally
        {
            lock (_reconnectLock)
            {
                _reconnectTask = null;
            }
        }
    }

    /// <summary>
    /// First phase of disposal: cancels in-flight work, drops the connection and waits for
    /// the reconnect loop. Gate/CTS disposal happens later in <see cref="Dispose"/> so
    /// deferred work scheduled by the capture controller can still drain.
    /// </summary>
    public async Task ShutdownAsync()
    {
        await _disposeCts.CancelAsync().ConfigureAwait(false);

        await CleanupAsync(clearSubscriptions: true, disableReconnect: true).ConfigureAwait(false);
        Task? reconnectTask;
        lock (_reconnectLock)
        {
            reconnectTask = _reconnectTask;
        }

        if (reconnectTask is not null && reconnectTask.Id != Task.CurrentId)
        {
            try
            {
                await reconnectTask.ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // ReconnectLoop handles expected shutdown cancellation and observes failures.
            }
        }
    }

    public void Dispose()
    {
        _disposeCts.Dispose();
        _reconnectCts.Dispose();
        WriteGate.Dispose();
        ConnectGate.Dispose();
    }

    public static bool IsTimeoutException(Exception ex)
    {
        if (ex is TimeoutException)
        {
            return true;
        }

        if (ex is IOException ioEx && ioEx.InnerException is SocketException ioSocketEx && ioSocketEx.SocketErrorCode is SocketError.TimedOut)
        {
            return true;
        }

        if (ex is SocketException socketEx && socketEx.SocketErrorCode is SocketError.TimedOut)
        {
            return true;
        }

        return false;
    }

    public static bool IsPermissionDeniedException(Exception ex)
    {
        if (ex is UnauthorizedAccessException)
        {
            return true;
        }

        if (ex is SocketException socketEx)
        {
            return socketEx.SocketErrorCode is SocketError.AccessDenied;
        }

        if (ex is IOException ioEx && ioEx.InnerException is SocketException ioSocketEx)
        {
            return ioSocketEx.SocketErrorCode is SocketError.AccessDenied;
        }

        return false;
    }

    private static void CancelSafely(CancellationTokenSource? cts)
    {
        if (cts is null)
        {
            return;
        }

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // expected when CTS was already disposed concurrently during shutdown.
        }
    }

    private static void SafeDispose(IDisposable? disposable)
    {
        if (disposable is null)
        {
            return;
        }

        try
        {
            disposable.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // expected when disposable was already disposed concurrently.
        }
    }
}
