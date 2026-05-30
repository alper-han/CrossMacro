using System.Reflection;
using System.Runtime.InteropServices;
using CrossMacro.Platform.MacOS.Native;
using Xunit;

namespace CrossMacro.Platform.MacOS.Tests.Native;

public class CoreGraphicsPermissionAccessTests
{
    [Fact]
    public void CGEventTapOptions_ListenOnly_MatchesNativeValue()
    {
        Assert.Equal(1u, (uint)CoreGraphics.CGEventTapOptions.ListenOnly);
    }

    [Theory]
    [InlineData(nameof(CoreGraphics.CGPreflightListenEventAccess))]
    [InlineData(nameof(CoreGraphics.CGRequestListenEventAccess))]
    [InlineData(nameof(CoreGraphics.CGPreflightPostEventAccess))]
    [InlineData(nameof(CoreGraphics.CGRequestPostEventAccess))]
    public void PermissionAccessWrappers_ReturnManagedBoolean(string methodName)
    {
        MethodInfo method = typeof(CoreGraphics).GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)!;

        Assert.Equal(typeof(bool), method.ReturnType);
        Assert.Empty(method.GetParameters());
    }

    [Theory]
    [InlineData(nameof(CoreGraphics.IsCGPreflightListenEventAccessAvailable))]
    [InlineData(nameof(CoreGraphics.IsCGRequestListenEventAccessAvailable))]
    [InlineData(nameof(CoreGraphics.IsCGPreflightPostEventAccessAvailable))]
    [InlineData(nameof(CoreGraphics.IsCGRequestPostEventAccessAvailable))]
    public void PermissionAccessAvailabilityWrappers_ReturnManagedBoolean(string methodName)
    {
        MethodInfo method = typeof(CoreGraphics).GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)!;

        Assert.Equal(typeof(bool), method.ReturnType);
        Assert.Empty(method.GetParameters());
    }

    [Fact]
    public void PermissionAccessNativeImports_AreResolvedDynamicallyInsteadOfDirectDllImport()
    {
        var directPermissionImports = typeof(CoreGraphics)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(method => method.Name.Contains("EventAccess", StringComparison.Ordinal))
            .Where(method => method.GetCustomAttribute<DllImportAttribute>() is not null)
            .ToArray();

        Assert.Empty(directPermissionImports);
    }
}
