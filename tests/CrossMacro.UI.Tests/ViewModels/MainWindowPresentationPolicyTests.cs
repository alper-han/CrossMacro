namespace CrossMacro.UI.Tests.ViewModels;

public sealed class MainWindowPresentationPolicyTests
{
    [Theory]
    [InlineData(DisplayEnvironment.LinuxX11)]
    [InlineData(DisplayEnvironment.LinuxWayland)]
    [InlineData(DisplayEnvironment.LinuxHyprland)]
    [InlineData(DisplayEnvironment.LinuxWayfire)]
    [InlineData(DisplayEnvironment.LinuxKDE)]
    [InlineData(DisplayEnvironment.LinuxGnome)]
    public void LinuxEnvironments_UseLinuxTroubleshootingGuidance(DisplayEnvironment environment)
    {
        Assert.Equal(
            "MainWindow_BackendTroubleshootingLinux",
            MainWindowPresentationPolicy.GetBackendTroubleshootingHintKey(environment));
    }

    [Theory]
    [InlineData(DisplayEnvironment.Windows, "MainWindow_BackendTroubleshootingWindows")]
    [InlineData(DisplayEnvironment.MacOS, "MainWindow_BackendTroubleshootingMacOS")]
    public void HostEnvironments_UseHostTroubleshootingGuidance(
        DisplayEnvironment environment,
        string expectedKey)
    {
        Assert.Equal(expectedKey, MainWindowPresentationPolicy.GetBackendTroubleshootingHintKey(environment));
    }

    [Fact]
    public void UnknownEnvironment_DoesNotShowTroubleshootingGuidance()
    {
        Assert.Null(MainWindowPresentationPolicy.GetBackendTroubleshootingHintKey(DisplayEnvironment.Unknown));
    }
}
