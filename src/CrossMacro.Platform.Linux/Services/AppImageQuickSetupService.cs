using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Linux.Services.QuickSetup;

namespace CrossMacro.Platform.Linux.Services;

internal sealed class AppImageQuickSetupService : IAppImageQuickSetupService
{
    private const string AppImageKey = "APPIMAGE";
    private const string FlatpakIdKey = "FLATPAK_ID";
    private const string SessionTypeKey = "XDG_SESSION_TYPE";

    private readonly Func<string, string?> _getEnvironmentVariable;
    private readonly ILinuxInputCapabilityDetector _capabilityDetector;
    private readonly LinuxQuickSetupExecutor _executor;
    private readonly IPrivilegedHostCommandLauncher _launcher;

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

        if (!string.IsNullOrWhiteSpace(_getEnvironmentVariable(FlatpakIdKey)))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(_getEnvironmentVariable(AppImageKey)))
        {
            return false;
        }

        var sessionType = _getEnvironmentVariable(SessionTypeKey);
        return string.Equals(sessionType, "wayland", StringComparison.OrdinalIgnoreCase);
    }

    public bool ShouldPrompt()
    {
        if (!IsApplicable())
        {
            return false;
        }

        var mode = _capabilityDetector.DetermineMode();
        return mode == InputProviderMode.None ||
               (mode == InputProviderMode.Legacy && !_capabilityDetector.CanReadInputEvents);
    }

    public async Task<QuickSetupResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var result = await _executor.RunAsync(
            _launcher,
            LinuxQuickSetupScriptOptions.Strict,
            "AppImageQuickSetupService",
            "Failed to run quick setup command from AppImage.",
            cancellationToken);

        if (result.Success)
        {
            _capabilityDetector.InvalidateCache();
        }

        return result;
    }
}
