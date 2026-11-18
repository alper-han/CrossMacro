
namespace CrossMacro.Platform.Linux.Services;

public class LinuxDisplaySessionService : IDisplaySessionService
{
    private const string UnsupportedSessionReason = "Unsupported Flatpak session. CrossMacro requires an X11 or Wayland desktop session.";
    private const string DirectInputUnavailableReason = "Wayland direct mode requires /dev/uinput write access and readable /dev/input/event* devices.";

    private readonly ILinuxInputCapabilitySnapshotProvider _snapshotProvider;
    private readonly LinuxEnvironmentSnapshot _environment;

    public LinuxDisplaySessionService()
        : this(LinuxEnvironmentVariables.CaptureCurrentSnapshot()) { /* Empty */ }

    private LinuxDisplaySessionService(LinuxEnvironmentSnapshot environment)
        : this(new LinuxInputCapabilitySnapshotProvider(!environment.UsesPortableDirectInput), environment) { /* Empty */ }

    internal LinuxDisplaySessionService(
        ILinuxInputCapabilitySnapshotProvider snapshotProvider,
        LinuxEnvironmentSnapshot environment)
    {
        _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
        _environment = environment;
    }

    public bool IsSessionSupported(out string reason)
    {
        var verdict = Evaluate();
        if (verdict is SessionVerdict.RequiresDirectInput)
        {
            return ReportDirectInput(HasDirectInputAccess(), out reason);
        }

        reason = verdict is SessionVerdict.Unsupported ? UnsupportedSessionReason : string.Empty;
        return verdict is SessionVerdict.Supported;
    }

    public async ValueTask<(bool Supported, string Reason)> IsSessionSupportedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var verdict = Evaluate();
        if (verdict is SessionVerdict.RequiresDirectInput)
        {
            var snapshot = await _snapshotProvider.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
            var supported = ReportDirectInput(snapshot.HasDirectInputAccess, out var reason);
            return (supported, reason);
        }

        return verdict is SessionVerdict.Supported ? (true, string.Empty) : (false, UnsupportedSessionReason);
    }

    private SessionVerdict Evaluate()
    {
        Log.Information("[LinuxDisplaySessionService] Checking Session Support. Flatpak: {IsFlatpak}, ID: {FlatpakId}",
            _environment.IsFlatpak, _environment.FlatpakId ?? "null");

        if (!_environment.IsFlatpak)
        {
            return SessionVerdict.Supported;
        }

        var session = LinuxDisplaySessionClassifier.Classify(_environment);
        if (session is LinuxSessionType.Wayland)
        {
            return SessionVerdict.RequiresDirectInput;
        }

        if (session is LinuxSessionType.X11)
        {
            Log.Information("[LinuxDisplaySessionService] Flatpak running on X11. Supported.");
            return SessionVerdict.Supported;
        }

        Log.Warning("[LinuxDisplaySessionService] {Reason} SessionType={SessionType}",
            UnsupportedSessionReason, _environment.SessionType ?? "unknown");
        return SessionVerdict.Unsupported;
    }

    private bool HasDirectInputAccess()
    {
        try
        {
            return _snapshotProvider.GetSnapshot().HasDirectInputAccess;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return false;
        }
    }

    private static bool ReportDirectInput(bool hasDirectInputAccess, out string reason)
    {
        if (hasDirectInputAccess)
        {
            Log.Information("[LinuxDisplaySessionService] Flatpak on Wayland without daemon. Using direct device access.");
            reason = string.Empty;
            return true;
        }

        reason = DirectInputUnavailableReason;
        Log.Warning("[LinuxDisplaySessionService] {Reason}", reason);
        return false;
    }

    private enum SessionVerdict
    {
        Supported,
        Unsupported,
        RequiresDirectInput,
    }
}
