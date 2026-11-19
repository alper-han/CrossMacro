namespace CrossMacro.UI.ViewModels;

/// <summary>
/// Maps environment capabilities to presentation-only localization guidance.
/// The policy is deliberately free of Avalonia and runtime service state so it
/// can be tested independently from <see cref="MainWindowViewModel"/>.
/// </summary>
internal static class MainWindowPresentationPolicy
{
    internal static bool IsLinux(DisplayEnvironment environment)
    {
        return environment switch
        {
            DisplayEnvironment.LinuxX11
                or DisplayEnvironment.LinuxWayland
                or DisplayEnvironment.LinuxHyprland
                or DisplayEnvironment.LinuxWayfire
                or DisplayEnvironment.LinuxKDE
                or DisplayEnvironment.LinuxGnome => true,
            DisplayEnvironment.Windows
                or DisplayEnvironment.MacOS
                or DisplayEnvironment.Unknown => false,
            _ => throw new ArgumentOutOfRangeException(nameof(environment), environment, message: null),
        };
    }

    internal static string? GetBackendTroubleshootingHintKey(DisplayEnvironment environment)
    {
        if (IsLinux(environment))
        {
            return "MainWindow_BackendTroubleshootingLinux";
        }

        if (environment is DisplayEnvironment.Windows)
        {
            return "MainWindow_BackendTroubleshootingWindows";
        }

        return environment is DisplayEnvironment.MacOS ? "MainWindow_BackendTroubleshootingMacOS" : null;
    }
}
