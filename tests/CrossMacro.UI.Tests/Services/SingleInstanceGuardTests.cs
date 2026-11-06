namespace CrossMacro.UI.Tests.Services;


[Collection("EnvironmentVariableSensitive")]
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
        using var environment = new InstanceEnvironment();
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
        using var environment = new InstanceEnvironment();
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
        using var environment = new InstanceEnvironment();
        environment.Set("XDG_RUNTIME_DIR", environment.CreateDirectory(new string('r', 120)));
        var instanceName = $"crossmacro-single-instance-activation-{Guid.NewGuid():N}";
        var firstActivated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondActivated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        environment.Set("XDG_CACHE_HOME", Path.Combine(environment.Root, "cache-one"));
        using var first = SingleInstanceActivationListener.TryStart(instanceName, firstActivated.SetResult);
        environment.Set("XDG_CACHE_HOME", Path.Combine(environment.Root, "cache-two"));
        using var second = SingleInstanceActivationListener.TryStart(instanceName, secondActivated.SetResult);

        Assert.True(SingleInstanceActivationListener.TrySignal(instanceName));
        await secondActivated.Task.WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, CancellationToken.None);
        Assert.False(firstActivated.Task.IsCompleted);

        environment.Set("XDG_CACHE_HOME", Path.Combine(environment.Root, "cache-one"));
        Assert.True(SingleInstanceActivationListener.TrySignal(instanceName));
        await firstActivated.Task.WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, CancellationToken.None);
    }

    [Fact]
    public async Task ActivationListener_WhenSignaled_InvokesActivation()
    {
        var instanceName = $"crossmacro-single-instance-activation-{new string('x', 256)}-{Guid.NewGuid():N}";
        var activated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var listener = SingleInstanceActivationListener.TryStart(instanceName, activated.SetResult);

        Assert.True(SingleInstanceActivationListener.TrySignal(instanceName));
        await activated.Task.WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, CancellationToken.None);
    }

    [LinuxTheory]
    [InlineData("r", 80)]
    [InlineData("ü", 40)]
    [InlineData("r", 120)]
    public async Task ActivationListener_WhenRuntimePathIsLong_InvokesActivation(string segment, int repetitions)
    {

        using var environment = new InstanceEnvironment();
        environment.Set("XDG_CACHE_HOME", Path.Combine(environment.Root, "cache"));
        environment.Set("XDG_RUNTIME_DIR",
            environment.CreateDirectory(string.Concat(Enumerable.Repeat(segment, repetitions))));
        var instanceName = $"crossmacro-activation-{Guid.NewGuid():N}";
        var activated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        using var listener = SingleInstanceActivationListener.TryStart(instanceName, activated.SetResult);
        Assert.NotNull(listener);
        Assert.True(SingleInstanceActivationListener.TrySignal(instanceName));
        await activated.Task.WaitAsync(TimeSpan.FromSeconds(2), TimeProvider.System, CancellationToken.None);
    }

    private static async Task AssertSecondAcquisitionFailsAsync(string mutexName)
    {
        using var releaseOwner = new ManualResetEventSlim(initialState: false);
        var acquisitionCompleted = new TaskCompletionSource<SingleInstanceGuard?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var ownerThread = new Thread(() =>
        {
            SingleInstanceGuard? guard = null;
            try
            {
                guard = SingleInstanceGuard.TryAcquire(mutexName);
                acquisitionCompleted.SetResult(guard);
                releaseOwner.Wait(CancellationToken.None);
            }
#pragma warning disable CA1031 // The helper must surface any owner-thread failure through the awaited task.
            catch (Exception exception)
            {
                acquisitionCompleted.SetException(exception);
            }
#pragma warning restore CA1031
            finally
            {
                guard?.Dispose();
            }
        })
        {
            IsBackground = true,
        };

        ownerThread.Start();

        try
        {
            var first = await acquisitionCompleted.Task;
            Assert.NotNull(first);

            using var second = SingleInstanceGuard.TryAcquire(mutexName);
            Assert.Null(second);
        }
        finally
        {
            releaseOwner.Set();
            ownerThread.Join();
        }
    }

    private sealed class InstanceEnvironment : IDisposable
    {
        private readonly Dictionary<string, string?> _originalValues = [];

        public string Root { get; } = Path.Combine(Path.GetTempPath(), $"crossmacro-instance-{Guid.NewGuid():N}");

        public string CreateDirectory(string name)
        {
            var path = Path.Combine(Root, name);
            _ = Directory.CreateDirectory(path);
            return path;
        }

        public void Set(string name, string value)
        {
            _ = _originalValues.TryAdd(name, Environment.GetEnvironmentVariable(name));
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            foreach (var (name, value) in _originalValues)
            {
                Environment.SetEnvironmentVariable(name, value);
            }

            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

}
