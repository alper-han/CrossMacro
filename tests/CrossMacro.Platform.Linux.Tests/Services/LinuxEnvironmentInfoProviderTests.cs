namespace CrossMacro.Platform.Linux.Tests.Services;

using CrossMacro.Core.Services;
using CrossMacro.Platform.Linux.DisplayServer;
using CrossMacro.Platform.Linux.Services;

public class LinuxEnvironmentInfoProviderTests
{
    [Theory]
    [InlineData(CompositorType.X11, DisplayEnvironment.LinuxX11, false)]
    [InlineData(CompositorType.HYPRLAND, DisplayEnvironment.LinuxHyprland, true)]
    [InlineData(CompositorType.KDE, DisplayEnvironment.LinuxKDE, false)]
    [InlineData(CompositorType.GNOME, DisplayEnvironment.LinuxGnome, false)]
    [InlineData(CompositorType.Other, DisplayEnvironment.LinuxWayland, false)]
    [InlineData(CompositorType.Unknown, DisplayEnvironment.Unknown, false)]
    public void CurrentEnvironment_ShouldMapFromCompositor(CompositorType compositor, DisplayEnvironment expectedEnvironment, bool expectedHandlesCloseButton)
    {
        var provider = new LinuxEnvironmentInfoProvider(compositor);

        Assert.Equal(expectedEnvironment, provider.CurrentEnvironment);
        Assert.Equal(expectedHandlesCloseButton, provider.WindowManagerHandlesCloseButton);
    }
}
