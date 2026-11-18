namespace CrossMacro.Platform.Linux.Tests.Services;


public sealed class LinuxEnvironmentInfoProviderTests
{
    [Theory]
    [InlineData(CompositorType.X11, DisplayEnvironment.LinuxX11, false)]
    [InlineData(CompositorType.Hyprland, DisplayEnvironment.LinuxHyprland, true)]
    [InlineData(CompositorType.Wayfire, DisplayEnvironment.LinuxWayfire, false)]
    [InlineData(CompositorType.Niri, DisplayEnvironment.LinuxWayland, true)]
    [InlineData(CompositorType.Cosmic, DisplayEnvironment.LinuxWayland, false)]
    [InlineData(CompositorType.Sway, DisplayEnvironment.LinuxWayland, true)]
    [InlineData(CompositorType.Kde, DisplayEnvironment.LinuxKDE, false)]
    [InlineData(CompositorType.Gnome, DisplayEnvironment.LinuxGnome, false)]
    [InlineData(CompositorType.Other, DisplayEnvironment.LinuxWayland, false)]
    [InlineData(CompositorType.Unknown, DisplayEnvironment.Unknown, false)]
    public void CurrentEnvironment_ShouldMapFromCompositor(CompositorType compositor, DisplayEnvironment expectedEnvironment, bool expectedHandlesCloseButton)
    {
        var provider = new LinuxEnvironmentInfoProvider(compositor);

        Assert.Equal(expectedEnvironment, provider.CurrentEnvironment);
        Assert.Equal(expectedHandlesCloseButton, provider.WindowManagerHandlesCloseButton);
    }

    [Theory]
    [InlineData("show", false)]
    [InlineData("1", false)]
    [InlineData("true", false)]
    [InlineData("yes", false)]
    [InlineData("on", false)]
    [InlineData("hide", true)]
    [InlineData("0", true)]
    [InlineData("false", true)]
    [InlineData("no", true)]
    [InlineData("off", true)]
    [InlineData("auto", true)]
    [InlineData("unknown", true)]
    public void WindowManagerHandlesCloseButton_ShouldRespectEnvironmentOverride(string value, bool expected)
    {
        var provider = new LinuxEnvironmentInfoProvider(
            CompositorType.Hyprland,
            key => key is "CROSSMACRO_WINDOW_BUTTONS" ? value : null);

        Assert.Equal(expected, provider.WindowManagerHandlesCloseButton);
    }

    [Fact]
    public void WindowManagerHandlesCloseButton_ShouldUseDefault_WhenOverrideMissing()
    {
        var hyprlandProvider = new LinuxEnvironmentInfoProvider(
            CompositorType.Hyprland,
            _ => null);
        var swayProvider = new LinuxEnvironmentInfoProvider(
            CompositorType.Sway,
            _ => null);
        var niriProvider = new LinuxEnvironmentInfoProvider(
            CompositorType.Niri,
            _ => null);
        var x11Provider = new LinuxEnvironmentInfoProvider(
            CompositorType.X11,
            _ => null);

        Assert.True(hyprlandProvider.WindowManagerHandlesCloseButton);
        Assert.True(swayProvider.WindowManagerHandlesCloseButton);
        Assert.True(niriProvider.WindowManagerHandlesCloseButton);
        Assert.False(x11Provider.WindowManagerHandlesCloseButton);
    }
}
