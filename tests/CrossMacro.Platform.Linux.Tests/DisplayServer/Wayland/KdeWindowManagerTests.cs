namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class KdeWindowManagerTests
{
    [LinuxFact]
    public async Task CleanupOneShotScriptAsync_RemovesPendingRequestAndTempFile()
    {
        using var manager = new KdeWindowManagerTestScope();
        var correlationId = Guid.NewGuid().ToString("N");
        var tempFile = Path.Combine(Path.GetTempPath(), $"crossmacro-kwin-{Guid.NewGuid():N}.js");
        await File.WriteAllTextAsync(tempFile, "script");

        manager.AddPendingRequest(correlationId);
        await manager.CleanupAsync(scriptId: null, tempFile, expectsCallback: true, correlationId);

        Assert.False(File.Exists(tempFile));
        Assert.False(manager.HasPendingRequest(correlationId));
    }

    [LinuxFact]
    public async Task DisposeAsync_WaitsForInFlightOperationBeforeDisposingConnection()
    {
        await using var manager = new KdeWindowManagerTestScope();
        var operationGate = manager.OperationGate;
        Assert.True(operationGate.Wait(TimeSpan.FromSeconds(2)));

        var disposeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposeTask = Task.Run(async () =>
        {
            disposeStarted.SetResult();
            await manager.DisposeAsync();
        });
        await disposeStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(disposeTask.IsCompleted);

        _ = operationGate.Release();
        await disposeTask;
        Assert.True(manager.IsDisposed);
    }

    private sealed class KdeWindowManagerTestScope : IAsyncDisposable, IDisposable
    {
        private readonly KdeWindowManager _manager = new();
        private readonly FieldInfo _pendingRequestsField = typeof(KdeWindowManager).GetField(
            "_pendingRequests", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private readonly MethodInfo _cleanupMethod = typeof(KdeWindowManager).GetMethod(
            "CleanupOneShotScriptAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        public SemaphoreSlim OperationGate => (SemaphoreSlim)typeof(KdeWindowManager)
            .GetField("_operationLock", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(_manager)!;

        public bool IsDisposed => (bool)typeof(KdeWindowManager)
            .GetField("_disposed", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(_manager)!;

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

        public async Task CleanupAsync(string? scriptId, string tempFile, bool expectsCallback, string correlationId)
        {
            var task = (Task)_cleanupMethod.Invoke(_manager, [scriptId, tempFile, expectsCallback, correlationId])!;
            await task;
        }

        public ValueTask DisposeAsync() => _manager.DisposeAsync();

        public void Dispose() => _manager.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
