using CrossMacro.Platform.Linux.DisplayServer.Wayland.Hyprland;
using CrossMacro.Platform.Linux.DisplayServer.Wayland.Niri;

namespace CrossMacro.Platform.Linux.Services.Factories;

/// <summary>
/// Chooses the <see cref="IWindowManager"/> for the running compositor: a compositor whose IPC socket answers wins,
/// otherwise the desktop environment decides, otherwise window management is reported as unsupported.
/// </summary>
internal static class LinuxWindowManagerFactory
{
    internal static IWindowManager Create(
        HyprlandIpcClient hyprland,
        ISwayIpcClient sway,
        INiriIpcClient niri,
        LinuxEnvironmentSnapshot environment)
    {
        if (hyprland.IsAvailable)
        {
            return new HyprlandWindowManager(hyprland);
        }

        if (sway.IsAvailable)
        {
            return new SwayWindowManager(sway);
        }

        if (niri.IsAvailable)
        {
            return new NiriWindowManager(niri);
        }

        if (LinuxDisplaySessionClassifier.IsKde(environment))
        {
            return new KdeWindowManager();
        }

        if (LinuxDisplaySessionClassifier.IsGnome(environment))
        {
            return new GnomeWindowManager();
        }

        return new NullWindowManager(operation =>
            Log.Warning("[NullWindowManager] Window management is not supported on this platform. Operation: {Op}", operation));
    }
}
