namespace CrossMacro.Platform.Linux.Ipc;

/// <summary>
/// Owns the daemon connection: socket lifecycle, handshake, session generations, the read
/// loop, send primitives and the auto-reconnect loop.
/// Frames are serialized into a reusable buffer and written to the socket with one call. The
/// write gate serializes writers; the lifecycle lock is only held to read or swap the session
/// and is never held across socket I/O, so teardown never waits behind a stalled daemon.
/// A write that cannot complete within the write timeout drops the session.
/// Callback implementations may take the capture lock, therefore no callback is ever invoked
/// while <c>WriteGate</c> or <c>_lifecycleLock</c> is held.
/// </summary>
internal sealed class IpcTransport(
    Func<string> socketPathResolver,
    bool autoReconnect,
    IpcTransportHandlers handlers,
    TimeSpan writeTimeout) : IDisposable
{
    private const int HandshakeTimeoutMs = 5000;
    private const int ReadBufferSize = 4096;

    public static readonly TimeSpan DefaultWriteTimeout = TimeSpan.FromSeconds(5);

    private readonly Func<string> _socketPathResolver = socketPathResolver;
    private readonly IpcTransportHandlers _handlers = handlers;
    private readonly TimeSpan _writeTimeout = writeTimeout;
    private readonly IpcFrameBuffer _frames = new();

    private IpcConnectionSession? _session;
    private int _sessionGeneration;
    private readonly CancellationTokenSource _reconnectCts = new();
    private readonly Lock _lifecycleLock = new();
    private readonly Lock _reconnectLock = new();
    private Task? _reconnectTask;
    private readonly CancellationTokenSource _disposeCts = new();
    private bool _reconnectEnabled = true;
    private bool _disposed;

    public bool IsConnected => _session?.IsConnected ?? false;
    public bool AutoReconnectEnabled { get; } = autoReconnect;
    public bool IsDisposed => Volatile.Read(ref _disposed);
    public bool IsDisposeRequested => Volatile.Read(ref _disposed) || _disposeCts.IsCancellationRequested;
    public CancellationToken DisposeToken => _disposeCts.Token;
    public CancellationToken SessionOrReconnectToken => _session?.Token ?? _reconnectCts.Token;
    public CancellationToken SessionTokenOrNone => _session?.Token ?? CancellationToken.None;
    public bool IsSessionCancellationRequested => _session?.Token.IsCancellationRequested ?? false;

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
        ObjectDisposedException.ThrowIf(_disposed, typeof(IpcClient));
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

            token = _session?.Token ?? _reconnectCts.Token;
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
            await _handlers.ReplayAfterConnectAsync(token).ConfigureAwait(false);
            _handlers.OnTransportConnected();
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
            socket.SendTimeout = checked((int)_writeTimeout.TotalMilliseconds);
            stream = new NetworkStream(socket);
            var reader = new BinaryReader(new BufferedStream(stream, ReadBufferSize));

            using (var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                handshakeCts.CancelAfter(HandshakeTimeoutMs);
                await IpcHandshakeExchange.PerformAsync(stream, handshakeCts.Token).ConfigureAwait(false);
            }

            var readCts = new CancellationTokenSource();
            lock (_lifecycleLock)
            {
                ObjectDisposedException.ThrowIf(_disposed, typeof(IpcClient));
                var generation = unchecked(++_sessionGeneration);
                var session = new IpcConnectionSession(socket, stream, reader, readCts, generation);
                _session = session;
                session.ReadTask = Task.Factory.StartNew(
                    () => ReadLoop(session),
                    CancellationToken.None,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);
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

    private void ReadLoop(IpcConnectionSession session)
    {
        var token = session.CancellationSource.Token;
        try
        {
            while (!token.IsCancellationRequested)
            {
                var opcode = (IpcOpCode)session.Reader.ReadByte();
                _handlers.OnMessage(session.Reader, opcode);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            HandleReadLoopError(ex, session);
        }
    }

    private void HandleReadLoopError(Exception ex, IpcConnectionSession session)
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
        _handlers.OnReadLoopFailure(ex);
        Cleanup(clearSubscriptions: false, disableReconnect: false, sessionGeneration: session.Generation);
        StartReconnectLoop();
    }

    /// <summary>Sends the opcode followed by an optional payload.</summary>
    public bool Send(IpcOpCode op, Action<BinaryWriter>? payload = null, bool throwOnFailure = false) =>
        SendCore(op, payload, prefixOpCode: true, throwOnFailure);

    /// <summary>Sends a frame whose writer emits the opcode itself.</summary>
    public bool SendFrame(IpcOpCode op, Action<BinaryWriter> frame, bool throwOnFailure = false) =>
        SendCore(op, frame, prefixOpCode: false, throwOnFailure);

    /// <inheritdoc cref="Send"/>
    public Task<bool> SendAsync(
        IpcOpCode op,
        Action<BinaryWriter>? payload = null,
        bool throwOnFailure = false,
        CancellationToken cancellationToken = default) =>
        SendCoreAsync(op, payload, prefixOpCode: true, throwOnFailure, cancellationToken);

    /// <inheritdoc cref="SendFrame"/>
    public Task<bool> SendFrameAsync(
        IpcOpCode op,
        Action<BinaryWriter> frame,
        bool throwOnFailure,
        CancellationToken cancellationToken) =>
        SendCoreAsync(op, frame, prefixOpCode: false, throwOnFailure, cancellationToken);

    private bool SendCore(IpcOpCode op, Action<BinaryWriter>? write, bool prefixOpCode, bool throwOnFailure)
    {
        if (!IsConnected)
        {
            return ConnectionUnavailable(op, throwOnFailure);
        }

        IpcConnectionSession session;
        Exception sendFailure;
        WriteGate.Wait(CancellationToken.None);
        try
        {
            var current = GetConnectedSession();
            if (current is null)
            {
                return ConnectionUnavailable(op, throwOnFailure);
            }

            session = current;
            var frame = _frames.Build(op, write, prefixOpCode);
            try
            {
                session.Stream.Write(frame.Span);
                return true;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                sendFailure = ex;
            }
        }
        finally
        {
            _ = WriteGate.Release();
        }

        // Runs after the gate is released: it takes the capture lock via callbacks, and capture
        // paths holding that lock may wait on the gate (lock-order inversion otherwise).
        HandleSendFailure(sendFailure, op, throwOnFailure, session.Generation);
        return false;
    }

    private async Task<bool> SendCoreAsync(
        IpcOpCode op,
        Action<BinaryWriter>? write,
        bool prefixOpCode,
        bool throwOnFailure,
        CancellationToken cancellationToken)
    {
        IpcConnectionSession session;
        Exception sendFailure;
        await WriteGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = GetConnectedSession();
            if (current is null)
            {
                return ConnectionUnavailable(op, throwOnFailure);
            }

            session = current;
            var frame = _frames.Build(op, write, prefixOpCode);

            // Cancellation applies to waiting for the gate only. Once bytes are leaving, the frame
            // either completes or the session is dropped, so a half-written frame never survives.
            using var writeCts = CancellationTokenSource.CreateLinkedTokenSource(session.Token);
            writeCts.CancelAfter(_writeTimeout);
            try
            {
                await session.Stream.WriteAsync(frame, writeCts.Token).ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException ex) when (!session.Token.IsCancellationRequested)
            {
                sendFailure = new TimeoutException(
                    $"Timed out after {_writeTimeout.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture)}ms writing to the daemon socket.",
                    ex);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                sendFailure = ex;
            }
        }
        finally
        {
            _ = WriteGate.Release();
        }

        HandleSendFailure(sendFailure, op, throwOnFailure, session.Generation);
        return false;
    }

    private IpcConnectionSession? GetConnectedSession()
    {
        lock (_lifecycleLock)
        {
            return _session is { IsConnected: true } session ? session : null;
        }
    }

    private static bool ConnectionUnavailable(IpcOpCode op, bool throwOnFailure)
    {
        if (throwOnFailure)
        {
            throw new IpcClientException(
                IpcClientFailureReason.ConnectFailed,
                $"Failed to send '{op}' because the daemon connection is not available.");
        }

        return false;
    }

    private void HandleSendFailure(Exception ex, IpcOpCode op, bool throwOnFailure, int sessionGeneration)
    {
        // A failure from a session that was already replaced or torn down is not news.
        var isCurrentSession = !IsDisposeRequested && Volatile.Read(ref _sessionGeneration) == sessionGeneration;
        if (isCurrentSession)
        {
            Log.LogError(ex, "Failed to send IPC message: {OpCode}", op);
            _handlers.OnSendFailure(op, ex);

            DropTransport(deferErrorNotifications: true, sessionGeneration: sessionGeneration);
            StartReconnectLoop();
        }

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

        _handlers.OnCleanupSubscriptions(clearSubscriptions);
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

        _handlers.OnCleanupSubscriptions(clearSubscriptions);
    }

    private IpcConnectionSession? DetachSession(int? expectedGeneration, out bool detached)
    {
        lock (_lifecycleLock)
        {
            if (expectedGeneration is not null && _sessionGeneration != expectedGeneration)
            {
                detached = false;
                return null;
            }
            var session = _session;
            _session = null;
            _ = Interlocked.Increment(ref _sessionGeneration);
            detached = true;
            return session;
        }
    }

    private void DropTransport(bool deferErrorNotifications = false, int? sessionGeneration = null)
    {
        var session = DetachSession(sessionGeneration, out var detached);
        if (!detached)
        {
            return;
        }
        _handlers.OnTransportDropped(deferErrorNotifications);
        session?.Dispose();
    }

    private async Task DropTransportAsync(bool deferErrorNotifications = false, int? sessionGeneration = null)
    {
        var session = DetachSession(sessionGeneration, out var detached);
        if (!detached)
        {
            return;
        }
        _handlers.OnTransportDropped(deferErrorNotifications);
        if (session is not null)
        {
            await session.DisposeAsync().ConfigureAwait(false);
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
        var attempt = 0;

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
                    attempt++;
                    LogReconnectAttempt(attempt, ex, delay);
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

    private const int InitialReconnectLogAttempts = 6;
    private const int ReconnectLogFrequency = 20;

    private static void LogReconnectAttempt(int attempt, Exception ex, TimeSpan nextDelay)
    {
        // A daemon restart is an expected transient: keep the first failure fully
        // diagnosable, then stay quiet instead of flooding the log with stack traces.
        // At steady state (5s backoff) every 20th attempt logs roughly once per 100s.
        if (attempt is 1)
        {
            Log.Warning(ex, "[IpcClient] Daemon connection lost; attempting to reconnect");
        }
        else if (attempt is <= InitialReconnectLogAttempts || attempt % ReconnectLogFrequency is 0)
        {
            Log.Information(
                "[IpcClient] Daemon unavailable ({Reason}); retrying in {DelaySeconds:0.#}s",
                ex.Message,
                nextDelay.TotalSeconds);
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
        _frames.Dispose();
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
