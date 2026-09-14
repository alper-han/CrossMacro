
namespace CrossMacro.UI.Services.Runtime;

internal sealed class DesktopQuickSetupGateService(
    Func<IFlatpakQuickSetupService?> getFlatpakQuickSetupService,
    Func<IAppImageQuickSetupService?> getAppImageQuickSetupService,
    Func<IDisplaySessionService?>? getDisplaySessionService = null,
    Func<ILinuxDirectInputQuickSetupService?>? getLinuxDirectInputQuickSetupService = null)
{
    private readonly Func<IFlatpakQuickSetupService?> _getFlatpakQuickSetupService = getFlatpakQuickSetupService ?? throw new ArgumentNullException(nameof(getFlatpakQuickSetupService));
    private readonly Func<IAppImageQuickSetupService?> _getAppImageQuickSetupService = getAppImageQuickSetupService ?? throw new ArgumentNullException(nameof(getAppImageQuickSetupService));
    private readonly Func<IDisplaySessionService?> _getDisplaySessionService = getDisplaySessionService ?? (static () => null);
    private readonly Func<ILinuxDirectInputQuickSetupService?> _getLinuxDirectInputQuickSetupService = getLinuxDirectInputQuickSetupService ?? (static () => null);

    public async Task<bool> TryHandleAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        DesktopStartupPreferences startupPreferences,
        string? unsupportedSessionReason,
        Func<IClassicDesktopStyleApplicationLifetime, DesktopStartupPreferences, Task> startDesktopRuntimeAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(desktop);
        ArgumentNullException.ThrowIfNull(startDesktopRuntimeAsync);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrWhiteSpace(unsupportedSessionReason))
        {
            var flatpakQuickSetupService = _getFlatpakQuickSetupService();
            if (flatpakQuickSetupService is not null && flatpakQuickSetupService.IsApplicable())
            {
                await HandleFlatpakQuickSetupAsync(desktop, startupPreferences, unsupportedSessionReason, startDesktopRuntimeAsync, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return true;
            }

            ShowUnsupportedSessionDialog(desktop, unsupportedSessionReason, cancellationToken);
            return true;
        }

        var appImageQuickSetupService = _getAppImageQuickSetupService();
        if ((appImageQuickSetupService?.ShouldPrompt()) is true)
        {
            await HandleAppImageQuickSetupAsync(desktop, startupPreferences, startDesktopRuntimeAsync, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return true;
        }

        var directInputQuickSetupService = _getLinuxDirectInputQuickSetupService();
        if (directInputQuickSetupService is not null && await directInputQuickSetupService.ShouldPromptAsync(cancellationToken).ConfigureAwait(false))
        {
            await HandleDaemonFallbackQuickSetupAsync(desktop, startupPreferences, directInputQuickSetupService, startDesktopRuntimeAsync, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return true;
        }

        return false;
    }

    private async Task HandleFlatpakQuickSetupAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        DesktopStartupPreferences startupPreferences,
        string initialReason,
        Func<IClassicDesktopStyleApplicationLifetime, DesktopStartupPreferences, Task> startDesktopRuntimeAsync,
        CancellationToken cancellationToken = default)
    {
        var quickSetupService = _getFlatpakQuickSetupService();
        if (quickSetupService is null)
        {
            ShowUnsupportedSessionDialog(desktop, initialReason, cancellationToken);
            return;
        }

        await DesktopPermissionGateService.RunWithBootstrapOwnerAsync(desktop, async bootstrapOwner =>
        {
            try
            {
                var promptMessage =
                    "CrossMacro cannot access host input devices in Flatpak on Wayland.\n\n" +
                    "Run Quick Setup now?\n\n" +
                    "Quick Setup uses flatpak-spawn and the host polkit authentication agent to request authorization and enable direct device access for your user session.\n\n" +
                    $"Details: {initialReason}";

                var shouldRunSetup = await DesktopPermissionGateService.ShowDialogAsync<bool>(
                    bootstrapOwner,
                    () => DesktopPermissionGateService.CreateCenteredConfirmationDialog(
                        "Wayland Setup Required",
                        promptMessage,
                        "Run Quick Setup",
                        "Exit",
                        dangerYes: false,
                        dangerNo: true)).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (!shouldRunSetup)
                {
                    ShowUnsupportedSessionDialog(desktop, initialReason, cancellationToken);
                    return;
                }

                var setupResult = await quickSetupService.RunAsync(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (!setupResult.Success)
                {
                    ShowQuickSetupFailureDialog(desktop, $"{initialReason}\n\n{setupResult.Message}", cancellationToken);
                    return;
                }

                var displaySessionService = _getDisplaySessionService();
                if (displaySessionService is not null)
                {
                    var sessionSupport = await displaySessionService.IsSessionSupportedAsync(cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!sessionSupport.Supported)
                    {
                        ShowUnsupportedSessionDialog(desktop, sessionSupport.Reason, cancellationToken);
                        return;
                    }
                }

                await startDesktopRuntimeAsync(desktop, startupPreferences).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not OperationCanceledException)
            {
                Log.LogError(ex, "[DesktopStartupCoordinator] Flatpak quick setup flow failed");
                ShowQuickSetupFailureDialog(desktop, "Quick setup failed due to an unexpected error.", cancellationToken);
            }
        }, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task HandleAppImageQuickSetupAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        DesktopStartupPreferences startupPreferences,
        Func<IClassicDesktopStyleApplicationLifetime, DesktopStartupPreferences, Task> startDesktopRuntimeAsync,
        CancellationToken cancellationToken = default)
    {
        var quickSetupService = _getAppImageQuickSetupService();
        if (quickSetupService is null)
        {
            await startDesktopRuntimeAsync(desktop, startupPreferences).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return;
        }

        await DesktopPermissionGateService.RunWithBootstrapOwnerAsync(desktop, async bootstrapOwner =>
        {
            try
            {
                const string promptMessage =
                    "CrossMacro cannot access Linux input devices in this AppImage session.\n\n" +
                    "Run Quick Setup now?\n\n" +
                    "Quick Setup requests host authorization to grant temporary direct device mode access to /dev/uinput and /dev/input/event* for your current user.\n\n" +
                    "These permissions are temporary and may need to be applied again after reboot or device re-enumeration.";

                var shouldRunSetup = await DesktopPermissionGateService.ShowDialogAsync<bool>(
                    bootstrapOwner,
                    () => DesktopPermissionGateService.CreateCenteredConfirmationDialog(
                        "Linux Input Setup Required",
                        promptMessage,
                        "Run Quick Setup",
                        "Continue",
                        dangerYes: false,
                        dangerNo: false)).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (shouldRunSetup)
                {
                    var setupResult = await quickSetupService.RunAsync(cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!setupResult.Success)
                    {
                        _ = await DesktopPermissionGateService.ShowDialogAsync<bool>(
                            bootstrapOwner,
                            () => DesktopPermissionGateService.CreateCenteredConfirmationDialog(
                                "Quick Setup Failed",
                                $"{setupResult.Message}\n\nCrossMacro will continue without temporary device permissions.",
                                "Continue",
                                noText: null,
                                dangerYes: false)).ConfigureAwait(false);
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }

                await startDesktopRuntimeAsync(desktop, startupPreferences).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not OperationCanceledException)
            {
                Log.LogError(ex, "[DesktopStartupCoordinator] AppImage quick setup flow failed");
                await startDesktopRuntimeAsync(desktop, startupPreferences).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static async Task HandleDaemonFallbackQuickSetupAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        DesktopStartupPreferences startupPreferences,
        ILinuxDirectInputQuickSetupService quickSetupService,
        Func<IClassicDesktopStyleApplicationLifetime, DesktopStartupPreferences, Task> startDesktopRuntimeAsync,
        CancellationToken cancellationToken = default)
    {
        await DesktopPermissionGateService.RunWithBootstrapOwnerAsync(desktop, async bootstrapOwner =>
        {
            const string promptMessage =
                "CrossMacro cannot use the input daemon or direct input capture in this session.\n\n" +
                "Run Quick Setup now?\n\n" +
                "Quick Setup requests host authorization to grant temporary direct access to /dev/uinput and /dev/input/event* for your current user. CrossMacro will use this direct mode until you log out and back in. Device reconnection or reboot may require running it again.\n\n" +
                "After signing in again, CrossMacro will try the daemon automatically.";

            var shouldRunSetup = await DesktopPermissionGateService.ShowDialogAsync<bool>(
                bootstrapOwner,
                () => DesktopPermissionGateService.CreateCenteredConfirmationDialog(
                    "Linux Input Setup Required",
                    promptMessage,
                    "Run Quick Setup",
                    "Continue",
                    dangerYes: false,
                    dangerNo: false)).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!shouldRunSetup)
            {
                await startDesktopRuntimeAsync(desktop, startupPreferences).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return;
            }

            var setupResult = await quickSetupService.RunAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!setupResult.Success)
            {
                _ = await DesktopPermissionGateService.ShowDialogAsync<bool>(
                    bootstrapOwner,
                    () => DesktopPermissionGateService.CreateCenteredConfirmationDialog(
                        "Quick Setup Failed",
                        $"{setupResult.Message}\n\nCrossMacro will continue without temporary device permissions.",
                        "Continue",
                        noText: null,
                        dangerYes: false)).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                await startDesktopRuntimeAsync(desktop, startupPreferences).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return;
            }

            await startDesktopRuntimeAsync(desktop, startupPreferences).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }

    internal static void ShowUnsupportedSessionDialog(IClassicDesktopStyleApplicationLifetime desktop, string reason, CancellationToken cancellationToken = default)
        => ShowSessionDialog(desktop, "Unsupported Session", reason, cancellationToken);

    internal static void ShowQuickSetupFailureDialog(IClassicDesktopStyleApplicationLifetime desktop, string reason, CancellationToken cancellationToken = default)
        => ShowSessionDialog(desktop, "Quick Setup Failed", reason, cancellationToken);

    private static void ShowSessionDialog(
        IClassicDesktopStyleApplicationLifetime desktop,
        string title,
        string reason, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(desktop);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (cancellationToken.IsCancellationRequested) { return; }

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => ShowSessionDialog(desktop, title, reason, cancellationToken), DispatcherPriority.Send);
            return;
        }

        desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

        var dialog = new ConfirmationDialog(
            title,
            reason,
            "Exit",
noText: null);

        desktop.MainWindow = dialog;

        if (!dialog.IsVisible)
        {
            dialog.Show();
        }
    }
}
