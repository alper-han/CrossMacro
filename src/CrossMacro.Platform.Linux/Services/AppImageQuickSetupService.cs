
namespace CrossMacro.Platform.Linux.Services;

internal sealed class AppImageQuickSetupService : IAppImageQuickSetupService
{
    private const string AppImageKey = "APPIMAGE";
    private const string SessionTypeKey = "XDG_SESSION_TYPE";

    private readonly Func<string, string?> _getEnvironmentVariable;
    private readonly ILinuxInputCapabilityDetector? _capabilityDetector;
    private readonly ILinuxCapabilitySnapshotProvider? _snapshotProvider;
    private readonly LinuxQuickSetupExecutor _executor;
    private readonly IPrivilegedHostCommandLauncher _launcher;

    internal AppImageQuickSetupService(
        ILinuxCapabilitySnapshotProvider snapshotProvider,
        LinuxQuickSetupExecutor executor,
        IPrivilegedHostCommandLauncher launcher)
    {
        _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _getEnvironmentVariable = static _ => null;
        _capabilityDetector = null;
    }

    internal AppImageQuickSetupService(
        ILinuxCapabilitySnapshotProvider snapshotProvider,
        Func<string, string?> getEnvironmentVariable,
        LinuxQuickSetupExecutor executor,
        IPrivilegedHostCommandLauncher launcher)
    {
        _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
        _getEnvironmentVariable = getEnvironmentVariable ?? throw new ArgumentNullException(nameof(getEnvironmentVariable));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _capabilityDetector = null;
    }

    internal AppImageQuickSetupService(
        ILinuxInputCapabilityDetector capabilityDetector,
        Func<string, string?> getEnvironmentVariable,
        LinuxQuickSetupExecutor executor,
        IPrivilegedHostCommandLauncher launcher)
    {
        _capabilityDetector = capabilityDetector ?? throw new ArgumentNullException(nameof(capabilityDetector));
        _getEnvironmentVariable = getEnvironmentVariable ?? throw new ArgumentNullException(nameof(getEnvironmentVariable));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
    }

    public bool IsApplicable()
    {
        if (!OperatingSystem.IsLinux())
        {
            return false;
        }

        var environment = _snapshotProvider?.GetSnapshot().Environment ?? new LinuxEnvironmentSnapshot(
            FlatpakId: _getEnvironmentVariable("FLATPAK_ID"),
            AppImage: _getEnvironmentVariable(AppImageKey),
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
        var appImage = environment.AppImage;
        var sessionType = environment.SessionType;

        if (environment.IsFlatpak)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(appImage))
        {
            return false;
        }

        return string.Equals(sessionType, "wayland", StringComparison.OrdinalIgnoreCase);
    }

    public bool ShouldPrompt()
    {
        if (!IsApplicable())
        {
            return false;
        }

        if (_snapshotProvider is not null)
        {
            var input = _snapshotProvider.GetSnapshot().Input;
            return !input.CanUseDirectUInput || !input.CanReadInputEvents;
        }

        if (_capabilityDetector is not null)
        {
            return !_capabilityDetector.CanUseDirectUInput || !_capabilityDetector.CanReadInputEvents;
        }

        return false;
    }

    public async Task<QuickSetupResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var result = await _executor.RunAsync(
            _launcher,
            LinuxQuickSetupScriptOptions.Strict,
            "AppImageQuickSetupService",
            "Failed to run quick setup command from AppImage.",
            cancellationToken).ConfigureAwait(false);

        if (result.Success)
        {
            if (_snapshotProvider is not null)
            {
                _snapshotProvider.InvalidateCache();
            }
            else
            {
                _capabilityDetector?.InvalidateCache();
            }
        }

        return result;
    }
}
