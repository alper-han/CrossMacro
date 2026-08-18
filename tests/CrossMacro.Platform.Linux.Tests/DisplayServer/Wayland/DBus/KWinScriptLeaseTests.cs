using System.Globalization;

namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland.DBus;

public sealed class KWinScriptLeaseTests
{
    private const string PluginName = "io.github.alper_han.crossmacro.test";
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(2);

    [LinuxFact]
    public async Task LoadAndRunAsync_Success_PreservesProtocolOrderAndHandle()
    {
        var calls = new List<string>();
        await using var lease = CreateLease(
            loadScriptAsync: (path, name) =>
            {
                calls.Add($"load:{path}:{name}");
                return Task.FromResult(42);
            },
            runScriptAsync: id =>
            {
                calls.Add($"run:{id.ToString(CultureInfo.InvariantCulture)}");
                return Task.CompletedTask;
            },
            unloadScriptAsync: name =>
            {
                calls.Add($"unload:{name}");
                return Task.CompletedTask;
            });

        await lease.LoadAndRunAsync("/tmp/tracker.js", CancellationToken.None);

        Assert.Equal(new KWinScriptHandle(42, PluginName), lease.Handle);
        await lease.DisposeAsync();
        Assert.Null(lease.Handle);
        Assert.Equal(
            ["load:/tmp/tracker.js:io.github.alper_han.crossmacro.test", "run:42", "unload:io.github.alper_han.crossmacro.test"],
            calls);
    }

    [LinuxFact]
    public async Task LoadAndRunAsync_WhenLoadIsCanceled_UnloadsByPluginNameOnce()
    {
        var loadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var loadCompletion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var unloadedNames = new ConcurrentQueue<string>();
        using var cancellation = new CancellationTokenSource();
        await using var lease = CreateLease(
            loadScriptAsync: (_, _) =>
            {
                loadStarted.SetResult();
                return loadCompletion.Task;
            },
            unloadScriptAsync: name =>
            {
                unloadedNames.Enqueue(name);
                return Task.CompletedTask;
            });

        var operation = lease.LoadAndRunAsync("/tmp/tracker.js", cancellation.Token);
        await loadStarted.Task.WaitAsync(TestTimeout, TimeProvider.System, CancellationToken.None);
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => operation.WaitAsync(TestTimeout, TimeProvider.System, CancellationToken.None));
        Assert.Null(lease.Handle);
        Assert.Collection(unloadedNames, name => Assert.Equal(PluginName, name));

        loadCompletion.SetException(new IOException("late load failure"));
        await lease.DisposeAsync();
        Assert.Collection(unloadedNames, name => Assert.Equal(PluginName, name));
    }

    [LinuxFact]
    public async Task LoadAndRunAsync_WhenRunIsCanceled_UnloadsLoadedScriptOnce()
    {
        var runStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var unloadCount = 0;
        using var cancellation = new CancellationTokenSource();
        await using var lease = CreateLease(
            runScriptAsync: _ =>
            {
                runStarted.SetResult();
                return runCompletion.Task;
            },
            unloadScriptAsync: name =>
            {
                _ = Interlocked.Increment(ref unloadCount);
                return Task.CompletedTask;
            });

        var operation = lease.LoadAndRunAsync("/tmp/tracker.js", cancellation.Token);
        await runStarted.Task.WaitAsync(TestTimeout, TimeProvider.System, CancellationToken.None);
        await cancellation.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => operation.WaitAsync(TestTimeout, TimeProvider.System, CancellationToken.None));
        Assert.Null(lease.Handle);
        Assert.Equal(1, Volatile.Read(ref unloadCount));

        runCompletion.SetException(new IOException("late run failure"));
        await lease.DisposeAsync();
        Assert.Equal(1, Volatile.Read(ref unloadCount));
    }

    [LinuxFact]
    public async Task LoadAndRunAsync_WhenLoadTimesOut_UnloadsAndThrowsTimeout()
    {
        var loadCompletion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var unloadedNames = new ConcurrentQueue<string>();
        await using var lease = CreateLease(
            loadScriptAsync: (_, _) => loadCompletion.Task,
            unloadScriptAsync: name =>
            {
                unloadedNames.Enqueue(name);
                return Task.CompletedTask;
            },
            operationTimeout: TimeSpan.FromMilliseconds(30));

        _ = await Assert.ThrowsAsync<TimeoutException>(
            () => lease.LoadAndRunAsync("/tmp/tracker.js", CancellationToken.None)
                .WaitAsync(TestTimeout, TimeProvider.System, CancellationToken.None));

        Assert.Collection(unloadedNames, name => Assert.Equal(PluginName, name));
        loadCompletion.SetResult(42);
    }

    [LinuxFact]
    public async Task DisposeAsync_WhenCleanupTimesOut_ReportsTimeoutAndCompletes()
    {
        var unloadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var unloadCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanupErrors = new ConcurrentQueue<Exception>();
        await using var lease = CreateLease(
            unloadScriptAsync: _ =>
            {
                unloadStarted.SetResult();
                return unloadCompletion.Task;
            },
            cleanupTimeout: TimeSpan.FromMilliseconds(30),
            onCleanupError: cleanupErrors.Enqueue);
        await lease.LoadAndRunAsync("/tmp/tracker.js", CancellationToken.None);

        await lease.DisposeAsync().AsTask()
            .WaitAsync(TestTimeout, TimeProvider.System, CancellationToken.None);

        Assert.True(unloadStarted.Task.IsCompletedSuccessfully);
        _ = Assert.IsType<TimeoutException>(Assert.Single(cleanupErrors));
        unloadCompletion.SetException(new IOException("late cleanup failure"));
    }

    [LinuxFact]
    public async Task DisposeAsync_WhenCleanupFails_ReportsFailureWithoutThrowing()
    {
        var expected = new IOException("unload failed");
        var cleanupErrors = new ConcurrentQueue<Exception>();
        await using var lease = CreateLease(
            unloadScriptAsync: _ => Task.FromException(expected),
            onCleanupError: cleanupErrors.Enqueue);
        await lease.LoadAndRunAsync("/tmp/tracker.js", CancellationToken.None);

        var exception = await Record.ExceptionAsync(() => lease.DisposeAsync().AsTask());

        Assert.Null(exception);
        Assert.Same(expected, Assert.Single(cleanupErrors));
    }

    [LinuxFact]
    public async Task DisposeAsync_WhenLoadWasNotAttempted_DoesNotRequestCleanup()
    {
        var unloadCount = 0;
        await using var lease = CreateLease(unloadScriptAsync: name =>
        {
            _ = Interlocked.Increment(ref unloadCount);
            return Task.CompletedTask;
        });

        await lease.DisposeAsync();

        Assert.Equal(0, Volatile.Read(ref unloadCount));
    }

    [LinuxFact]
    public async Task DisposeAsync_WhenCleanupDiagnosticThrows_DoesNotPropagateDiagnosticFailure()
    {
        await using var lease = CreateLease(
            unloadScriptAsync: _ => Task.FromException(new IOException("unload failed")),
            onCleanupError: _ => throw new InvalidOperationException("diagnostic failure"));
        await lease.LoadAndRunAsync("/tmp/tracker.js", CancellationToken.None);

        var exception = await Record.ExceptionAsync(() => lease.DisposeAsync().AsTask());

        Assert.Null(exception);
    }

    [LinuxFact]
    public async Task DisposeAsync_WhenCalledConcurrently_UnloadsExactlyOnce()
    {
        var unloadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var unloadCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var unloadCount = 0;
        await using var lease = CreateLease(unloadScriptAsync: name =>
        {
            _ = Interlocked.Increment(ref unloadCount);
            unloadStarted.SetResult();
            return unloadCompletion.Task;
        });
        await lease.LoadAndRunAsync("/tmp/tracker.js", CancellationToken.None);

        var disposals = Enumerable.Range(0, 16)
            .Select(_ => lease.DisposeAsync().AsTask())
            .ToArray();
        await unloadStarted.Task.WaitAsync(TestTimeout, TimeProvider.System, CancellationToken.None);
        Assert.Equal(1, Volatile.Read(ref unloadCount));

        unloadCompletion.SetResult();
        await Task.WhenAll(disposals)
            .WaitAsync(TestTimeout, TimeProvider.System, CancellationToken.None);
        await lease.DisposeAsync();

        Assert.Equal(1, Volatile.Read(ref unloadCount));
        Assert.Null(lease.Handle);
    }

    private static KWinScriptLease CreateLease(
        Func<string, string, Task<int>>? loadScriptAsync = null,
        Func<int, Task>? runScriptAsync = null,
        Func<string, Task>? unloadScriptAsync = null,
        TimeSpan? operationTimeout = null,
        TimeSpan? cleanupTimeout = null,
        Action<Exception>? onCleanupError = null)
    {
        return new KWinScriptLease(
            PluginName,
            loadScriptAsync ?? (static (_, _) => Task.FromResult(42)),
            runScriptAsync ?? (static _ => Task.CompletedTask),
            unloadScriptAsync ?? (static _ => Task.CompletedTask),
            operationTimeout ?? TimeSpan.FromSeconds(1),
            cleanupTimeout ?? TimeSpan.FromSeconds(1),
            onCleanupError);
    }
}
