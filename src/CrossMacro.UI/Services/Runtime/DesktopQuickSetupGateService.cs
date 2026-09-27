
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
        Func<IClassicDesktopStyleApplicationLifetime, DesktopStartupPreferences, bool, Task> startDesktopRuntimeAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(desktop);
        ArgumentNullException.ThrowIfNull(startDesktopRuntimeAsync);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrWhiteSpace(unsupportedSessionReason))
        {
            var flatpak = _getFlatpakQuickSetupService();
            if (flatpak is not null && flatpak.IsApplicable())
            {
                await HandleQuickSetupAsync(desktop, startupPreferences,
                    "Wayland Setup Required",
                    "CrossMacro cannot access host input devices in Flatpak on Wayland.\n\n" +
                    "Run Quick Setup now?\n\n" +
                    "Quick Setup uses flatpak-spawn and the host authentication agent to request authorization and enable direct device access for your user session.\n\n" +
                    $"Details: {unsupportedSessionReason}",
                    flatpak.RunAsync, allowWithoutAutomation: false, startDesktopRuntimeAsync, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                ShowUnsupportedSessionDialog(desktop, unsupportedSessionReason, cancellationToken);
            }
            return true;
        }

        var appImage = _getAppImageQuickSetupService();
        if (appImage?.ShouldPrompt() is true)
        {
            await HandleQuickSetupAsync(desktop, startupPreferences,
                "Linux Input Setup Required",
                "CrossMacro cannot access Linux input devices in this AppImage session.\n\n" +
                "Run Quick Setup now?\n\n" +
                "Quick Setup requests host authorization to grant temporary direct device access to /dev/uinput and /dev/input/event* for your current user.\n\n" +
                "These permissions are temporary and may need to be applied again after reboot or device re-enumeration.",
                appImage.RunAsync, allowWithoutAutomation: true, startDesktopRuntimeAsync, cancellationToken).ConfigureAwait(false);
            return true;
        }

        var directInput = _getLinuxDirectInputQuickSetupService();
        if (directInput is not null && await directInput.ShouldPromptAsync(cancellationToken).ConfigureAwait(false))
        {
            await HandleQuickSetupAsync(desktop, startupPreferences,
                "Linux Input Setup Required",
                "CrossMacro cannot use the input daemon or direct input capture in this session.\n\n" +
                "Run Quick Setup now?\n\n" +
                "Quick Setup requests host authorization to grant temporary direct access to /dev/uinput and /dev/input/event* for your current user. Device reconnection or reboot may require running it again.",
                directInput.RunAsync, allowWithoutAutomation: true, startDesktopRuntimeAsync, cancellationToken).ConfigureAwait(false);
            return true;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return false;
    }

    private async Task HandleQuickSetupAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        DesktopStartupPreferences startupPreferences,
        string title,
        string message,
        Func<CancellationToken, Task<QuickSetupResult>> runSetupAsync,
        bool allowWithoutAutomation,
        Func<IClassicDesktopStyleApplicationLifetime, DesktopStartupPreferences, bool, Task> startDesktopRuntimeAsync,
        CancellationToken cancellationToken)
    {
        await DesktopPermissionGateService.RunWithBootstrapOwnerAsync(desktop, async owner =>
        {
            var actionText = "Run Quick Setup";
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Nullable results distinguish closing the window from an explicit Continue button.
                var choice = await DesktopPermissionGateService.ShowDialogAsync<bool?>(owner,
                    () =>
                    {
                        var dialog = DesktopPermissionGateService.CreateCenteredConfirmationDialog(
                            title,
                            allowWithoutAutomation
                                ? $"{message}\n\nContinue without automation opens the app without starting input services automatically."
                                : message,
                            actionText,
                            allowWithoutAutomation ? "Continue without automation" : "Exit",
                            dangerYes: false,
                            dangerNo: !allowWithoutAutomation);
                        dialog.Width = 450;
                        return dialog;
                    }).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                if (choice is not true)
                {
                    if (choice is false && allowWithoutAutomation)
                    {
                        await startDesktopRuntimeAsync(desktop, startupPreferences, false).ConfigureAwait(false);
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    else
                    {
                        await Dispatcher.UIThread.InvokeAsync(() => desktop.Shutdown());
                    }
                    return;
                }

                QuickSetupResult result;
                try
                {
                    result = await runSetupAsync(cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (result.Success && !allowWithoutAutomation && _getDisplaySessionService() is { } displaySession)
                    {
                        var support = await displaySession.IsSessionSupportedAsync(cancellationToken).ConfigureAwait(false);
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!support.Supported)
                        {
                            result = new QuickSetupResult(QuickSetupOutcome.DeviceAccessUnavailable, support.Reason);
                        }
                    }
                }
                catch (Exception ex) when (ex is not OutOfMemoryException and not OperationCanceledException)
                {
                    Log.LogError(ex, "[DesktopStartupCoordinator] Quick setup failed");
                    result = new QuickSetupResult(QuickSetupOutcome.Failed, "Quick setup failed due to an unexpected error.");
                }

                if (result.Success)
                {
                    await startDesktopRuntimeAsync(desktop, startupPreferences, true).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    return;
                }

                title = result.Outcome is QuickSetupOutcome.Cancelled ? "Quick Setup Cancelled" : "Quick Setup Failed";
                message = result.Message;
                actionText = "Retry";
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    internal static void ShowUnsupportedSessionDialog(IClassicDesktopStyleApplicationLifetime desktop, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(desktop);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (cancellationToken.IsCancellationRequested) { return; }
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => ShowUnsupportedSessionDialog(desktop, reason, cancellationToken), DispatcherPriority.Send);
            return;
        }

        desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
        var dialog = new ConfirmationDialog("Unsupported Session", reason, "Exit", noText: null);
        desktop.MainWindow = dialog;
        dialog.Show();
    }
}
