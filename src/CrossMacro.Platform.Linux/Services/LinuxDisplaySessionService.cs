
namespace CrossMacro.Platform.Linux.Services;

public class LinuxDisplaySessionService : IDisplaySessionService
{
    private readonly ILinuxInputCapabilitySnapshotProvider _snapshotProvider;
    private readonly LinuxEnvironmentVariables _environmentVariables;

    public LinuxDisplaySessionService()
        : this(LinuxEnvironmentVariables.CaptureCurrentSnapshot()) { /* Empty */ }

    private LinuxDisplaySessionService(LinuxEnvironmentSnapshot environment)
        : this(new LinuxInputCapabilitySnapshotProvider(!environment.UsesPortableDirectInput), environment) { /* Empty */ }

    internal LinuxDisplaySessionService(
        ILinuxInputCapabilitySnapshotProvider snapshotProvider,
        LinuxEnvironmentSnapshot environment)
    {
        _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
        _environmentVariables = new LinuxEnvironmentVariables(environment);
    }

    public bool IsSessionSupported(out string reason)
    {
        reason = string.Empty;

        var environment = _environmentVariables.CaptureSnapshot();
        bool isFlatpak = environment.IsFlatpak;

        Log.Information("[LinuxDisplaySessionService] Checking Session Support. Flatpak: {IsFlatpak}, ID: {FlatpakId}",
            isFlatpak, environment.FlatpakId ?? "null");

        if (!isFlatpak)
        {
            return true;
        }

        return IsFlatpakSessionSupported(environment, out reason);
    }

    public async ValueTask<(bool Supported, string Reason)> IsSessionSupportedAsync(CancellationToken cancellationToken = default)
    {
        var environment = _environmentVariables.CaptureSnapshot();
        bool isFlatpak = environment.IsFlatpak;

        Log.Information("[LinuxDisplaySessionService] Checking Session Support. Flatpak: {IsFlatpak}, ID: {FlatpakId}",
            isFlatpak, environment.FlatpakId ?? "null");

        if (!isFlatpak)
        {
            return (true, string.Empty);
        }

        return await IsFlatpakSessionSupportedAsync(environment, cancellationToken).ConfigureAwait(false);
    }

    private bool IsFlatpakSessionSupported(LinuxEnvironmentSnapshot environment, out string reason)
    {
        reason = string.Empty;

        var compositor = CompositorDetector.ClassifyFromEnvironment(environment, OperatingSystem.IsLinux());
        bool isWaylandSession = string.Equals(environment.SessionType, "wayland", StringComparison.OrdinalIgnoreCase);
        bool isX11Session = string.Equals(environment.SessionType, "x11", StringComparison.OrdinalIgnoreCase);

        if (compositor is CompositorType.X11 || isX11Session)
        {
            Log.Information("[LinuxDisplaySessionService] Flatpak running on X11. Supported.");
            return true;
        }

        if (!isWaylandSession)
        {
            reason = "Unsupported Flatpak session. CrossMacro requires an X11 or Wayland desktop session.";
            Log.Warning("[LinuxDisplaySessionService] {Reason} SessionType={SessionType}, Compositor={Compositor}",
                reason, environment.SessionType ?? "unknown", compositor);
            return false;
        }

        LinuxInputCapabilitySnapshot? startupSnapshot = null;
        return IsFlatpakWaylandDirectSupported(ref startupSnapshot, out reason);
    }

    private async ValueTask<(bool Supported, string Reason)> IsFlatpakSessionSupportedAsync(
        LinuxEnvironmentSnapshot environment,
        CancellationToken cancellationToken)
    {
        var compositor = CompositorDetector.ClassifyFromEnvironment(environment, OperatingSystem.IsLinux());
        bool isWaylandSession = string.Equals(environment.SessionType, "wayland", StringComparison.OrdinalIgnoreCase);
        bool isX11Session = string.Equals(environment.SessionType, "x11", StringComparison.OrdinalIgnoreCase);

        if (compositor is CompositorType.X11 || isX11Session)
        {
            Log.Information("[LinuxDisplaySessionService] Flatpak running on X11. Supported.");
            return (true, string.Empty);
        }

        if (!isWaylandSession)
        {
            var reason = "Unsupported Flatpak session. CrossMacro requires an X11 or Wayland desktop session.";
            Log.Warning("[LinuxDisplaySessionService] {Reason} SessionType={SessionType}, Compositor={Compositor}",
                reason, environment.SessionType ?? "unknown", compositor);
            return (false, reason);
        }

        LinuxInputCapabilitySnapshot? startupSnapshot = null;
        return await IsFlatpakWaylandDirectSupportedAsync(startupSnapshot, cancellationToken).ConfigureAwait(false);
    }

    private bool IsFlatpakWaylandDirectSupported(ref LinuxInputCapabilitySnapshot? snapshot, out string reason)
    {
        if (HasDirectInputAccess(ref snapshot))
        {
            Log.Information("[LinuxDisplaySessionService] Flatpak on Wayland without daemon. Using direct device access.");
            reason = string.Empty;
            return true;
        }

        reason = "Wayland direct mode requires /dev/uinput write access and readable /dev/input/event* devices.";
        Log.Warning("[LinuxDisplaySessionService] {Reason}", reason);
        return false;
    }

    private async ValueTask<(bool Supported, string Reason)> IsFlatpakWaylandDirectSupportedAsync(
        LinuxInputCapabilitySnapshot? snapshot,
        CancellationToken cancellationToken)
    {
        snapshot ??= await _snapshotProvider.CaptureSnapshotAsync(TimeSpan.Zero, cancellationToken).ConfigureAwait(false);

        if (snapshot.Value.HasDirectInputAccess)
        {
            Log.Information("[LinuxDisplaySessionService] Flatpak on Wayland without daemon. Using direct device access.");
            return (true, string.Empty);
        }

        var reason = "Wayland direct mode requires /dev/uinput write access and readable /dev/input/event* devices.";
        Log.Warning("[LinuxDisplaySessionService] {Reason}", reason);
        return (false, reason);
    }

    private bool HasDirectInputAccess(ref LinuxInputCapabilitySnapshot? snapshot)
    {
        try
        {
            snapshot ??= _snapshotProvider.CaptureSnapshot(TimeSpan.Zero);
            return snapshot.Value.HasDirectInputAccess;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return false;
        }
    }
}
