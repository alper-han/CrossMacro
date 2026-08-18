namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class KdeWindowManagerTests
{
    [LinuxFact]
    public void BuildOneShotScriptContent_ShouldRouteCallbackToConnectionSpecificDestination()
    {
        const string template = "callDBus('__SERVICE_NAME__', '__OBJECT_PATH__', '__INTERFACE__', 'ReportWindowData', '__CORRELATION_ID__');";

        var first = KdeWindowManager.BuildOneShotScriptContent(template, "first", ":1.42");
        var second = KdeWindowManager.BuildOneShotScriptContent(template, "second", ":1.43");

        Assert.Contains("callDBus(':1.42'", first, StringComparison.Ordinal);
        Assert.Contains("'first'", first, StringComparison.Ordinal);
        Assert.DoesNotContain(":1.43", first, StringComparison.Ordinal);
        Assert.Contains("callDBus(':1.43'", second, StringComparison.Ordinal);
        Assert.Contains("'second'", second, StringComparison.Ordinal);
        Assert.DoesNotContain(":1.42", second, StringComparison.Ordinal);
    }

    [LinuxFact]
    public void CreateOneShotPluginName_ShouldPreserveCorrelationIdentity()
    {
        var first = KdeWindowManager.CreateOneShotPluginName("first");
        var second = KdeWindowManager.CreateOneShotPluginName("second");

        Assert.Equal("crossmacro-window-first", first);
        Assert.Equal("crossmacro-window-second", second);
        Assert.False(string.Equals(first, second, StringComparison.Ordinal));
    }

    [LinuxFact]
    public async Task AwaitCallbackAsync_ShouldReturnOriginalCallbackPayload()
    {
        const string expected = "{\"name\":\"Desktop 1\"}";

        var result = await KdeWindowManager.AwaitCallbackAsync(
            Task.FromResult(expected),
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        Assert.Equal(expected, result);
    }

    [LinuxFact]
    public async Task AwaitCallbackAsync_WhenDeadlineExpires_ShouldReturnNull()
    {
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        var result = await KdeWindowManager.AwaitCallbackAsync(
            pending.Task,
            TimeSpan.Zero,
            CancellationToken.None);

        Assert.Null(result);
    }

    [LinuxFact]
    public async Task AwaitCallbackAsync_WhenCallerCancels_ShouldPropagateCancellation()
    {
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await TestAssertions.ThrowsAnyAsync<OperationCanceledException>(() =>
            KdeWindowManager.AwaitCallbackAsync(
                pending.Task,
                TimeSpan.FromSeconds(1),
                cancellation.Token));
    }

    [LinuxFact]
    public async Task CleanupOneShotArtifacts_RemovesPendingRequestAndTempFile()
    {
        using var manager = new KdeWindowManagerTestScope();
        var correlationId = Guid.NewGuid().ToString("N");
        var tempFile = Path.Combine(Path.GetTempPath(), $"crossmacro-kwin-{Guid.NewGuid():N}.js");
        await File.WriteAllTextAsync(tempFile, "script", CancellationToken.None);

        manager.AddPendingRequest(correlationId);
        manager.Cleanup(tempFile, expectsCallback: true, correlationId);

        Assert.False(File.Exists(tempFile));
        Assert.False(manager.HasPendingRequest(correlationId));
    }

    [LinuxFact]
    public async Task DisposeAsync_WaitsForInFlightOperationBeforeDisposingConnection()
    {
        await using var manager = new KdeWindowManagerTestScope();
        var operationGate = manager.OperationGate;
        Assert.True(await operationGate.WaitAsync(TimeSpan.FromSeconds(2), CancellationToken.None));

        var disposeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposeTask = Task.Run(async () =>
        {
            disposeStarted.SetResult();
            await manager.DisposeAsync();
        }, CancellationToken.None);
        await disposeStarted.Task.WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, CancellationToken.None);
        Assert.False(disposeTask.IsCompleted);
        Assert.True(manager.IsDisposed);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => manager.GetActiveWindowAsync());

        _ = operationGate.Release();
        await disposeTask.WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, CancellationToken.None);
        Assert.True(manager.IsDisposed);
    }

    private sealed class KdeWindowManagerTestScope : IAsyncDisposable, IDisposable
    {
        private readonly KdeWindowManager _manager = new();
        private readonly FieldInfo _pendingRequestsField = typeof(KdeWindowManager).GetField(
            "_pendingRequests", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private readonly MethodInfo _cleanupMethod = typeof(KdeWindowManager).GetMethod(
            "CleanupOneShotArtifacts", BindingFlags.Instance | BindingFlags.NonPublic)!;

        public SemaphoreSlim OperationGate => (SemaphoreSlim)typeof(KdeWindowManager)
            .GetField("_operationLock", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(_manager)!;

        public bool IsDisposed => _manager.IsDisposed;

        public Task<WindowInfo?> GetActiveWindowAsync() => _manager.GetActiveWindowAsync();

        public void AddPendingRequest(string correlationId)
        {
            var pending = (ConcurrentDictionary<string, TaskCompletionSource<string>>)_pendingRequestsField.GetValue(_manager)!;
            pending[correlationId] = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public bool HasPendingRequest(string correlationId)
        {
            var pending = (ConcurrentDictionary<string, TaskCompletionSource<string>>)_pendingRequestsField.GetValue(_manager)!;
            return pending.ContainsKey(correlationId);
        }

        public void Cleanup(string tempFile, bool expectsCallback, string correlationId)
            => _ = _cleanupMethod.Invoke(_manager, [tempFile, expectsCallback, correlationId]);

        public ValueTask DisposeAsync() => _manager.DisposeAsync();

        public void Dispose() => _manager.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
