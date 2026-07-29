
namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class KdePositionProviderTests
{
    private static readonly string[] ExpectedScriptCalls = ["stop:42", "unload:42"];

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
    public void BuildTrackerScriptContent_ShouldUseTimerBasedCursorPolling()
    {
        var script = KdePositionProvider.BuildTrackerScriptContent();

        Assert.Contains("var timer = new QTimer();", script, StringComparison.Ordinal);
        Assert.Contains("timer.timeout.connect(function()", script, StringComparison.Ordinal);
        Assert.Contains("timer.start();", script, StringComparison.Ordinal);
        Assert.Contains("callDBus(dbusService, dbusPath, dbusInterface, 'UpdatePosition'", script, StringComparison.Ordinal);
        Assert.DoesNotContain("cursorPosChanged", script, StringComparison.Ordinal);
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
