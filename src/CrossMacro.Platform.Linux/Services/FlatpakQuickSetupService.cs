
namespace CrossMacro.Platform.Linux.Services;

internal sealed class FlatpakQuickSetupService : IFlatpakQuickSetupService
{
    private const string SessionTypeKey = "XDG_SESSION_TYPE";

    private readonly Func<string, string?> _getEnvironmentVariable;
    private readonly LinuxEnvironmentSnapshot? _environment;
    private readonly ILinuxCapabilitySnapshotProvider _snapshotProvider;
    private readonly LinuxQuickSetupExecutor _executor;
    private readonly IPrivilegedHostCommandLauncher _launcher;

    internal FlatpakQuickSetupService(
        Func<string, string?> getEnvironmentVariable,
        LinuxQuickSetupExecutor executor,
        IPrivilegedHostCommandLauncher launcher,
        ILinuxCapabilitySnapshotProvider snapshotProvider)
    {
        _getEnvironmentVariable = getEnvironmentVariable ?? throw new ArgumentNullException(nameof(getEnvironmentVariable));
        _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
    }

    internal FlatpakQuickSetupService(
        LinuxEnvironmentSnapshot environment,
        LinuxQuickSetupExecutor executor,
        IPrivilegedHostCommandLauncher launcher,
        ILinuxCapabilitySnapshotProvider snapshotProvider)
        : this(static _ => null, executor, launcher, snapshotProvider)
    {
        _environment = environment;
    }

    public bool IsApplicable()
    {
        if (!OperatingSystem.IsLinux())
        {
            return false;
        }

        var environment = _environment ?? new LinuxEnvironmentSnapshot(
            FlatpakId: _getEnvironmentVariable("FLATPAK_ID"),
            AppImage: null,
            SessionType: _getEnvironmentVariable(SessionTypeKey),
            WaylandDisplay: null,
            Display: null,
            CurrentDesktop: null,
            GdmSession: null,
            HyprlandInstanceSignature: null,
            RuntimeDir: null,
            WayfireSocket: null,
            SwaySocket: null,
            WindowButtons: null,
            CrossMacroFlatpak: _getEnvironmentVariable("CROSSMACRO_FLATPAK"),
            FlatpakInfoExists: false);

        if (!environment.IsFlatpak)
        {
            return false;
        }

        var sessionType = environment.SessionType;
        if (!string.Equals(sessionType, "wayland", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    public async Task<QuickSetupResult> RunAsync(CancellationToken cancellationToken = default)
    {
        QuickSetupResult result;
        try
        {
            result = await _executor.RunAsync(
                _launcher, LinuxQuickSetupScriptOptions.Strict, "FlatpakQuickSetupService",
                "Failed to run quick setup command inside Flatpak.", cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _snapshotProvider.InvalidateCache();
        }

        if (!result.Success)
        {
            return result;
        }
        var input = _snapshotProvider.GetSnapshot().Input;
        return input.CanUseDirectUInput && input.CanReadInputEvents
            ? result
            : new QuickSetupResult(QuickSetupOutcome.DeviceAccessUnavailable,
                "The host helper completed, but this app still cannot write /dev/uinput or read usable input devices. Host permissions may have changed; check Flatpak device access restrictions.");
    }
}
