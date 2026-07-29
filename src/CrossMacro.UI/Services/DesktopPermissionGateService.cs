
namespace CrossMacro.UI.Services;

internal sealed class DesktopPermissionGateService(
    IDisplaySessionService displaySessionService,
    Func<IPermissionChecker?> getPermissionChecker)
{
    internal readonly record struct GateResult(bool Handled, string? UnsupportedSessionReason)
    {
        public static GateResult Continue() => new(Handled: false, UnsupportedSessionReason: null);
        public static GateResult HandledByDialog() => new(Handled: true, UnsupportedSessionReason: null);

        public static GateResult UnsupportedSession(string reason)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(reason);
            return new(Handled: false, reason);
        }
    }

    internal enum StartupPermissionGateKind
    {
        None,
        InputMonitoring,
        Accessibility,
    }

    private readonly IDisplaySessionService _displaySessionService = displaySessionService ?? throw new ArgumentNullException(nameof(displaySessionService));
    private readonly Func<IPermissionChecker?> _getPermissionChecker = getPermissionChecker ?? throw new ArgumentNullException(nameof(getPermissionChecker));

    public async Task<GateResult> TryHandleAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        ArgumentNullException.ThrowIfNull(desktop);

        var permissionChecker = _getPermissionChecker();
        PrepareStartupPermissionRequest(permissionChecker);
        var startupGateKind = GetStartupPermissionGateKind(permissionChecker);
        if (startupGateKind is not StartupPermissionGateKind.None)
        {
            var permissionResolved = await HandleStartupPermissionGateAsync(desktop, permissionChecker!, startupGateKind).ConfigureAwait(false);
            if (!permissionResolved)
            {
                return GateResult.HandledByDialog();
            }
        }

        var sessionSupport = await _displaySessionService.IsSessionSupportedAsync(CancellationToken.None).ConfigureAwait(false);
        if (!sessionSupport.Supported)
        {
            return GateResult.UnsupportedSession(sessionSupport.Reason);
        }

        return GateResult.Continue();
    }

    internal static bool IsStartupPermissionBlocked(IPermissionChecker? permissionChecker)
    {
        return GetStartupPermissionGateKind(permissionChecker) is not StartupPermissionGateKind.None;
    }

    internal static void PrepareStartupPermissionRequest(IPermissionChecker? permissionChecker)
    {
        if (permissionChecker is null || !permissionChecker.IsSupported || !permissionChecker.RequiresStartupPermissionGate)
        {
            return;
        }

        if (permissionChecker is IMacOSPermissionChecker macOSPermissionChecker)
        {
            _ = macOSPermissionChecker.RequestListenEventAccess();
        }
    }

    internal static StartupPermissionGateKind GetStartupPermissionGateKind(IPermissionChecker? permissionChecker)
    {
        if (permissionChecker is null || !permissionChecker.IsSupported)
        {
            return StartupPermissionGateKind.None;
        }

        if (!permissionChecker.RequiresStartupPermissionGate)
        {
            return StartupPermissionGateKind.None;
        }

        if (permissionChecker is IMacOSPermissionChecker macOSPermissionChecker)
        {
            if (!macOSPermissionChecker.IsListenEventListedOrGranted())
            {
                return StartupPermissionGateKind.InputMonitoring;
            }

            return macOSPermissionChecker.IsPostEventAccessGranted() || macOSPermissionChecker.IsAccessibilityTrusted()
                ? StartupPermissionGateKind.None
                : StartupPermissionGateKind.Accessibility;
        }

        return permissionChecker.IsAccessibilityTrusted()
            ? StartupPermissionGateKind.None
            : StartupPermissionGateKind.Accessibility;
    }

    internal static ConfirmationDialog CreateCenteredConfirmationDialog(
        string title,
        string message,
        string yesText,
        string? noText,
        bool dangerYes = false,
        bool dangerNo = false)
    {
        return new ConfirmationDialog(
            title,
            message,
            yesText,
            noText,
            dangerYes,
            dangerNo)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
        };
    }

    internal static async Task RunWithBootstrapOwnerAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        Func<Window, Task> action)
    {
        var bootstrapOwner = CreateBootstrapOwnerWindow();
        desktop.MainWindow = bootstrapOwner;
        bootstrapOwner.Show();

        try
        {
            await action(bootstrapOwner).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                bootstrapOwner.Close();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // Ignore close races if owner was already disposed by the windowing backend.
            }
        }
    }

    private static async Task<bool> HandleStartupPermissionGateAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        IPermissionChecker permissionChecker,
        StartupPermissionGateKind gateKind)
    {
        var permissionResolved = false;

        await RunWithBootstrapOwnerAsync(desktop, async bootstrapOwner =>
        {
            try
            {
                var currentGateKind = gateKind;
                while (currentGateKind is not StartupPermissionGateKind.None)
                {
                    var permissionDialog = CreateCenteredConfirmationDialog(
                        UIStrings.PermissionRequiredTitle,
                        GetStartupPermissionMessage(currentGateKind),
                        UIStrings.OpenSettingsButton,
                        UIStrings.ExitButton,
                        dangerYes: false,
                        dangerNo: true);

                    var shouldOpenSettings = await permissionDialog.ShowDialog<bool>(bootstrapOwner).ConfigureAwait(false);
                    if (!shouldOpenSettings)
                    {
                        return;
                    }

                    OpenStartupPermissionSettings(permissionChecker, currentGateKind);

                    var recheckDialog = CreateCenteredConfirmationDialog(
                        UIStrings.PermissionRequiredTitle,
                        UIStrings.MacOSPermissionApprovalRecheckMessage,
                        UIStrings.ContinueButton,
                        UIStrings.ExitButton,
                        dangerYes: false,
                        dangerNo: true);

                    var shouldRecheck = await recheckDialog.ShowDialog<bool>(bootstrapOwner).ConfigureAwait(false);
                    if (!shouldRecheck)
                    {
                        return;
                    }

                    currentGateKind = GetStartupPermissionGateKind(permissionChecker);
                    if (currentGateKind is not StartupPermissionGateKind.None)
                    {
                        await ShowApprovalPendingDialogAsync(bootstrapOwner, currentGateKind).ConfigureAwait(false);
                        return;
                    }
                }

                permissionResolved = true;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.LogError(ex, "[DesktopStartupCoordinator] macOS startup permission gate flow failed");
            }
        }).ConfigureAwait(false);

        if (!permissionResolved)
        {
            try
            {
                desktop.Shutdown();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.Warning(ex, "[DesktopStartupCoordinator] Failed to shutdown app after macOS permission gate");
            }
        }

        return permissionResolved;
    }

    private static Window CreateBootstrapOwnerWindow()
    {
        return new Window
        {
            Width = 1,
            Height = 1,
            Opacity = 0,
            ShowInTaskbar = false,
            CanResize = false,
            WindowDecorations = WindowDecorations.None,
            WindowStartupLocation = WindowStartupLocation.Manual,
        };
    }

    private static string GetStartupPermissionMessage(StartupPermissionGateKind gateKind)
    {
        return gateKind is StartupPermissionGateKind.InputMonitoring
            ? UIStrings.MacOSInputMonitoringStartupBlockMessage
            : UIStrings.MacOSAccessibilityStartupBlockMessage;
    }

    private static async Task ShowApprovalPendingDialogAsync(Window bootstrapOwner, StartupPermissionGateKind gateKind)
    {
        var pendingDialog = CreateCenteredConfirmationDialog(
            UIStrings.PermissionRequiredTitle,
            GetApprovalPendingMessage(gateKind),
            UIStrings.ExitButton,
            noText: null,
            dangerYes: true);

        _ = await pendingDialog.ShowDialog<bool>(bootstrapOwner).ConfigureAwait(false);
    }

    private static string GetApprovalPendingMessage(StartupPermissionGateKind gateKind)
    {
        return gateKind is StartupPermissionGateKind.InputMonitoring
            ? UIStrings.MacOSInputMonitoringApprovalPendingMessage
            : UIStrings.MacOSAccessibilityApprovalPendingMessage;
    }

    internal static void OpenStartupPermissionSettings(IPermissionChecker permissionChecker, StartupPermissionGateKind gateKind)
    {
        if (gateKind is not StartupPermissionGateKind.InputMonitoring)
        {
            if (permissionChecker is IMacOSPermissionChecker accessibilityPermissionChecker)
            {
                _ = accessibilityPermissionChecker.RequestPermission(MacOSPermissionRequirement.Accessibility);
            }

            permissionChecker.OpenAccessibilitySettings();
            return;
        }

        if (permissionChecker is IMacOSPermissionChecker macOSPermissionChecker)
        {
            _ = macOSPermissionChecker.RequestListenEventAccess();
            macOSPermissionChecker.OpenInputMonitoringSettings();
            return;
        }

        permissionChecker.OpenAccessibilitySettings();
    }
}
