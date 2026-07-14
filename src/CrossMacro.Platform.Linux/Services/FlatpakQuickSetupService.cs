using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Packaging.Abstractions;
using CrossMacro.Platform.Linux.Services.QuickSetup;

namespace CrossMacro.Platform.Linux.Services;

internal sealed class FlatpakQuickSetupService : IFlatpakQuickSetupService
{
    private const string SessionTypeKey = "XDG_SESSION_TYPE";

    private readonly Func<string, string?> _getEnvironmentVariable;
    private readonly LinuxEnvironmentSnapshot? _environment;
    private readonly LinuxQuickSetupExecutor _executor;
    private readonly IPrivilegedHostCommandLauncher _launcher;

    internal FlatpakQuickSetupService(
        Func<string, string?> getEnvironmentVariable,
        LinuxQuickSetupExecutor executor,
        IPrivilegedHostCommandLauncher launcher)
    {
        _getEnvironmentVariable = getEnvironmentVariable ?? throw new ArgumentNullException(nameof(getEnvironmentVariable));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
    }

    internal FlatpakQuickSetupService(
        LinuxEnvironmentSnapshot environment,
        LinuxQuickSetupExecutor executor,
        IPrivilegedHostCommandLauncher launcher)
        : this(static _ => null, executor, launcher)
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
            UseDaemon: null,
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

    public Task<QuickSetupResult> RunAsync(CancellationToken cancellationToken = default)
    {
        return _executor.RunAsync(
            _launcher,
            LinuxQuickSetupScriptOptions.Strict,
            "FlatpakQuickSetupService",
            "Failed to run quick setup command inside Flatpak.",
            cancellationToken);
    }
}
