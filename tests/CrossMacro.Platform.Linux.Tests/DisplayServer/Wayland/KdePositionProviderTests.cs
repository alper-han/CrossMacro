
namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class KdePositionProviderTests
{
    private static readonly string[] ExpectedScriptCalls = ["stop:42", "unload:42"];

    [LinuxFact]
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

    [LinuxFact]
    public async Task GetAbsolutePositionAsync_ShouldReturnLatestHandlerState()
    {
        await using var provider = new KdePositionProvider(isSupported: true, autoStartTracking: false);

        provider.ApplyPositionUpdate(320, 640);

        var position = await provider.GetAbsolutePositionAsync();

        Assert.Equal((320, 640), position);
    }

    [LinuxFact]
    public async Task GetAbsolutePositionAsync_ShouldWaitForFirstHandlerState()
    {
        await using var provider = new KdePositionProvider(isSupported: true, autoStartTracking: false);

        var positionTask = provider.GetAbsolutePositionAsync();
        provider.ApplyPositionUpdate(320, 640);

        var position = await positionTask;

        Assert.Equal((320, 640), position);
    }

    [LinuxFact]
    public async Task GetScreenResolutionAsync_ShouldReturnResolutionAfterInitializationCallback()
    {
        await using var provider = new KdePositionProvider(isSupported: true, autoStartTracking: false);

        provider.ApplyResolutionUpdate(2560, 1440);

        var resolution = await provider.GetScreenResolutionAsync();

        Assert.Equal((2560, 1440), resolution);
    }

    [LinuxFact]
    public async Task GetDesktopBoundsAsync_ShouldPreserveVirtualDesktopOrigin()
    {
        await using var provider = new KdePositionProvider(isSupported: true, autoStartTracking: false);

        provider.ApplyDesktopBoundsUpdate(-1920, -200, 4480, 1640);

        var bounds = await provider.GetDesktopBoundsAsync();
        var resolution = await provider.GetScreenResolutionAsync();

        Assert.Equal(new ScreenRect(-1920, -200, 4480, 1640), bounds);
        Assert.Equal((4480, 1640), resolution);
    }

    [LinuxFact]
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

    [LinuxFact]
    public async Task GetAbsolutePositionAsync_ShouldIgnoreUpdatesAfterDispose()
    {
        var provider = new KdePositionProvider(isSupported: true, autoStartTracking: false);
        provider.Dispose();

        provider.ApplyPositionUpdate(100, 200);

        var position = await provider.GetAbsolutePositionAsync();

        Assert.Null(position);
    }

    [LinuxFact]
    public async Task GetScreenResolutionAsync_ShouldIgnoreUpdatesAfterDispose()
    {
        var provider = new KdePositionProvider(isSupported: true, autoStartTracking: false);
        provider.Dispose();

        provider.ApplyResolutionUpdate(1920, 1080);

        var resolution = await provider.GetScreenResolutionAsync();

        Assert.Null(resolution);
    }

    [LinuxFact]
    public async Task AwaitResolutionAsync_ShouldReturnResolutionWhenInitializationSucceeds()
    {
        var completedResolution = Task.FromResult((Width: 1920, Height: 1080));

        var resolution = await KdePositionProvider.AwaitResolutionAsync(
            completedResolution,
            TimeSpan.FromSeconds(1),
            _ => Task.CompletedTask);

        Assert.Equal((1920, 1080), resolution);
    }

    [LinuxFact]
    public async Task AwaitResolutionAsync_ShouldReturnNullWhenTimeoutWins()
    {
        var pendingResolution = new TaskCompletionSource<(int Width, int Height)>();

        var resolution = await KdePositionProvider.AwaitResolutionAsync(
            pendingResolution.Task,
            TimeSpan.FromMilliseconds(10),
            _ => Task.CompletedTask);

        Assert.Null(resolution);
    }

    [LinuxFact]
    public async Task AwaitPositionAsync_ShouldReturnPositionWhenInitializationSucceeds()
    {
        var completedPosition = Task.FromResult((X: 320, Y: 640));

        var position = await KdePositionProvider.AwaitPositionAsync(
            completedPosition,
            TimeSpan.FromSeconds(1),
            _ => Task.CompletedTask);

        Assert.Equal((320, 640), position);
    }

    [LinuxFact]
    public async Task AwaitPositionAsync_ShouldReturnNullWhenTimeoutWins()
    {
        var pendingPosition = new TaskCompletionSource<(int X, int Y)>();

        var position = await KdePositionProvider.AwaitPositionAsync(
            pendingPosition.Task,
            TimeSpan.FromMilliseconds(10),
            _ => Task.CompletedTask);

        Assert.Null(position);
    }

    [LinuxFact]
    public void BuildTrackerScriptContent_ShouldPreferCursorPositionNotifications()
    {
        var script = KdePositionProvider.BuildTrackerScriptContent();

        Assert.Contains("workspace.cursorPosChanged.connect(publishPosition)", script, StringComparison.Ordinal);
        Assert.Contains("var positionTimer = new QTimer();", script, StringComparison.Ordinal);
        Assert.Contains("positionTimer.interval = 1;", script, StringComparison.Ordinal);
        Assert.Contains("positionTimer.timeout.connect(publishPosition)", script, StringComparison.Ordinal);
        Assert.Contains("callDBus(dbusService, dbusPath, dbusInterface, 'UpdatePosition'", script, StringComparison.Ordinal);
        Assert.Contains("callDBus(dbusService, dbusPath, dbusInterface, 'UpdateDesktopBounds'", script, StringComparison.Ordinal);
        Assert.Contains("workspace.virtualScreenGeometryChanged.connect(sendResolution)", script, StringComparison.Ordinal);
    }

    [LinuxFact]
    public async Task ApplyPositionUpdate_ShouldNotifyOnlyWhenPositionChanges()
    {
        await using var provider = new KdePositionProvider(isSupported: true, autoStartTracking: false);
        var positions = new List<(int X, int Y)>();
        provider.PositionChanged += (_, e) => positions.Add((e.X, e.Y));

        provider.ApplyPositionUpdate(0, 0);
        provider.ApplyPositionUpdate(0, 0);
        provider.ApplyPositionUpdate(10, 5);

        _ = positions.Should().Equal((0, 0), (10, 5));
    }

    [LinuxFact]
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

        var position = positions.Should().ContainSingle().Which;
        _ = position.X.Should().Be(500);
        _ = position.Y.Should().Be(400);
        _ = position.IsDiscontinuity.Should().BeTrue();
    }

    [LinuxFact]
    public async Task StopLoadedScript_ShouldStopBeforeUnload()
    {
        var calls = new List<string>();

        await KdePositionProvider.StopLoadedScriptAsync(
            "42",
            stopScriptAsync: scriptId =>
            {
                calls.Add($"stop:{scriptId}");
                return Task.CompletedTask;
            },
            unloadScriptAsync: scriptId =>
            {
                calls.Add($"unload:{scriptId}");
                return Task.CompletedTask;
            },
            onError: _ => throw new InvalidOperationException("Unexpected error callback."));

        Assert.Equal(ExpectedScriptCalls, calls);
    }

    [LinuxFact]
    public async Task StopLoadedScript_ShouldSkipCleanupWhenDisposedDuringInitializationBeforeScriptLoads()
    {
        var calls = new List<string>();

        await KdePositionProvider.StopLoadedScriptAsync(
            scriptId: null,
            stopScriptAsync: scriptId =>
            {
                calls.Add($"stop:{scriptId}");
                return Task.CompletedTask;
            },
            unloadScriptAsync: scriptId =>
            {
                calls.Add($"unload:{scriptId}");
                return Task.CompletedTask;
            },
            onError: _ => throw new InvalidOperationException("Unexpected error callback."));

        Assert.Empty(calls);
    }

    [LinuxFact]
    public async Task CleanupLoadedScriptIfShutdownRequested_ShouldStopAndUnloadWhenCancellationWasRequested()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var calls = new List<string>();

        var cleanedUp = await KdePositionProvider.CleanupLoadedScriptIfShutdownRequestedAsync(
            disposed: false,
            scriptId: "42",
            cancellationToken: cts.Token,
            stopScriptAsync: scriptId =>
            {
                calls.Add($"stop:{scriptId}");
                return Task.CompletedTask;
            },
            unloadScriptAsync: scriptId =>
            {
                calls.Add($"unload:{scriptId}");
                return Task.CompletedTask;
            },
            onError: _ => throw new InvalidOperationException("Unexpected error callback."));

        Assert.True(cleanedUp);
        Assert.Equal(ExpectedScriptCalls, calls);
    }

    [LinuxFact]
    public async Task CleanupLoadedScriptIfShutdownRequested_ShouldBeNoOpWhenStillRunning()
    {
        using var cts = new CancellationTokenSource();
        var calls = new List<string>();

        var cleanedUp = await KdePositionProvider.CleanupLoadedScriptIfShutdownRequestedAsync(
            disposed: false,
            scriptId: "42",
            cancellationToken: cts.Token,
            stopScriptAsync: scriptId =>
            {
                calls.Add($"stop:{scriptId}");
                return Task.CompletedTask;
            },
            unloadScriptAsync: scriptId =>
            {
                calls.Add($"unload:{scriptId}");
                return Task.CompletedTask;
            },
            onError: _ => throw new InvalidOperationException("Unexpected error callback."));

        Assert.False(cleanedUp);
        Assert.Empty(calls);
    }

    [LinuxFact]
    public async Task StopLoadedScriptAsync_PropagatesCancellation()
    {
        using var cts = new CancellationTokenSource();
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var cleanup = KdePositionProvider.StopLoadedScriptAsync(
            "42",
            _ => pending.Task,
            _ => Task.CompletedTask,
            _ => throw new InvalidOperationException("Unexpected error callback."),
            cts.Token);

        cts.Cancel();

        await TestAssertions.ThrowsAnyAsync<OperationCanceledException>(() => cleanup);
    }
}
