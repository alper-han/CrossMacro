
namespace CrossMacro.UI.Services;

internal sealed class DesktopQuickSetupGateService(
    Func<IFlatpakQuickSetupService?> getFlatpakQuickSetupService,
    Func<IAppImageQuickSetupService?> getAppImageQuickSetupService,
    Func<IDisplaySessionService?>? getDisplaySessionService = null)
{
    private readonly Func<IFlatpakQuickSetupService?> _getFlatpakQuickSetupService = getFlatpakQuickSetupService ?? throw new ArgumentNullException(nameof(getFlatpakQuickSetupService));
    private readonly Func<IAppImageQuickSetupService?> _getAppImageQuickSetupService = getAppImageQuickSetupService ?? throw new ArgumentNullException(nameof(getAppImageQuickSetupService));
    private readonly Func<IDisplaySessionService?> _getDisplaySessionService = getDisplaySessionService ?? (static () => null);

    public async Task<bool> TryHandleAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        DesktopStartupPreferences startupPreferences,
        string? unsupportedSessionReason,
        Func<IClassicDesktopStyleApplicationLifetime, DesktopStartupPreferences, Task> startDesktopRuntimeAsync)
    {
        ArgumentNullException.ThrowIfNull(desktop);
        ArgumentNullException.ThrowIfNull(startDesktopRuntimeAsync);

        if (!string.IsNullOrWhiteSpace(unsupportedSessionReason))
        {
            var flatpakQuickSetupService = _getFlatpakQuickSetupService();
            if (flatpakQuickSetupService is not null && flatpakQuickSetupService.IsApplicable())
            {
                await HandleFlatpakQuickSetupAsync(desktop, startupPreferences, unsupportedSessionReason, startDesktopRuntimeAsync).ConfigureAwait(false);
                return true;
            }

            ShowUnsupportedSessionDialog(desktop, unsupportedSessionReason);
            return true;
        }

        var appImageQuickSetupService = _getAppImageQuickSetupService();
        if ((appImageQuickSetupService?.ShouldPrompt()) is true)
        {
            await HandleAppImageQuickSetupAsync(desktop, startupPreferences, startDesktopRuntimeAsync).ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private async Task HandleFlatpakQuickSetupAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        DesktopStartupPreferences startupPreferences,
        string initialReason,
        Func<IClassicDesktopStyleApplicationLifetime, DesktopStartupPreferences, Task> startDesktopRuntimeAsync)
    {
        var quickSetupService = _getFlatpakQuickSetupService();
        if (quickSetupService is null)
        {
            ShowUnsupportedSessionDialog(desktop, initialReason);
            return;
        }

        await DesktopPermissionGateService.RunWithBootstrapOwnerAsync(desktop, async bootstrapOwner =>
        {
            try
            {
                var promptMessage =
                    "CrossMacro cannot access host input devices in Flatpak on Wayland.\n\n" +
                    "Run Quick Setup now?\n\n" +
                    "Quick Setup uses flatpak-spawn to request host authorization and enable direct device access for your user session.\n\n" +
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
                if (!shouldRunSetup)
                {
                    ShowUnsupportedSessionDialog(desktop, initialReason);
                    return;
                }

                var setupResult = await quickSetupService.RunAsync(default).ConfigureAwait(false);
                if (!setupResult.Success)
                {
                    ShowUnsupportedSessionDialog(desktop, $"{initialReason}\n\n{setupResult.Message}");
                    return;
                }

                var displaySessionService = _getDisplaySessionService();
                if (displaySessionService is not null)
                {
                    var sessionSupport = await displaySessionService.IsSessionSupportedAsync(CancellationToken.None).ConfigureAwait(false);
                    if (!sessionSupport.Supported)
                    {
                        ShowUnsupportedSessionDialog(desktop, sessionSupport.Reason);
                        return;
                    }
                }

                await startDesktopRuntimeAsync(desktop, startupPreferences).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.LogError(ex, "[DesktopStartupCoordinator] Flatpak quick setup flow failed");
                ShowUnsupportedSessionDialog(desktop, "Quick setup failed due to an unexpected error.");
            }
        }).ConfigureAwait(false);
    }

    private async Task HandleAppImageQuickSetupAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        DesktopStartupPreferences startupPreferences,
        Func<IClassicDesktopStyleApplicationLifetime, DesktopStartupPreferences, Task> startDesktopRuntimeAsync)
    {
        var quickSetupService = _getAppImageQuickSetupService();
        if (quickSetupService is null)
        {
            await startDesktopRuntimeAsync(desktop, startupPreferences).ConfigureAwait(false);
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
                if (shouldRunSetup)
                {
                    var setupResult = await quickSetupService.RunAsync(default).ConfigureAwait(false);
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
                    }
                }

                await startDesktopRuntimeAsync(desktop, startupPreferences).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.LogError(ex, "[DesktopStartupCoordinator] AppImage quick setup flow failed");
                await startDesktopRuntimeAsync(desktop, startupPreferences).ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
    }

    internal static void ShowUnsupportedSessionDialog(IClassicDesktopStyleApplicationLifetime desktop, string reason)
    {
        ArgumentNullException.ThrowIfNull(desktop);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => ShowUnsupportedSessionDialog(desktop, reason), DispatcherPriority.Send);
            return;
        }

        desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

        var dialog = new ConfirmationDialog(
            "Unsupported Session",
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
