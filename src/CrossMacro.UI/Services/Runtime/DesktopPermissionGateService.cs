using CrossMacro.Platform.Abstractions.Permissions;

namespace CrossMacro.UI.Services.Runtime;

internal sealed class DesktopPermissionGateService(
    IDisplaySessionService displaySessionService,
    Func<IPermissionChecker?> getPermissionChecker,
    Func<ISettingsService> getSettingsService,
    BootstrapDialogHost dialogHost)
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
    private readonly Func<ISettingsService> _getSettingsService = getSettingsService ?? throw new ArgumentNullException(nameof(getSettingsService));
    private readonly BootstrapDialogHost _dialogHost = dialogHost ?? throw new ArgumentNullException(nameof(dialogHost));

    public async Task<GateResult> TryHandleAsync(IClassicDesktopStyleApplicationLifetime desktop, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(desktop);

        cancellationToken.ThrowIfCancellationRequested();
        var permissionChecker = _getPermissionChecker();
        PrepareStartupPermissionRequest(permissionChecker);
        var startupGateKind = GetStartupPermissionGateKind(permissionChecker);
        if (startupGateKind is not StartupPermissionGateKind.None)
        {
            var permissionResolved = await HandleStartupPermissionGateAsync(desktop, permissionChecker!, startupGateKind, cancellationToken).ConfigureAwait(false);
            if (!permissionResolved)
            {
                return GateResult.HandledByDialog();
            }
        }

        await HandleOptionalScreenRecordingOnboardingAsync(desktop, permissionChecker, cancellationToken).ConfigureAwait(false);

        var sessionSupport = await _displaySessionService.IsSessionSupportedAsync(cancellationToken).ConfigureAwait(false);
        if (!sessionSupport.Supported)
        {
            return GateResult.UnsupportedSession(sessionSupport.Reason);
        }

        return GateResult.Continue();
    }

    private async Task HandleOptionalScreenRecordingOnboardingAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        IPermissionChecker? permissionChecker, CancellationToken cancellationToken)
    {
        try
        {
            ISettingsService settingsService = _getSettingsService();
            if (!ShouldOfferOptionalScreenRecording(permissionChecker, settingsService.Current))
            {
                return;
            }

            var macOSPermissionChecker = (IMacOSPermissionChecker)permissionChecker!;

            await _dialogHost.RunWithOwnerAsync(desktop, async bootstrapOwner =>
            {
                var shouldEnable = await _dialogHost.ShowDialogAsync<bool>(
                    bootstrapOwner,
                    () => BootstrapDialogHost.CreateCenteredConfirmationDialog(
                        UIStrings.MacOSScreenRecordingOptionalTitle,
                        UIStrings.MacOSScreenRecordingOptionalMessage,
                        UIStrings.EnableButton,
                        UIStrings.NotNowButton)).ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();
                _ = settingsService.AccessCurrent(settings => settings.MacOSScreenRecordingOnboardingCompleted = true);
                await settingsService.SaveAsync().ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (!shouldEnable)
                {
                    return;
                }

                RequestOptionalScreenRecording(macOSPermissionChecker);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not OperationCanceledException)
        {
            Log.Warning(ex, "[DesktopPermissionGateService] Optional macOS Screen Recording onboarding failed; continuing startup");
        }
    }

    internal static bool ShouldOfferOptionalScreenRecording(
        IPermissionChecker? permissionChecker,
        AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (permissionChecker is not IMacOSPermissionChecker macOSPermissionChecker
            || !permissionChecker.IsSupported
            || settings.MacOSScreenRecordingOnboardingCompleted)
        {
            return false;
        }

        MacOSPermissionStatus status = macOSPermissionChecker.GetCurrentStatus();
        return status.ScreenRecordingApiAvailable
            && !status.IsGranted(MacOSPermissionRequirement.ScreenRecording);
    }

    internal static void RequestOptionalScreenRecording(IMacOSPermissionChecker permissionChecker)
    {
        ArgumentNullException.ThrowIfNull(permissionChecker);
        _ = permissionChecker.RequestScreenRecordingAccess();
        if (!permissionChecker.IsScreenRecordingAccessGranted())
        {
            permissionChecker.OpenScreenRecordingSettings();
        }
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

    private async Task<bool> HandleStartupPermissionGateAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        IPermissionChecker permissionChecker,
        StartupPermissionGateKind gateKind, CancellationToken cancellationToken)
    {
        var permissionResolved = false;

        await _dialogHost.RunWithOwnerAsync(desktop, async bootstrapOwner =>
        {
            try
            {
                var currentGateKind = gateKind;
                while (currentGateKind is not StartupPermissionGateKind.None)
                {
                    var shouldOpenSettings = await _dialogHost.ShowDialogAsync<bool>(
                        bootstrapOwner,
                        () => BootstrapDialogHost.CreateCenteredConfirmationDialog(
                            UIStrings.PermissionRequiredTitle,
                            GetStartupPermissionMessage(currentGateKind),
                            UIStrings.OpenSettingsButton,
                            UIStrings.ExitButton,
                            dangerYes: false,
                            dangerNo: true)).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!shouldOpenSettings)
                    {
                        return;
                    }

                    OpenStartupPermissionSettings(permissionChecker, currentGateKind);

                    var shouldRecheck = await _dialogHost.ShowDialogAsync<bool>(
                        bootstrapOwner,
                        () => BootstrapDialogHost.CreateCenteredConfirmationDialog(
                            UIStrings.PermissionRequiredTitle,
                            UIStrings.MacOSPermissionApprovalRecheckMessage,
                            UIStrings.ContinueButton,
                            UIStrings.ExitButton,
                            dangerYes: false,
                            dangerNo: true)).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
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
            catch (Exception ex) when (ex is not OutOfMemoryException and not OperationCanceledException)
            {
                Log.LogError(ex, "[DesktopPermissionGateService] macOS startup permission gate flow failed");
            }
        }, cancellationToken).ConfigureAwait(false);

        if (!permissionResolved)
        {
            try
            {
                await _dialogHost.ShutdownAsync(desktop).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not OperationCanceledException)
            {
                Log.Warning(ex, "[DesktopPermissionGateService] Failed to shutdown app after macOS permission gate");
            }
        }

        return permissionResolved;
    }

    private static string GetStartupPermissionMessage(StartupPermissionGateKind gateKind)
    {
        return gateKind is StartupPermissionGateKind.InputMonitoring
            ? UIStrings.MacOSInputMonitoringStartupBlockMessage
            : UIStrings.MacOSAccessibilityStartupBlockMessage;
    }

    private async Task ShowApprovalPendingDialogAsync(Window bootstrapOwner, StartupPermissionGateKind gateKind)
    {
        _ = await _dialogHost.ShowDialogAsync<bool>(
            bootstrapOwner,
            () => BootstrapDialogHost.CreateCenteredConfirmationDialog(
                UIStrings.PermissionRequiredTitle,
                GetApprovalPendingMessage(gateKind),
                UIStrings.ExitButton,
                noText: null,
                dangerYes: true)).ConfigureAwait(false);
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
