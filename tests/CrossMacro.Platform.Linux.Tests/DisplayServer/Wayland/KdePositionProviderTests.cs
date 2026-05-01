using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;
using CrossMacro.TestInfrastructure;

namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public class KdePositionProviderTests
{
    [LinuxFact]
    public async Task GetAbsolutePositionAsync_ShouldReturnLatestHandlerState()
    {
        using var provider = new KdePositionProvider(isSupported: true, autoStartTracking: false);

        provider.ApplyPositionUpdate(320, 640);

        var position = await provider.GetAbsolutePositionAsync();

        Assert.Equal((320, 640), position);
    }

    [LinuxFact]
    public async Task GetScreenResolutionAsync_ShouldReturnResolutionAfterInitializationCallback()
    {
        using var provider = new KdePositionProvider(isSupported: true, autoStartTracking: false);

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
    public void StopLoadedScript_ShouldStopBeforeUnload()
    {
        var calls = new List<string>();

        KdePositionProvider.StopLoadedScript(
            "42",
            scriptId =>
            {
                calls.Add($"stop:{scriptId}");
                return Task.CompletedTask;
            },
            scriptId =>
            {
                calls.Add($"unload:{scriptId}");
                return Task.CompletedTask;
            },
            _ => throw new InvalidOperationException("Unexpected error callback."));

        Assert.Equal(new[] { "stop:42", "unload:42" }, calls);
    }

    [LinuxFact]
    public void StopLoadedScript_ShouldSkipCleanupWhenDisposedDuringInitializationBeforeScriptLoads()
    {
        var calls = new List<string>();

        KdePositionProvider.StopLoadedScript(
            scriptId: null,
            scriptId =>
            {
                calls.Add($"stop:{scriptId}");
                return Task.CompletedTask;
            },
            scriptId =>
            {
                calls.Add($"unload:{scriptId}");
                return Task.CompletedTask;
            },
            _ => throw new InvalidOperationException("Unexpected error callback."));

        Assert.Empty(calls);
    }

    [LinuxFact]
    public void CleanupLoadedScriptIfShutdownRequested_ShouldStopAndUnloadWhenCancellationWasRequested()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var calls = new List<string>();

        var cleanedUp = KdePositionProvider.CleanupLoadedScriptIfShutdownRequested(
            disposed: false,
            cancellationToken: cts.Token,
            scriptId: "42",
            scriptId =>
            {
                calls.Add($"stop:{scriptId}");
                return Task.CompletedTask;
            },
            scriptId =>
            {
                calls.Add($"unload:{scriptId}");
                return Task.CompletedTask;
            },
            _ => throw new InvalidOperationException("Unexpected error callback."));

        Assert.True(cleanedUp);
        Assert.Equal(new[] { "stop:42", "unload:42" }, calls);
    }

    [LinuxFact]
    public void CleanupLoadedScriptIfShutdownRequested_ShouldBeNoOpWhenStillRunning()
    {
        using var cts = new CancellationTokenSource();
        var calls = new List<string>();

        var cleanedUp = KdePositionProvider.CleanupLoadedScriptIfShutdownRequested(
            disposed: false,
            cancellationToken: cts.Token,
            scriptId: "42",
            scriptId =>
            {
                calls.Add($"stop:{scriptId}");
                return Task.CompletedTask;
            },
            scriptId =>
            {
                calls.Add($"unload:{scriptId}");
                return Task.CompletedTask;
            },
            _ => throw new InvalidOperationException("Unexpected error callback."));

        Assert.False(cleanedUp);
        Assert.Empty(calls);
    }
}
