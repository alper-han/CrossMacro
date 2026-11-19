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
        var provider = Create(compositor);

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
        var provider = Create(CompositorType.Hyprland, windowButtons: value);

        Assert.Equal(expected, provider.WindowManagerHandlesCloseButton);
    }

    [Fact]
    public void WindowManagerHandlesCloseButton_ShouldUseDefault_WhenOverrideMissing()
    {
        var hyprlandProvider = Create(CompositorType.Hyprland);
        var swayProvider = Create(CompositorType.Sway);
        var niriProvider = Create(CompositorType.Niri);
        var x11Provider = Create(CompositorType.X11);

        Assert.True(hyprlandProvider.WindowManagerHandlesCloseButton);
        Assert.True(swayProvider.WindowManagerHandlesCloseButton);
        Assert.True(niriProvider.WindowManagerHandlesCloseButton);
        Assert.False(x11Provider.WindowManagerHandlesCloseButton);
    }

    private static LinuxEnvironmentInfoProvider Create(CompositorType compositor, string? windowButtons = null)
    {
        var detector = Substitute.For<ILinuxEnvironmentDetector>();
        _ = detector.DetectedCompositor.Returns(compositor);
        var variables = Substitute.For<ILinuxEnvironmentVariables>();
        _ = variables.CaptureSnapshot().Returns(default(LinuxEnvironmentSnapshot) with { WindowButtons = windowButtons });
        return new LinuxEnvironmentInfoProvider(detector, variables);
    }
}
