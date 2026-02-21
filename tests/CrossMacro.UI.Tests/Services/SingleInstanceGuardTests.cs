namespace CrossMacro.UI.Tests.Services;

using System;
using System.Reflection;
using System.Threading.Tasks;
using CrossMacro.UI.Services;

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

    private static (Type GuardType, MethodInfo TryAcquireMethod) GetSingleInstanceGuardMembers()
    {
        var assembly = typeof(DialogService).Assembly;
        var guardType = assembly.GetType("CrossMacro.UI.SingleInstanceGuard", throwOnError: true)!;
        var tryAcquireMethod = guardType.GetMethod("TryAcquire", BindingFlags.Public | BindingFlags.Static)!;
        return (guardType, tryAcquireMethod);
    }
}
