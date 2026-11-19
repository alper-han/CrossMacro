namespace CrossMacro.Platform.Linux.Tests.DisplayServer;

public sealed class CompositorTypeExtensionsTests
{
    [Theory]
    [InlineData(CompositorType.Unknown, "Unknown")]
    [InlineData(CompositorType.X11, "X11")]
    [InlineData(CompositorType.Hyprland, "HYPRLAND")]
    [InlineData(CompositorType.Wayfire, "WAYFIRE")]
    [InlineData(CompositorType.Niri, "NIRI")]
    [InlineData(CompositorType.Cosmic, "COSMIC")]
    [InlineData(CompositorType.Kde, "KDE")]
    [InlineData(CompositorType.Gnome, "GNOME")]
    [InlineData(CompositorType.Sway, "SWAY")]
    [InlineData(CompositorType.Other, "Other")]
    public void ToSessionKind_ReturnsThePublishedDiagnosticName(CompositorType compositor, string expected)
    {
        Assert.Equal(expected, compositor.ToSessionKind());
    }

    [Fact]
    public void ToSessionKind_CoversEveryCompositorType()
    {
        foreach (var compositor in Enum.GetValues<CompositorType>())
        {
            Assert.False(string.IsNullOrEmpty(compositor.ToSessionKind()));
        }
    }

    [Fact]
    public void ToSessionKind_RejectsUndefinedValues()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => ((CompositorType)999).ToSessionKind());
    }
}
