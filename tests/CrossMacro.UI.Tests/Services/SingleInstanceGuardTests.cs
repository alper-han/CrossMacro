namespace CrossMacro.UI.Tests.Services;


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
    public void TryAcquire_WhenAlreadyHeld_ReturnsNull()
    {
        var mutexName = $"crossmacro-single-instance-{Guid.NewGuid():N}";

        using var first = SingleInstanceGuard.TryAcquire(mutexName);

        var second = SingleInstanceGuard.TryAcquire(mutexName);

        Assert.Null(second);
    }

    [Fact]
    public void TryAcquire_WhenRuntimeDirectoryChanges_ReturnsNull()
    {
        var mutexName = $"crossmacro-single-instance-{Guid.NewGuid():N}";
        var originalRuntimeDirectory = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        var firstRuntimeDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var secondRuntimeDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        SingleInstanceGuard? first = null;
        SingleInstanceGuard? second = null;

        try
        {
            Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", firstRuntimeDirectory);
            first = SingleInstanceGuard.TryAcquire(mutexName);

            Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", secondRuntimeDirectory);
            second = SingleInstanceGuard.TryAcquire(mutexName);

            Assert.Null(second);
        }
        finally
        {
            second?.Dispose();
            first?.Dispose();
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

}
