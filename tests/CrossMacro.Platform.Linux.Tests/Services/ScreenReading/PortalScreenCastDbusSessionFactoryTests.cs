
namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading;

public sealed class PortalScreenCastDbusSessionFactoryTests
{
    [Fact]
    public async Task StartSessionAsync_WhenRestoredSessionIsInvalid_ClearsTokenDisposesSessionAndRetriesWithoutRestoreToken()
    {
        var invalidOwner = new CountingDisposable();
        var invalidSession = CreateSession(
            [Stream(42, sourceType: 2U)],
            invalidOwner,
            restoreToken: "invalid-next-token");
        var validSession = CreateSession(
            [Stream(43, id: "valid", sourceType: 1U, x: 0, y: 0, width: 2, height: 1)],
            restoreToken: "valid-next-token");
        var tokenStore = new FakeRestoreTokenStore("stored-token");
        var clientFactory = new FakeSessionClientFactory(
            new FakeSessionClient(invalidSession),
            new FakeSessionClient(validSession));
        var factory = new PortalScreenCastDbusSessionFactory(tokenStore, clientFactory);

        var result = await factory.StartSessionAsync(ScreenReadOptions.Default);

        Assert.True(result.IsSuccess);
        Assert.Same(validSession, result.Session);
        Assert.Equal(2, clientFactory.ConnectCalls);
        Assert.Equal("stored-token", clientFactory.Clients[0].RestoreToken);
        Assert.Null(clientFactory.Clients[0].RestoreData);
        Assert.Null(clientFactory.Clients[1].RestoreToken);
        Assert.Equal(1, tokenStore.ClearCalls);
        Assert.Equal(["valid-next-token"], tokenStore.SavedTokens);
        Assert.Equal(1, invalidOwner.DisposeCount);
    }

    [Fact]
    public async Task StartSessionAsync_WhenRestoredSessionIsValid_SavesRefreshedTokenWithoutRetry()
    {
        var session = CreateSession(
            [Stream(42, id: "valid", sourceType: 1U, x: 0, y: 0, width: 2, height: 1)],
            restoreToken: "next-token");
        var tokenStore = new FakeRestoreTokenStore("stored-token");
        var clientFactory = new FakeSessionClientFactory(new FakeSessionClient(session));
        var factory = new PortalScreenCastDbusSessionFactory(tokenStore, clientFactory);

        var result = await factory.StartSessionAsync(ScreenReadOptions.Default);

        Assert.True(result.IsSuccess);
        Assert.Same(session, result.Session);
        Assert.Equal(1, clientFactory.ConnectCalls);
        Assert.Equal("stored-token", clientFactory.Clients[0].RestoreToken);
        Assert.Equal(0, tokenStore.ClearCalls);
        Assert.Equal(["next-token"], tokenStore.SavedTokens);
    }

    [Fact]
    public async Task StartSessionAsync_ForwardsAndPersistsLegacyRestoreData()
    {
        var session = CreateSession(
            [Stream(42, id: "valid", sourceType: 1U, x: 0, y: 0, width: 2, height: 1)],
            restoreData: "next-restore-data");
        var tokenStore = new FakeRestoreTokenStore("stored-token", "stored-restore-data");
        var clientFactory = new FakeSessionClientFactory(new FakeSessionClient(session));
        var factory = new PortalScreenCastDbusSessionFactory(tokenStore, clientFactory);

        var result = await factory.StartSessionAsync(ScreenReadOptions.Default);

        Assert.True(result.IsSuccess);
        Assert.Equal("stored-restore-data", clientFactory.Clients[0].RestoreData);
        Assert.Equal(["next-restore-data"], tokenStore.SavedRestoreData);
    }

    [Fact]
    public async Task StartSessionAsync_WhenInteractiveSessionIsInvalid_DoesNotSaveToken()
    {
        var invalidSession = CreateSession(
            [Stream(42, sourceType: 2U)],
            restoreToken: "bad-next-token");
        var tokenStore = new FakeRestoreTokenStore(initialToken: null);
        var clientFactory = new FakeSessionClientFactory(new FakeSessionClient(invalidSession));
        var factory = new PortalScreenCastDbusSessionFactory(tokenStore, clientFactory);

        var result = await factory.StartSessionAsync(ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.CaptureFailed, result.ErrorKind);
        Assert.Empty(tokenStore.SavedTokens);
        Assert.Equal(0, tokenStore.ClearCalls);
        Assert.Equal(1, clientFactory.ConnectCalls);
    }

    [Fact]
    public async Task StartSessionAsync_WhenRequestedRegionIsOutsideSelectedMonitor_KeepsSessionAndToken()
    {
        var session = CreateSession(
            [Stream(42, id: "dp-2", sourceType: 1U, x: 0, y: 0, width: 2, height: 1)],
            restoreToken: "next-token");
        var tokenStore = new FakeRestoreTokenStore("stored-token");
        var clientFactory = new FakeSessionClientFactory(new FakeSessionClient(session));
        var factory = new PortalScreenCastDbusSessionFactory(tokenStore, clientFactory, NoopLeaseAsync);

        var result = await factory.StartSessionAsync(new ScreenRect(3, 0, 1, 1), ScreenReadOptions.Default);

        Assert.True(result.IsSuccess);
        Assert.Same(session, result.Session);
        Assert.Equal(1, clientFactory.ConnectCalls);
        Assert.Equal(0, tokenStore.ClearCalls);
        Assert.Equal(["next-token"], tokenStore.SavedTokens);
    }

    [Fact]
    public async Task StartSessionAsync_WhenRestoreStartFailsWithCaptureError_RetriesWithoutToken()
    {
        var validSession = CreateSession(
            [Stream(43, id: "valid", sourceType: 1U, x: 0, y: 0, width: 2, height: 1)],
            restoreToken: "valid-next-token");
        var tokenStore = new FakeRestoreTokenStore("stored-token");
        var clientFactory = new FakeSessionClientFactory(
            new ThrowingSessionClient(new PortalScreenCastException(ScreenReadErrorKind.CaptureFailed, "restore rejected")),
            new FakeSessionClient(validSession));
        var factory = new PortalScreenCastDbusSessionFactory(tokenStore, clientFactory, NoopLeaseAsync);

        var result = await factory.StartSessionAsync(ScreenReadOptions.Default);

        Assert.True(result.IsSuccess);
        Assert.Same(validSession, result.Session);
        Assert.Equal(2, clientFactory.ConnectCalls);
        Assert.Equal(1, tokenStore.ClearCalls);
        Assert.Equal(["valid-next-token"], tokenStore.SavedTokens);
    }

    [Fact]
    public async Task StartSessionAsync_WhenRestoreStartIsDenied_DoesNotRetryOrClearToken()
    {
        var tokenStore = new FakeRestoreTokenStore("stored-token");
        var clientFactory = new FakeSessionClientFactory(
            new ThrowingSessionClient(new PortalScreenCastException(ScreenReadErrorKind.PermissionDenied, "permission denied")));
        var factory = new PortalScreenCastDbusSessionFactory(tokenStore, clientFactory, NoopLeaseAsync);

        var result = await factory.StartSessionAsync(ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.PermissionDenied, result.ErrorKind);
        Assert.Equal(1, clientFactory.ConnectCalls);
        Assert.Equal(0, tokenStore.ClearCalls);
    }

    [Fact]
    public async Task StartSessionAsync_WhenRestoreStartTimesOut_DoesNotClearStoredRestoreState()
    {
        var tokenStore = new FakeRestoreTokenStore("stored-token", "stored-data");
        var clientFactory = new FakeSessionClientFactory(
            new ThrowingSessionClient(new PortalScreenCastException(ScreenReadErrorKind.CaptureTimeout, "capture timed out")));
        var factory = new PortalScreenCastDbusSessionFactory(tokenStore, clientFactory, NoopLeaseAsync);

        var result = await factory.StartSessionAsync(ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.CaptureTimeout, result.ErrorKind);
        Assert.Equal(0, tokenStore.ClearCalls);
    }

    [Fact]
    public async Task StartSessionAsync_WhenRestoreTokenPersistenceFailsAfterSessionCreation_DisposesSessionAndClient()
    {
        var owner = new CountingDisposable();
        var session = CreateSession(
            [Stream(42, id: "valid", sourceType: 1U, x: 0, y: 0, width: 2, height: 1)],
            owner,
            restoreToken: "next-token");
        var client = new FakeSessionClient(session);
        var factory = new PortalScreenCastDbusSessionFactory(
            new ThrowingRestoreTokenStore(),
            new FakeSessionClientFactory(client));

        var result = await factory.StartSessionAsync(ScreenReadOptions.Default);

        Assert.False(result.IsSuccess);
        Assert.Equal(ScreenReadErrorKind.CaptureFailed, result.ErrorKind);
        Assert.Equal(1, owner.DisposeCount);
        Assert.Equal(1, client.DisposeCount);
    }

    private static PortalScreenCastSession CreateSession(
        IReadOnlyList<PortalStreamDescriptor> streams,
        CountingDisposable? owner = null,
        string? restoreToken = null,
        string? restoreData = null)
    {
        return new PortalScreenCastSession(
            "/org/freedesktop/portal/desktop/session/fake",
            streams,
            new SafeFileHandle(new IntPtr(-1), ownsHandle: false),
            owner,
            restoreToken,
            restoreData);
    }

    private static PortalStreamDescriptor Stream(
        uint nodeId,
        string? id = "monitor",
        uint sourceType = 1U,
        int x = 0,
        int y = 0,
        int width = 1920,
        int height = 1080)
    {
        var properties = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["source_type"] = sourceType,
            ["position"] = new object[] { x, y },
            ["size"] = new object[] { width, height },
        };

        if (id is not null)
        {
            properties["id"] = id;
        }

        return new PortalStreamDescriptor(nodeId, properties);
    }

    private sealed class FakeRestoreTokenStore(string? initialToken, string? initialData = null) : IPortalScreenCastRestoreTokenStore
    {
        private readonly string? _initialToken = initialToken;
        private readonly string? _initialData = initialData;

        public int ClearCalls { get; private set; }

        public List<string> SavedTokens { get; } = [];

        public List<string> SavedRestoreData { get; } = [];

        public Task<string?> LoadRestoreTokenAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_initialToken);
        }

        public Task<string?> LoadRestoreDataAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_initialData);
        }

        public Task SaveRestoreTokenAsync(string restoreToken)
        {
            SavedTokens.Add(restoreToken);
            return Task.CompletedTask;
        }

        public Task SaveRestoreDataAsync(string restoreData)
        {
            SavedRestoreData.Add(restoreData);
            return Task.CompletedTask;
        }

        public Task ClearRestoreTokenAsync()
        {
            ClearCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingRestoreTokenStore : IPortalScreenCastRestoreTokenStore
    {
        public Task<string?> LoadRestoreTokenAsync(CancellationToken cancellationToken) => Task.FromResult<string?>(null);

        public Task<string?> LoadRestoreDataAsync(CancellationToken cancellationToken) => Task.FromResult<string?>(null);

        public Task SaveRestoreTokenAsync(string restoreToken) => throw new ArgumentException("save failed");

        public Task SaveRestoreDataAsync(string restoreData) => throw new ArgumentException("save failed");

        public Task ClearRestoreTokenAsync() => Task.CompletedTask;
    }

    private sealed class FakeSessionClientFactory(params PortalScreenCastDbusSessionFactoryTests.FakeSessionClient[] clients) : IPortalScreenCastSessionClientFactory
    {
        private readonly Queue<FakeSessionClient> _clients = new Queue<FakeSessionClient>(clients);

        public int ConnectCalls { get; private set; }

        public IReadOnlyList<FakeSessionClient> Clients { get; } = clients;

        public Task<IPortalScreenCastSessionClient> ConnectAsync()
        {
            ConnectCalls++;
            return Task.FromResult<IPortalScreenCastSessionClient>(_clients.Dequeue());
        }
    }

    private class FakeSessionClient(PortalScreenCastSession? session, Exception? startException = null) : IPortalScreenCastSessionClient
    {
        private readonly PortalScreenCastSession? _session = session;
        private readonly Exception? _startException = startException;

        public string? RestoreToken { get; private set; }

        public string? RestoreData { get; private set; }

        public int DisposeCount { get; private set; }

        public Task<PortalScreenCastSession> StartAsync(ScreenReadOptions options, string? restoreToken = null, string? restoreData = null)
        {
            RestoreToken = restoreToken;
            RestoreData = restoreData;
            if (_startException is not null)
            {
                return Task.FromException<PortalScreenCastSession>(_startException);
            }

            return Task.FromResult(_session ?? throw new InvalidOperationException("Fake session was not configured."));
        }

        public void DisposeIfNotOwnedBySession()
        {
            DisposeCount++;
        }

        public void Dispose()
        {
            DisposeCount++;
        }
    }

    private sealed class ThrowingSessionClient : FakeSessionClient
    {
        public ThrowingSessionClient(Exception exception) : base(session: null, startException: exception)
        {
        }
    }

    private static Task<IDisposable> NoopLeaseAsync(CancellationToken _) => Task.FromResult<IDisposable>(new CountingDisposable());
}
