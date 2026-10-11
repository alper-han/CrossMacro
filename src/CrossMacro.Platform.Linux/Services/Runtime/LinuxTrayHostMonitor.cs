namespace CrossMacro.Platform.Linux.Services.Runtime;

/// <summary>Observes the session StatusNotifierWatcher and its registered tray host.</summary>
public sealed class LinuxTrayHostMonitor : ITrayHostMonitor
{
    private const string WatcherName = "org.kde.StatusNotifierWatcher";
    private const string WatcherPath = "/StatusNotifierWatcher";
    private readonly Lock _gate = new();
    private readonly Func<DBusConnection> _connectionFactory;
    private readonly TimeSpan _timeout;
    private readonly bool _requireHostName;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly CancellationToken _lifetimeToken;
    private readonly List<IDisposable> _subscriptions = [];
    private DBusConnection? _connection;
    private Task? _startTask;
    private string? _owner;
    private long _generation;
    private bool _available;
    private bool _stopped;

    public LinuxTrayHostMonitor(LinuxEnvironmentSnapshot environment)
        : this(LinuxDbusTransportBoundary.CreateSessionConnection, environment: environment) { }

    internal LinuxTrayHostMonitor(Func<DBusConnection> connectionFactory, TimeSpan? timeout = null,
        LinuxEnvironmentSnapshot environment = default)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _timeout = timeout ?? TimeSpan.FromSeconds(2);
        _lifetimeToken = _lifetime.Token;
        foreach (var part in (environment.CurrentDesktop ?? string.Empty).AsSpan().Split(':'))
        {
            if ((environment.CurrentDesktop ?? string.Empty).AsSpan()[part].Equals("KDE", StringComparison.OrdinalIgnoreCase))
            {
                _requireHostName = !environment.IsFlatpak;
                break;
            }
        }
    }

    public bool IsAvailable { get { lock (_gate) { return _available; } } }
    public event EventHandler? AvailabilityChanged;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_stopped)
            {
                return Task.CompletedTask;
            }
            return _startTask ??= StartCoreAsync(cancellationToken);
        }
    }

    private async Task StartCoreAsync(CancellationToken cancellationToken)
    {
        using var initial = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeToken);
        initial.CancelAfter(_timeout);
        try
        {
            var connection = _connectionFactory();
            lock (_gate)
            {
                if (_stopped) { connection.Dispose(); return; }
                _connection = connection;
            }
            await AwaitAsync(connection.ConnectAsync().AsTask(), initial.Token).ConfigureAwait(false);
            await SubscribeAsync(connection, new MatchRule
            {
                Type = MessageType.Signal,
                Sender = "org.freedesktop.DBus",
                Path = "/org/freedesktop/DBus",
                Interface = "org.freedesktop.DBus",
                Member = "NameOwnerChanged",
                Arg0 = WatcherName,
            }, static (message, state) =>
            {
                var reader = message.GetBodyReader();
                _ = reader.ReadString();
                _ = reader.ReadString();
                return reader.ReadString();
            }, OnOwnerChanged, initial.Token).ConfigureAwait(false);

            if (_requireHostName)
            {
                await SubscribeAsync(connection, new MatchRule
                {
                    Type = MessageType.Signal,
                    Sender = "org.freedesktop.DBus",
                    Path = "/org/freedesktop/DBus",
                    Interface = "org.freedesktop.DBus",
                    Member = "NameOwnerChanged",
                    Arg0Namespace = "org.kde",
                }, static (message, _) => message.GetBodyReader().ReadStringAsSpan().StartsWith("org.kde.StatusNotifierHost-"u8),
                    OnNamedHostOwnerChanged, initial.Token).ConfigureAwait(false);
            }

            await SubscribeAsync(connection, new MatchRule
            {
                Type = MessageType.Signal,
                Path = WatcherPath,
                Interface = WatcherName,
            }, static (message, _) => (message.SenderAsString, message.MemberAsString),
                OnHostSignal, initial.Token).ConfigureAwait(false);

            long generation;
            lock (_gate) { generation = _generation; }
            var writer = connection.GetMessageWriter();
            writer.WriteMethodCallHeader(destination: "org.freedesktop.DBus", path: "/org/freedesktop/DBus",
                @interface: "org.freedesktop.DBus", member: "GetNameOwner", signature: "s");
            writer.WriteString(WatcherName);
            string owner;
            try
            {
                owner = await LinuxDbusTransportBoundary.AwaitReplyAsync(
                    connection.CallMethodAsync(writer.CreateMessage(), static (message, _) => message.GetBodyReader().ReadString()),
                    _timeout, initial.Token).ConfigureAwait(false);
            }
            catch (DBusErrorReplyException ex) when (ex.ErrorName is "org.freedesktop.DBus.Error.NameHasNoOwner")
            {
                return;
            }
            lock (_gate)
            {
                if (_stopped || generation != _generation) { return; }
                _owner = owner;
                generation = ++_generation;
            }
            await RefreshAsync(connection, owner, generation, initial.Token).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Stop(notify: true);
            throw;
        }
        catch (Exception ex) when (ex is DBusExceptionBase or IOException or TimeoutException or OperationCanceledException or InvalidOperationException or ArgumentException)
        {
            Stop(notify: true);
        }
    }

    private async Task SubscribeAsync<T>(DBusConnection connection, MatchRule rule, MessageValueReader<T> reader,
        Action<T> handler, CancellationToken cancellationToken)
    {
        var pending = connection.AddMatchAsync(rule, reader,
            (Notification<T> notification) =>
            {
                if (notification.HasValue) { handler(notification.Value); }
                else if (notification.IsCompletion) { Stop(notify: true); }
            },
            emitOnCapturedContext: false,
            flags: ObserverFlags.EmitOnConnectionClosed | ObserverFlags.EmitOnReaderFailed).AsTask();
        IDisposable subscription;
        try
        {
            subscription = await LinuxDbusTransportBoundary.AwaitReplyAsync(pending, _timeout, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // A late AddMatch reply must not leave an observer rooted after cancellation.
            _ = pending.ContinueWith(static completed => completed.Result.Dispose(), CancellationToken.None,
                TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            throw;
        }
        lock (_gate)
        {
            if (!_stopped)
            {
                _subscriptions.Add(subscription);
                return;
            }
        }
        subscription.Dispose();
    }

    private void OnOwnerChanged(string owner)
    {
        lock (_gate)
        {
            if (_stopped) { return; }
            _owner = string.IsNullOrEmpty(owner) ? null : owner;
            var generation = ++_generation;
            SetAvailable(available: false);
            if (_owner is not null && _connection is not null)
            {
                _ = RefreshAsync(_connection, _owner, generation, _lifetimeToken);
            }
        }
    }

    private void OnHostSignal((string? Sender, string? Member) signal)
    {
        lock (_gate)
        {
            if (_stopped || _owner is null || !string.Equals(signal.Sender, _owner, StringComparison.Ordinal) || _connection is null) { return; }
            if (signal.Member is not ("StatusNotifierHostRegistered" or "StatusNotifierHostUnregistered")) { return; }
            var generation = ++_generation;
            if (signal.Member is "StatusNotifierHostUnregistered") { SetAvailable(available: false); }
            _ = RefreshAsync(_connection, _owner, generation, _lifetimeToken);
        }
    }

    private void OnNamedHostOwnerChanged(bool isHost)
    {
        if (!isHost) { return; }
        lock (_gate)
        {
            if (_stopped || _owner is null || _connection is null) { return; }
            _ = RefreshAsync(_connection, _owner, ++_generation, _lifetimeToken);
        }
    }

    private async Task RefreshAsync(DBusConnection connection, string owner, long generation, CancellationToken cancellationToken)
    {
        var available = false;
        var started = _requireHostName ? Stopwatch.GetTimestamp() : 0;
        try
        {
            var writer = connection.GetMessageWriter();
            writer.WriteMethodCallHeader(destination: owner, path: WatcherPath,
                @interface: "org.freedesktop.DBus.Properties", member: "Get", signature: "ss");
            writer.WriteString(WatcherName);
            writer.WriteString("IsStatusNotifierHostRegistered");
            available = await LinuxDbusTransportBoundary.AwaitReplyAsync(
                connection.CallMethodAsync(writer.CreateMessage(), static (message, _) => message.GetBodyReader().ReadVariantValue().GetBool()),
                _timeout, cancellationToken).ConfigureAwait(false);
            if (available && _requireHostName)
            {
                // KDED keeps reporting a registered host even after plasmashell exits.
                // Check its actual bus name without adding desktop-specific permissions.
                var remaining = _timeout - Stopwatch.GetElapsedTime(started);
                if (remaining <= TimeSpan.Zero) { throw new TimeoutException(); }
                var names = connection.GetMessageWriter();
                names.WriteMethodCallHeader(destination: "org.freedesktop.DBus", path: "/org/freedesktop/DBus",
                    @interface: "org.freedesktop.DBus", member: "ListNames");
                available = await LinuxDbusTransportBoundary.AwaitReplyAsync(
                    connection.CallMethodAsync(names.CreateMessage(), static (message, _) =>
                    {
                        var reader = message.GetBodyReader();
                        var end = reader.ReadArrayStart(DBusType.String);
                        while (reader.HasNext(end))
                        {
                            if (reader.ReadStringAsSpan().StartsWith("org.kde.StatusNotifierHost-"u8)) { return true; }
                        }
                        return false;
                    }), remaining, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is DBusExceptionBase or IOException or TimeoutException or OperationCanceledException or InvalidOperationException or ArgumentException or InvalidCastException)
        {
            // An unavailable or malformed watcher cannot provide a usable tray host.
            available = false;
        }
        lock (_gate)
        {
            if (!_stopped && !cancellationToken.IsCancellationRequested && generation == _generation && string.Equals(owner, _owner, StringComparison.Ordinal))
            {
                SetAvailable(available);
            }
        }
    }

    private Task AwaitAsync(Task task, CancellationToken cancellationToken)
        => LinuxDbusTransportBoundary.AwaitReplyAsync(task, _timeout, cancellationToken);

    // Called under _gate so disposal cannot race a notification or an old refresh result.
    private void SetAvailable(bool available)
    {
        if (_available == available) { return; }
        _available = available;
        AvailabilityChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Stop(bool notify)
    {
        DBusConnection? connection;
        lock (_gate)
        {
            if (_stopped) { return; }
            _stopped = true;
            ++_generation;
            _owner = null;
            if (notify) { SetAvailable(available: false); }
            else { _available = false; }
            AvailabilityChanged = null;
            connection = _connection;
            _connection = null;
        }
        // Protocol observer disposal waits for callbacks; never hold our callback gate here.
        _lifetime.Cancel();
        foreach (var subscription in _subscriptions) { subscription.Dispose(); }
        _subscriptions.Clear();
        connection?.Dispose();
        _lifetime.Dispose();
    }

    public void Dispose() => Stop(notify: false);
}
