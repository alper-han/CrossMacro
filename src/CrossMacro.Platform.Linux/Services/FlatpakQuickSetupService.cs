using System;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using CrossMacro.Platform.Linux.Services.QuickSetup;

namespace CrossMacro.Platform.Linux.Services;

internal sealed class FlatpakQuickSetupService : IFlatpakQuickSetupService
{
    private const string FlatpakIdKey = "FLATPAK_ID";
    private const string SessionTypeKey = "XDG_SESSION_TYPE";

    private readonly Func<string, string?> _getEnvironmentVariable;
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

    public bool IsApplicable()
    {
        if (!OperatingSystem.IsLinux())
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(_getEnvironmentVariable(FlatpakIdKey)))
        {
            return false;
        }

        var sessionType = _getEnvironmentVariable(SessionTypeKey);
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
            LinuxQuickSetupScriptOptions.Lenient,
            "FlatpakQuickSetupService",
            "Failed to run quick setup command inside Flatpak.",
            cancellationToken);
    }
}
