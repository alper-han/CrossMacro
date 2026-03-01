namespace CrossMacro.UI.Tests.Services;

using CrossMacro.Core.Services;
using CrossMacro.UI.Services;

public sealed class TrayIconServiceTests
{
    [Fact]
    public void IsTraySupported_WhenRuntimeContextIsFlatpak_ReturnsFalse()
    {
        var runtimeContext = new FakeRuntimeContext { IsFlatpak = true };
        Assert.False(TrayIconService.IsTraySupported(runtimeContext));
    }

    [Fact]
    public void IsTraySupported_WhenRuntimeContextIsNotFlatpak_ReturnsTrue()
    {
        var runtimeContext = new FakeRuntimeContext { IsFlatpak = false };
        Assert.True(TrayIconService.IsTraySupported(runtimeContext));
    }

    private sealed class FakeRuntimeContext : IRuntimeContext
    {
        public bool IsLinux => true;
        public bool IsWindows => false;
        public bool IsMacOS => false;
        public bool IsFlatpak { get; set; }
        public string? SessionType => "wayland";
    }
}
