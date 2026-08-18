namespace CrossMacro.UI.ViewModels;

/// <summary>
/// Maps environment capabilities to presentation-only localization guidance.
/// The policy is deliberately free of Avalonia and runtime service state so it
/// can be tested independently from <see cref="MainWindowViewModel"/>.
/// </summary>
internal static class MainWindowPresentationPolicy
{
    internal static string? GetBackendTroubleshootingHintKey(DisplayEnvironment environment)
    {
        return environment switch
        {
            DisplayEnvironment.LinuxX11
                or DisplayEnvironment.LinuxWayland
                or DisplayEnvironment.LinuxHyprland
                or DisplayEnvironment.LinuxWayfire
                or DisplayEnvironment.LinuxKDE
                or DisplayEnvironment.LinuxGnome
                => "MainWindow_BackendTroubleshootingLinux",
            DisplayEnvironment.Windows
                => "MainWindow_BackendTroubleshootingWindows",
            DisplayEnvironment.MacOS
                => "MainWindow_BackendTroubleshootingMacOS",
            DisplayEnvironment.Unknown => null,
            _ => throw new ArgumentOutOfRangeException(nameof(environment), environment, message: null),
        };
    }
}
