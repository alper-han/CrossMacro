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

    [Fact]
    public void IOKitInputMonitoringConstants_MatchNativeValues()
    {
        Assert.Equal(0u, (uint)IOKit.IOHIDRequestType.PostEvent);
        Assert.Equal(1u, (uint)IOKit.IOHIDRequestType.ListenEvent);
        Assert.Equal(0u, (uint)IOKit.IOHIDAccessType.Granted);
        Assert.Equal(1u, (uint)IOKit.IOHIDAccessType.Denied);
        Assert.Equal(2u, (uint)IOKit.IOHIDAccessType.Unknown);
    }

    [Theory]
    [InlineData(nameof(CoreGraphics.CGPreflightListenEventAccess))]
    [InlineData(nameof(CoreGraphics.CGRequestListenEventAccess))]
    [InlineData(nameof(CoreGraphics.CGPreflightPostEventAccess))]
    [InlineData(nameof(CoreGraphics.CGRequestPostEventAccess))]
    [InlineData(nameof(CoreGraphics.CGPreflightScreenCaptureAccess))]
    [InlineData(nameof(CoreGraphics.CGRequestScreenCaptureAccess))]
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
    [InlineData(nameof(CoreGraphics.IsCGPreflightScreenCaptureAccessAvailable))]
    [InlineData(nameof(CoreGraphics.IsCGRequestScreenCaptureAccessAvailable))]
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
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(method => method.Name.Contains("Access", StringComparison.Ordinal))
            .Where(method => method.GetCustomAttribute<DllImportAttribute>() is not null)
            .ToArray();

        Assert.Empty(directPermissionImports);
    }

    [Fact]
    public void ScreenCaptureNativeImports_HaveExpectedManagedShapes()
    {
        Assert.Equal(typeof(CoreGraphics.CGError), typeof(CoreGraphics).GetMethod(nameof(CoreGraphics.CGGetOnlineDisplayList))!.ReturnType);
        Assert.Equal(typeof(CoreGraphics.CGError), typeof(CoreGraphics).GetMethod(nameof(CoreGraphics.CGGetActiveDisplayList))!.ReturnType);
        Assert.Equal(typeof(CoreGraphics.CGError), typeof(CoreGraphics).GetMethod(nameof(CoreGraphics.CGGetDisplaysWithRect))!.ReturnType);
        Assert.Equal(typeof(IntPtr), typeof(CoreGraphics).GetMethod(nameof(CoreGraphics.CGDisplayCreateImageForRect))!.ReturnType);
        Assert.Equal(typeof(void), typeof(CoreGraphics).GetMethod(nameof(CoreGraphics.CGImageRelease))!.ReturnType);
        Assert.Equal(typeof(nuint), typeof(CoreGraphics).GetMethod(nameof(CoreGraphics.CGImageGetWidth))!.ReturnType);
        Assert.Equal(typeof(nuint), typeof(CoreGraphics).GetMethod(nameof(CoreGraphics.CGImageGetBytesPerRow))!.ReturnType);
        Assert.Equal(typeof(nint), typeof(CoreFoundation).GetMethod(nameof(CoreFoundation.CFDataGetLength))!.ReturnType);
    }

    [Fact]
    public void ScreenCaptureConstants_MatchCoreGraphicsValues()
    {
        Assert.Equal(0, (int)CoreGraphics.CGError.Success);
        Assert.Equal(0x1Fu, CoreGraphics.kCGBitmapAlphaInfoMask);
        Assert.Equal(0x7000u, CoreGraphics.kCGBitmapByteOrderMask);
        Assert.Equal(0x2000u, CoreGraphics.kCGBitmapByteOrder32Little);
        Assert.Equal(0x4000u, CoreGraphics.kCGBitmapByteOrder32Big);
    }
}
