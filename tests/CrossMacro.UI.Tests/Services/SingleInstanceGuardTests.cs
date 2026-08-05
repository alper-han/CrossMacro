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

    [Fact]
    public async Task TryAcquire_WhenRuntimeDirectoryChanges_ReturnsNull()
    {
        var mutexName = $"crossmacro-single-instance-{Guid.NewGuid():N}";
        var originalRuntimeDirectory = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        var firstRuntimeDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var secondRuntimeDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", firstRuntimeDirectory);
            Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", secondRuntimeDirectory);
            await AssertSecondAcquisitionFailsAsync(mutexName);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", originalRuntimeDirectory);
            if (Directory.Exists(firstRuntimeDirectory))
            {
                Directory.Delete(firstRuntimeDirectory, recursive: true);
            }

            if (Directory.Exists(secondRuntimeDirectory))
            {
                Directory.Delete(secondRuntimeDirectory, recursive: true);
            }
        }
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

}
