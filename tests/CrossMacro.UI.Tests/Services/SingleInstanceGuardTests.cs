namespace CrossMacro.UI.Tests.Services;

[Collection(TestCollections.EnvironmentVariableSensitive)]
public sealed class SingleInstanceGuardTests
{
    [Fact]
    public void TryAcquire_WithUniqueName_ReturnsGuard()
    {
        var mutexName = $"crossmacro-single-instance-{Guid.NewGuid():N}";

        var first = SingleInstanceGuard.TryAcquire(mutexName);
        Assert.NotNull(first);
        first.Dispose();
    }

    [Fact]
    public void TryAcquire_WhenDisposedTwice_CanBeAcquiredAgain()
    {
        var mutexName = $"crossmacro-single-instance-{Guid.NewGuid():N}";

        var first = SingleInstanceGuard.TryAcquire(mutexName);
        Assert.NotNull(first);
        first.Dispose();
        first.Dispose();

        var second = SingleInstanceGuard.TryAcquire(mutexName);
        Assert.NotNull(second);
        second.Dispose();
    }

    [Fact]
    public async Task TryAcquire_WhenAlreadyHeld_ReturnsNull()
    {
        var mutexName = $"crossmacro-single-instance-{Guid.NewGuid():N}";

        await AssertSecondAcquisitionFailsAsync(mutexName);
    }

    [LinuxFact]
    public void TryAcquire_WhenHomesDifferButCacheIsShared_RejectsSecondInstance()
    {
        using var environment = new EnvironmentVariableScope();
        environment.Set("XDG_CACHE_HOME", Path.Combine(environment.Root, "cache"));
        environment.Set("HOME", environment.CreateDirectory("home-one"));
        var instanceName = $"crossmacro-single-instance-{Guid.NewGuid():N}";
        using var first = SingleInstanceGuard.TryAcquire(instanceName);
        Assert.NotNull(first);

        environment.Set("HOME", environment.CreateDirectory("home-two"));
        using var second = SingleInstanceGuard.TryAcquire(instanceName);

        Assert.Null(second);
    }

    [LinuxFact]
    public void TryAcquire_WhenCachesAreIndependent_AllowsIndependentInstances()
    {
        using var environment = new EnvironmentVariableScope();
        environment.Set("XDG_CACHE_HOME", Path.Combine(environment.Root, "cache-one"));
        var instanceName = $"crossmacro-single-instance-{Guid.NewGuid():N}";
        using var first = SingleInstanceGuard.TryAcquire(instanceName);
        Assert.NotNull(first);

        environment.Set("XDG_CACHE_HOME", Path.Combine(environment.Root, "cache-two"));
        using var second = SingleInstanceGuard.TryAcquire(instanceName);

        Assert.NotNull(second);
    }

    [LinuxFact]
    public async Task ActivationListener_WhenCachesDiffer_ActivatesOnlyMatchingInstance()
    {
        using var environment = new EnvironmentVariableScope();
        environment.Set("XDG_RUNTIME_DIR", environment.CreateDirectory(new string('r', 120)));
        var instanceName = $"crossmacro-single-instance-activation-{Guid.NewGuid():N}";
        var firstActivated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondActivated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        environment.Set("XDG_CACHE_HOME", Path.Combine(environment.Root, "cache-one"));
        using var first = SingleInstanceActivationListener.TryStart(instanceName, firstActivated.SetResult);
        environment.Set("XDG_CACHE_HOME", Path.Combine(environment.Root, "cache-two"));
        using var second = SingleInstanceActivationListener.TryStart(instanceName, secondActivated.SetResult);

        Assert.True(SingleInstanceActivationListener.TrySignal(instanceName));
        await secondActivated.Task.WaitAsync(TestTimeouts.Default, TimeProvider.System, CancellationToken.None);
        Assert.False(firstActivated.Task.IsCompleted);

        environment.Set("XDG_CACHE_HOME", Path.Combine(environment.Root, "cache-one"));
        Assert.True(SingleInstanceActivationListener.TrySignal(instanceName));
        await firstActivated.Task.WaitAsync(TestTimeouts.Default, TimeProvider.System, CancellationToken.None);
    }

    [Fact]
    public async Task ActivationListener_WhenSignaled_InvokesActivation()
    {
        var instanceName = $"crossmacro-single-instance-activation-{new string('x', 256)}-{Guid.NewGuid():N}";
        var activated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var listener = SingleInstanceActivationListener.TryStart(instanceName, activated.SetResult);

        Assert.True(SingleInstanceActivationListener.TrySignal(instanceName));
        await activated.Task.WaitAsync(TestTimeouts.Default, TimeProvider.System, CancellationToken.None);
    }

    [LinuxTheory]
    [InlineData("r", 80)]
    [InlineData("ü", 40)]
    [InlineData("r", 120)]
    public async Task ActivationListener_WhenRuntimePathIsLong_InvokesActivation(string segment, int repetitions)
    {

        using var environment = new EnvironmentVariableScope();
        environment.Set("XDG_CACHE_HOME", Path.Combine(environment.Root, "cache"));
        environment.Set("XDG_RUNTIME_DIR",
            environment.CreateDirectory(string.Concat(Enumerable.Repeat(segment, repetitions))));
        var instanceName = $"crossmacro-activation-{Guid.NewGuid():N}";
        var activated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var listener = SingleInstanceActivationListener.TryStart(instanceName, activated.SetResult);
        Assert.NotNull(listener);
        Assert.True(SingleInstanceActivationListener.TrySignal(instanceName));
        await activated.Task.WaitAsync(TestTimeouts.Default, TimeProvider.System, CancellationToken.None);
    }

    [LinuxFact]
    public async Task ActivationListener_AfterDisposal_CanListenAgainOnLongRuntimePath()
    {
        using var environment = new EnvironmentVariableScope();
        environment.Set("XDG_CACHE_HOME", Path.Combine(environment.Root, "cache"));
        environment.Set("XDG_RUNTIME_DIR", environment.CreateDirectory(new string('r', 120)));
        var instanceName = $"crossmacro-activation-{Guid.NewGuid():N}";
        var firstActivated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using (var first = SingleInstanceActivationListener.TryStart(instanceName, firstActivated.SetResult))
        {
            Assert.NotNull(first);
            Assert.True(SingleInstanceActivationListener.TrySignal(instanceName));
            await firstActivated.Task.WaitAsync(TestTimeouts.Default, TimeProvider.System);
        }

        var secondActivated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var second = SingleInstanceActivationListener.TryStart(instanceName, secondActivated.SetResult);
        Assert.NotNull(second);
        Assert.True(SingleInstanceActivationListener.TrySignal(instanceName));
        await secondActivated.Task.WaitAsync(TestTimeouts.Default, TimeProvider.System);
    }

    private static async Task AssertSecondAcquisitionFailsAsync(string mutexName)
    {
        using var releaseOwner = new ManualResetEventSlim(initialState: false);
        var acquisitionCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        // A dedicated thread keeps the mutex owner alive; any owner failure faults the returned task.
        var owner = Task.Factory.StartNew(
            () =>
            {
                using var guard = SingleInstanceGuard.TryAcquire(mutexName);
                acquisitionCompleted.SetResult(guard is not null);
                releaseOwner.Wait(CancellationToken.None);
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        try
        {
            if (await Task.WhenAny(acquisitionCompleted.Task, owner) == owner)
            {
                await owner;
            }

            Assert.True(await acquisitionCompleted.Task);

            using var second = SingleInstanceGuard.TryAcquire(mutexName);
            Assert.Null(second);
        }
        finally
        {
            releaseOwner.Set();
            await owner;
        }
    }
}
