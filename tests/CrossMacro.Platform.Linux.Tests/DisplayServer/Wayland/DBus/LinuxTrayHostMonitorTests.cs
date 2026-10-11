using static CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland.DBus.DbusIntegrationTestSupport;

namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland.DBus;

public sealed class LinuxTrayHostMonitorTests
{
    private const string WatcherName = "org.kde.StatusNotifierWatcher";

    [DbusSessionFact]
    public async Task StartAsync_AbsentWatcher_RemainsUnavailable()
    {
        await using var bus = await CreatePrivateSessionBusAsync();
        using var monitor = new LinuxTrayHostMonitor(bus.CreateConnection);
        await monitor.StartAsync().WaitAsync(timeout: SessionBusTimeout, timeProvider: TimeProvider.System);
        Assert.False(monitor.IsAvailable);
    }

    [DbusSessionFact]
    public async Task StartAsync_WatcherWithoutHost_RemainsUnavailable()
    {
        await using var bus = await CreatePrivateSessionBusAsync();
        using var watcher = await Watcher.CreateAsync(bus: bus, host: false);
        using var monitor = new LinuxTrayHostMonitor(bus.CreateConnection);
        await monitor.StartAsync().WaitAsync(timeout: SessionBusTimeout, timeProvider: TimeProvider.System);
        Assert.False(monitor.IsAvailable);
    }

    [DbusSessionFact]
    public async Task StartAsync_LiveHost_IsAvailable()
    {
        await using var bus = await CreatePrivateSessionBusAsync();
        using var watcher = await Watcher.CreateAsync(bus: bus, host: true);
        using var monitor = new LinuxTrayHostMonitor(bus.CreateConnection);
        await monitor.StartAsync().WaitAsync(timeout: SessionBusTimeout, timeProvider: TimeProvider.System);
        Assert.True(monitor.IsAvailable);
    }

    [DbusSessionFact]
    public async Task HostSignal_MalformedPropertyReply_ClearsAvailability()
    {
        await using var bus = await CreatePrivateSessionBusAsync();
        using var watcher = await Watcher.CreateAsync(bus: bus, host: true);
        using var monitor = new LinuxTrayHostMonitor(bus.CreateConnection);
        await monitor.StartAsync();
        watcher.MalformedReply = true;
        await ExpectChangeAsync(monitor: monitor, expected: false, action: () => watcher.SetHost(true));
    }

    [DbusSessionFact]
    public async Task HostSignals_RegistrationAndLoss_UpdateAvailability()
    {
        await using var bus = await CreatePrivateSessionBusAsync();
        using var watcher = await Watcher.CreateAsync(bus: bus, host: false);
        using var monitor = new LinuxTrayHostMonitor(bus.CreateConnection);
        await monitor.StartAsync();
        await ExpectChangeAsync(monitor: monitor, expected: true, action: () => watcher.SetHost(true));
        await ExpectChangeAsync(monitor: monitor, expected: false, action: () => watcher.SetHost(false));
    }

    [DbusSessionFact]
    public async Task OwnerChanges_LateArrivalLossAndReplacement_UpdateAvailability()
    {
        await using var bus = await CreatePrivateSessionBusAsync();
        using var monitor = new LinuxTrayHostMonitor(bus.CreateConnection);
        await monitor.StartAsync();
        using var watcher = await Watcher.CreateAsync(bus: bus, host: true, acquireName: false);
        await ExpectChangeAsync(monitor: monitor, expected: true, action: () => watcher.AcquireNameAsync());
        await ExpectChangeAsync(monitor: monitor, expected: false, action: () => watcher.Connection.ReleaseNameAsync(WatcherName));
        using var replacement = await Watcher.CreateAsync(bus: bus, host: false);
        Assert.False(monitor.IsAvailable);
        await ExpectChangeAsync(monitor: monitor, expected: true, action: () => replacement.SetHost(true));
    }

    [DbusSessionFact]
    public async Task OwnerReplacement_WithHostlessWatcher_ClearsOldHostAvailability()
    {
        await using var bus = await CreatePrivateSessionBusAsync();
        using var watcher = await Watcher.CreateAsync(bus: bus, host: true);
        using var monitor = new LinuxTrayHostMonitor(bus.CreateConnection);
        await monitor.StartAsync();
        using var replacement = await Watcher.CreateAsync(bus: bus, host: false, acquireName: false);
        await ExpectChangeAsync(monitor: monitor, expected: false, action: () => replacement.AcquireNameAsync());
        await ExpectChangeAsync(monitor: monitor, expected: true, action: () => replacement.SetHost(true));
    }

    [DbusSessionFact]
    public async Task Refresh_OldOwnerReply_CannotRestoreAvailabilityAfterOwnerLoss()
    {
        await using var bus = await CreatePrivateSessionBusAsync();
        using var watcher = await Watcher.CreateAsync(bus: bus, host: true);
        using var monitor = new LinuxTrayHostMonitor(bus.CreateConnection);
        await monitor.StartAsync();
        watcher.HoldReplies = true;
        watcher.SetHost(true);
        await watcher.PendingReply.Task.WaitAsync(timeout: SessionBusTimeout, timeProvider: TimeProvider.System);
        await ExpectChangeAsync(monitor: monitor, expected: false, action: () => watcher.Connection.ReleaseNameAsync(WatcherName));
        var restored = 0;
        monitor.AvailabilityChanged += (_, _) =>
        {
            if (monitor.IsAvailable)
            {
                Interlocked.Increment(ref restored);
            }
        };
        watcher.CompletePendingReply(true);
        await BarrierAsync(monitor, bus);
        Assert.False(monitor.IsAvailable);
        Assert.Equal(1, restored);
    }

    [DbusSessionFact]
    public async Task Dispose_InFlightPropertyRead_CancelsStartupAndStopsNotifications()
    {
        await using var bus = await CreatePrivateSessionBusAsync();
        using var watcher = await Watcher.CreateAsync(bus: bus, host: true);
        watcher.HoldReplies = true;
        using var monitor = new LinuxTrayHostMonitor(bus.CreateConnection);
        var changes = 0;
        monitor.AvailabilityChanged += (_, _) => Interlocked.Increment(ref changes);
        var start = monitor.StartAsync();
        await watcher.PendingReply.Task.WaitAsync(timeout: SessionBusTimeout, timeProvider: TimeProvider.System);
        monitor.Dispose();
        await start.WaitAsync(timeout: SessionBusTimeout, timeProvider: TimeProvider.System);
        watcher.CompletePendingReply(true);
        Assert.False(monitor.IsAvailable);
        Assert.Equal(0, changes);
    }

    [DbusSessionFact]
    public async Task StartAsync_CancellationDuringPropertyRead_RemainsUnavailable()
    {
        await using var bus = await CreatePrivateSessionBusAsync();
        using var watcher = await Watcher.CreateAsync(bus: bus, host: true);
        watcher.HoldReplies = true;
        using var monitor = new LinuxTrayHostMonitor(bus.CreateConnection);
        using var cancellation = new CancellationTokenSource();
        var start = monitor.StartAsync(cancellation.Token);
        await watcher.PendingReply.Task.WaitAsync(timeout: SessionBusTimeout, timeProvider: TimeProvider.System);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => start.WaitAsync(timeout: SessionBusTimeout, timeProvider: TimeProvider.System));
        watcher.CompletePendingReply(true);
        Assert.False(monitor.IsAvailable);
    }

    [DbusSessionFact]
    public async Task StartAsync_UnresponsiveWatcher_TimesOutUnavailable()
    {
        await using var bus = await CreatePrivateSessionBusAsync();
        using var watcher = await Watcher.CreateAsync(bus: bus, host: true);
        watcher.HoldReplies = true;
        using var monitor = new LinuxTrayHostMonitor(bus.CreateConnection, TimeSpan.FromMilliseconds(100));
        await monitor.StartAsync().WaitAsync(timeout: SessionBusTimeout, timeProvider: TimeProvider.System);
        Assert.False(monitor.IsAvailable);
    }

    [DbusSessionFact]
    public async Task ConnectionLoss_ClearsAvailability()
    {
        await using var bus = await CreatePrivateSessionBusAsync();
        using var watcher = await Watcher.CreateAsync(bus: bus, host: true);
        using var connection = bus.CreateConnection();
        using var monitor = new LinuxTrayHostMonitor(() => connection);
        await monitor.StartAsync();
        await ExpectChangeAsync(monitor: monitor, expected: false, action: connection.Dispose);
    }

    [DbusSessionFact]
    public async Task Dispose_LiveHost_ClosesConnectionWithoutFurtherNotifications()
    {
        await using var bus = await CreatePrivateSessionBusAsync();
        using var watcher = await Watcher.CreateAsync(bus: bus, host: true);
        using var connection = bus.CreateConnection();
        using var monitor = new LinuxTrayHostMonitor(() => connection);
        await monitor.StartAsync();
        var disconnected = connection.DisconnectedAsync();
        var changes = 0;
        monitor.AvailabilityChanged += (_, _) => Interlocked.Increment(ref changes);
        monitor.Dispose();
        await disconnected.WaitAsync(timeout: SessionBusTimeout, timeProvider: TimeProvider.System);
        watcher.SetHost(false);
        await monitor.StartAsync();
        Assert.False(monitor.IsAvailable);
        Assert.Equal(0, changes);
    }

    private static Task ExpectChangeAsync(LinuxTrayHostMonitor monitor, bool expected, Action action)
        => ExpectChangeAsync(monitor: monitor, expected: expected, action: () => { action(); return Task.CompletedTask; });

    private static async Task ExpectChangeAsync(LinuxTrayHostMonitor monitor, bool expected, Func<Task> action)
    {
        var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged(object? sender, EventArgs args)
        {
            if (monitor.IsAvailable == expected)
            {
                changed.TrySetResult();
            }
        }
        monitor.AvailabilityChanged += OnChanged;
        try
        {
            await action();
            await changed.Task.WaitAsync(timeout: SessionBusTimeout, timeProvider: TimeProvider.System);
            Assert.Equal(expected, monitor.IsAvailable);
        }
        finally
        {
            monitor.AvailabilityChanged -= OnChanged;
        }
    }

    private static async Task BarrierAsync(LinuxTrayHostMonitor monitor, PrivateDbusSessionBus bus)
    {
        // A new owner's live property round trip is the barrier for the old owner's reply.
        using var replacement = await Watcher.CreateAsync(bus: bus, host: false);
        await ExpectChangeAsync(monitor: monitor, expected: true, action: () => replacement.SetHost(true));
        await ExpectChangeAsync(monitor: monitor, expected: false, action: () => replacement.SetHost(false));
    }

    private sealed class Watcher(DBusConnection connection, bool hostRegistered) : IPathMethodHandler, IDisposable
    {
        public DBusConnection Connection { get; } = connection;
        public string Path => "/StatusNotifierWatcher";
        public bool HandlesChildPaths => false;
        public bool HostRegistered { get; private set; } = hostRegistered;
        public bool HoldReplies { get; set; }
        public bool MalformedReply { get; set; }
        public TaskCompletionSource<MethodContext> PendingReply { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _releaseReply = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public static async Task<Watcher> CreateAsync(PrivateDbusSessionBus bus, bool host, bool acquireName = true)
        {
            var connection = bus.CreateConnection();
            await connection.ConnectAsync().AsTask().WaitAsync(timeout: SessionBusTimeout, timeProvider: TimeProvider.System);
            var watcher = new Watcher(connection: connection, hostRegistered: host);
            connection.AddMethodHandler(watcher);
            if (acquireName)
            {
                await watcher.AcquireNameAsync();
            }
            return watcher;
        }

        public Task AcquireNameAsync() => Connection.RequestNameAsync(WatcherName, RequestNameOptions.Default);

        public ValueTask HandleMethodAsync(MethodContext context)
        {
            var request = context.Request;
            var reader = request.GetBodyReader();
            if (request.InterfaceAsString is not "org.freedesktop.DBus.Properties"
                || request.MemberAsString is not "Get"
                || reader.ReadString() is not WatcherName
                || reader.ReadString() is not "IsStatusNotifierHostRegistered")
            {
                context.ReplyUnknownMethodError();
                return default;
            }
            if (HoldReplies)
            {
                context.DisposesAsynchronously = true;
                PendingReply.TrySetResult(context);
                _ = ReplyWhenReleasedAsync(context);
                return default;
            }
            if (MalformedReply)
            {
                var writer = context.CreateReplyWriter("v");
                writer.WriteSignature("s");
                writer.WriteString("not a boolean");
                context.Reply(writer.CreateMessage());
                return default;
            }
            Reply(context, HostRegistered);
            return default;
        }

        public void CompletePendingReply(bool value) => _releaseReply.TrySetResult(value);

        private async Task ReplyWhenReleasedAsync(MethodContext context)
        {
            using (context)
            {
                var value = await _releaseReply.Task;
                if (!context.RequestAborted.IsCancellationRequested)
                {
                    Reply(context, value);
                }
            }
        }

        private static void Reply(MethodContext context, bool value)
        {
            var writer = context.CreateReplyWriter("v");
            writer.WriteSignature("b");
            writer.WriteBool(value);
            context.Reply(writer.CreateMessage());
        }

        public void SetHost(bool registered)
        {
            HostRegistered = registered;
            var writer = Connection.GetMessageWriter();
            writer.WriteSignalHeader(path: Path, @interface: WatcherName,
                member: registered ? "StatusNotifierHostRegistered" : "StatusNotifierHostUnregistered");
            Assert.True(Connection.TrySendMessage(writer.CreateMessage()));
        }

        public void Dispose()
        {
            _releaseReply.TrySetResult(false);
            Connection.Dispose();
        }
    }
}
