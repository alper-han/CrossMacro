
namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class KdePositionProviderTests
{
    [Fact]
    public async Task Constructor_OnKdeX11_DoesNotEnableKWinTracker()
    {
        var environment = default(LinuxEnvironmentSnapshot) with
        {
            SessionType = "x11",
            Display = ":0",
            CurrentDesktop = "KDE",
        };

        await using var provider = new KdePositionProvider(environment);

        Assert.False(provider.IsSupported);
    }

    [Fact]
    public async Task InitializationTask_WhenProviderIsUnsupported_CompletesFalse()
    {
        await using var provider = new KdePositionProvider(isSupported: false, autoStartTracking: false);

        Assert.False(await provider.InitializationTask);
    }

    [Fact]
    public async Task InitializationTask_WhenSupportedTrackingIsControlledByCaller_CompletesTrue()
    {
        await using var provider = new KdePositionProvider(isSupported: true, autoStartTracking: false);

        Assert.True(await provider.InitializationTask);
    }

    [Fact]
    public async Task GetAbsolutePositionAsync_ShouldReturnLatestHandlerState()
    {
        await using var provider = new KdePositionProvider(isSupported: true, autoStartTracking: false);

        provider.ApplyPositionUpdate(320, 640);

        var position = await provider.GetAbsolutePositionAsync();

        Assert.Equal((320, 640), position);
    }

    [Fact]
    public async Task GetAbsolutePositionAsync_ShouldWaitForFirstHandlerState()
    {
        await using var provider = new KdePositionProvider(isSupported: true, autoStartTracking: false);

        var positionTask = provider.GetAbsolutePositionAsync();
        provider.ApplyPositionUpdate(320, 640);

        var position = await positionTask;

        Assert.Equal((320, 640), position);
    }

    [Fact]
    public async Task GetScreenResolutionAsync_ShouldReturnResolutionAfterInitializationCallback()
    {
        await using var provider = new KdePositionProvider(isSupported: true, autoStartTracking: false);

        provider.ApplyResolutionUpdate(2560, 1440);

        var resolution = await provider.GetScreenResolutionAsync();

        Assert.Equal((2560, 1440), resolution);
    }

    [Fact]
    public async Task GetDesktopBoundsAsync_ShouldPreserveVirtualDesktopOrigin()
    {
        await using var provider = new KdePositionProvider(isSupported: true, autoStartTracking: false);

        provider.ApplyDesktopBoundsUpdate(-1920, -200, 4480, 1640);

        var bounds = await provider.GetDesktopBoundsAsync();
        var resolution = await provider.GetScreenResolutionAsync();

        Assert.Equal(new ScreenRect(-1920, -200, 4480, 1640), bounds);
        Assert.Equal((4480, 1640), resolution);
    }

    [Fact]
    public async Task GetDesktopBoundsAsync_ShouldReturnLatestTopologyUpdate()
    {
        await using var provider = new KdePositionProvider(isSupported: true, autoStartTracking: false);

        provider.ApplyDesktopBoundsUpdate(0, 0, 1920, 1080);
        _ = await provider.GetDesktopBoundsAsync();
        provider.ApplyDesktopBoundsUpdate(-2560, -400, 6400, 2560);

        var bounds = await provider.GetDesktopBoundsAsync();
        var resolution = await provider.GetScreenResolutionAsync();

        Assert.Equal(new ScreenRect(-2560, -400, 6400, 2560), bounds);
        Assert.Equal((6400, 2560), resolution);
    }

    [Fact]
    public async Task GetAbsolutePositionAsync_ShouldIgnoreUpdatesAfterDispose()
    {
        var provider = new KdePositionProvider(isSupported: true, autoStartTracking: false);
        await provider.DisposeAsync();

        provider.ApplyPositionUpdate(100, 200);

        var position = await provider.GetAbsolutePositionAsync();

        Assert.Null(position);
    }

    [Fact]
    public async Task GetScreenResolutionAsync_ShouldIgnoreUpdatesAfterDispose()
    {
        var provider = new KdePositionProvider(isSupported: true, autoStartTracking: false);
        await provider.DisposeAsync();

        provider.ApplyResolutionUpdate(1920, 1080);

        var resolution = await provider.GetScreenResolutionAsync();

        Assert.Null(resolution);
    }

    [Fact]
    public async Task DisposeAsync_WhenCalledConcurrently_SharesCleanupTask()
    {
        var provider = new KdePositionProvider(isSupported: true, autoStartTracking: false);

        var firstDispose = DisposeProviderAsync(provider);
        var secondDispose = DisposeProviderAsync(provider);

        await Task.WhenAll(firstDispose, secondDispose);
        Assert.True(provider.IsDisposed);
    }

    [Fact]
    public async Task DisposeAndDisposeAsync_WhenRepeated_AreIdempotent()
    {
        var provider = new KdePositionProvider(isSupported: true, autoStartTracking: false);

        provider.Dispose();
        await provider.DisposeAsync();
        provider.Dispose();

        Assert.True(provider.IsDisposed);
    }

    private static async Task DisposeProviderAsync(KdePositionProvider provider)
    {
        await provider.DisposeAsync();
    }

    [Fact]
    public async Task AwaitPositionAsync_ShouldReturnPositionWhenInitializationSucceeds()
    {
        var completedPosition = Task.FromResult((X: 320, Y: 640));

        var position = await KdePositionProvider.AwaitPositionAsync(
            completedPosition,
            TimeSpan.FromSeconds(1),
            _ => Task.CompletedTask);

        Assert.Equal((320, 640), position);
    }

    [Fact]
    public async Task AwaitPositionAsync_ShouldReturnNullWhenTimeoutWins()
    {
        var pendingPosition = new TaskCompletionSource<(int X, int Y)>();

        var position = await KdePositionProvider.AwaitPositionAsync(
            pendingPosition.Task,
            TimeSpan.FromMilliseconds(10),
            _ => Task.CompletedTask);

        Assert.Null(position);
    }

    [Fact]
    public async Task AwaitInitializedValueAsync_ShouldUseOneDeadlineForInitializationAndValue()
    {
        var pendingInitialization = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var value = Task.FromResult(42);

        var result = await KdePositionProvider.AwaitInitializedValueAsync(
            pendingInitialization.Task,
            value,
            TimeSpan.Zero,
            TimeProvider.System,
            CancellationToken.None);

        Assert.False(result.Completed);
    }

    [Fact]
    public async Task AwaitInitializedValueAsync_WhenInitializationSucceeds_ReturnsValue()
    {
        var result = await KdePositionProvider.AwaitInitializedValueAsync(
            Task.FromResult(true),
            Task.FromResult(42),
            TimeSpan.FromSeconds(1),
            TimeProvider.System,
            CancellationToken.None);

        Assert.True(result.Completed);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public async Task AwaitInitializedValueAsync_WhenInitializationRejects_DoesNotWaitForValue()
    {
        var pendingValue = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        var result = await KdePositionProvider.AwaitInitializedValueAsync(
            Task.FromResult(false),
            pendingValue.Task,
            TimeSpan.FromSeconds(1),
            TimeProvider.System,
            CancellationToken.None);

        Assert.False(result.Completed);
    }

    [Fact]
    public async Task AwaitInitializedValueAsync_WhenValueIsDefault_PreservesCompletedState()
    {
        var result = await KdePositionProvider.AwaitInitializedValueAsync(
            Task.FromResult(true),
            Task.FromResult<string?>(null),
            TimeSpan.FromSeconds(1),
            TimeProvider.System,
            CancellationToken.None);

        Assert.True(result.Completed);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task AwaitInitializedValueAsync_WhenCallerCancels_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            KdePositionProvider.AwaitInitializedValueAsync(
                Task.FromResult(true),
                new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously).Task,
                TimeSpan.FromSeconds(1),
                TimeProvider.System,
                cancellation.Token));
    }

    [Fact]
    public void BuildTrackerScriptContent_ShouldPreferCursorPositionNotifications()
    {
        const string callbackDestination = ":1.42";
        var script = KdePositionProvider.BuildTrackerScriptContent(callbackDestination);

        Assert.Contains($"var dbusService = '{callbackDestination}';", script, StringComparison.Ordinal);
        Assert.DoesNotContain("__TRACKER_SERVICE_NAME__", script, StringComparison.Ordinal);
        Assert.Contains("workspace.cursorPosChanged.connect(publishPosition)", script, StringComparison.Ordinal);
        Assert.Contains("var positionTimer = new QTimer();", script, StringComparison.Ordinal);
        Assert.Contains("positionTimer.interval = 1;", script, StringComparison.Ordinal);
        Assert.Contains("positionTimer.timeout.connect(publishPosition)", script, StringComparison.Ordinal);
        Assert.Contains("callDBus(dbusService, dbusPath, dbusInterface, 'UpdatePosition'", script, StringComparison.Ordinal);
        Assert.Contains("callDBus(dbusService, dbusPath, dbusInterface, 'UpdateDesktopBounds'", script, StringComparison.Ordinal);
        Assert.Contains("workspace.virtualScreenGeometryChanged.connect(sendResolution)", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplyPositionUpdate_ShouldNotifyOnlyWhenPositionChanges()
    {
        await using var provider = new KdePositionProvider(isSupported: true, autoStartTracking: false);
        var positions = new List<(int X, int Y)>();
        provider.PositionChanged += (_, e) => positions.Add((e.X, e.Y));

        provider.ApplyPositionUpdate(0, 0);
        provider.ApplyPositionUpdate(0, 0);
        provider.ApplyPositionUpdate(10, 5);

        Assert.Equal([(0, 0), (10, 5)], positions);
    }

    [Fact]
    public async Task ApplyPositionUpdate_AfterTopologyChange_ShouldMarkNewBaselineAsDiscontinuity()
    {
        await using var provider = new KdePositionProvider(isSupported: true, autoStartTracking: false);
        var positions = new List<MousePositionChangedEventArgs>();
        provider.PositionChanged += (_, e) => positions.Add(e);
        provider.ApplyDesktopBoundsUpdate(0, 0, 1920, 1080);
        provider.ApplyPositionUpdate(500, 400);
        positions.Clear();

        provider.ApplyDesktopBoundsUpdate(-1920, 0, 3840, 1080);
        provider.ApplyDesktopBoundsUpdate(-1920, 0, 3840, 1080);
        provider.ApplyPositionUpdate(500, 400);

        var position = Assert.Single(positions);
        Assert.Equal(500, position.X);
        Assert.Equal(400, position.Y);
        Assert.True(position.IsDiscontinuity);
    }

}
