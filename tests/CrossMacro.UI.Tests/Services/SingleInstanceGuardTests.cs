namespace CrossMacro.UI.Tests.Services;


public class SingleInstanceGuardTests
{
    [Fact]
    public void TryAcquire_WithUniqueName_ReturnsGuard()
    {
        var (_, tryAcquireMethod) = GetSingleInstanceGuardMembers();
        var mutexName = $"crossmacro-single-instance-{Guid.NewGuid():N}";

        var first = tryAcquireMethod.Invoke(null, [mutexName]);
        Assert.NotNull(first);
        ((IDisposable)first!).Dispose();
    }

    [Fact]
    public async Task TryAcquire_WhenReleased_CanBeAcquiredAgain()
    {
        var (_, tryAcquireMethod) = GetSingleInstanceGuardMembers();
        var mutexName = $"crossmacro-single-instance-{Guid.NewGuid():N}";

        var first = tryAcquireMethod.Invoke(null, [mutexName]);
        Assert.NotNull(first);
        ((IDisposable)first!).Dispose();

        var second = await Task.Run(() => tryAcquireMethod.Invoke(null, [mutexName]));
        Assert.NotNull(second);
        ((IDisposable)second!).Dispose();
    }

    [Fact]
    public void TryAcquire_WhenAlreadyHeld_ReturnsNull()
    {
        var (_, tryAcquireMethod) = GetSingleInstanceGuardMembers();
        var mutexName = $"crossmacro-single-instance-{Guid.NewGuid():N}";

        using var first = (IDisposable)tryAcquireMethod.Invoke(null, [mutexName])!;

        var second = tryAcquireMethod.Invoke(null, [mutexName]);

        Assert.Null(second);
    }

    [Fact]
    public void Dispose_WhenCalledTwice_DoesNotThrow()
    {
        var (_, tryAcquireMethod) = GetSingleInstanceGuardMembers();
        var mutexName = $"crossmacro-single-instance-{Guid.NewGuid():N}";
        var guard = (IDisposable)tryAcquireMethod.Invoke(null, [mutexName])!;

        guard.Dispose();
        guard.Dispose();
    }

    [Fact]
    public void TryAcquire_WhenRuntimeDirectoryChanges_ReturnsNull()
    {
        var (_, tryAcquireMethod) = GetSingleInstanceGuardMembers();
        var mutexName = $"crossmacro-single-instance-{Guid.NewGuid():N}";
        var originalRuntimeDirectory = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        var firstRuntimeDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var secondRuntimeDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        IDisposable? first = null;
        IDisposable? second = null;

        try
        {
            Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", firstRuntimeDirectory);
            first = (IDisposable)tryAcquireMethod.Invoke(null, [mutexName])!;

            Environment.SetEnvironmentVariable("XDG_RUNTIME_DIR", secondRuntimeDirectory);
            second = tryAcquireMethod.Invoke(null, [mutexName]) as IDisposable;

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

    private static (Type GuardType, MethodInfo TryAcquireMethod) GetSingleInstanceGuardMembers()
    {
        var assembly = typeof(DialogService).Assembly;
        var guardType = assembly.GetType("CrossMacro.UI.SingleInstanceGuard", throwOnError: true)!;
        var tryAcquireMethod = guardType.GetMethod("TryAcquire", BindingFlags.Public | BindingFlags.Static)!;
        return (guardType, tryAcquireMethod);
    }
}
