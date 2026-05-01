using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CrossMacro.Core.Logging;
using CrossMacro.Packaging.Abstractions;
using CrossMacro.UI.Startup;
using CrossMacro.UI.Views.Dialogs;

namespace CrossMacro.UI.Services;

internal sealed class DesktopQuickSetupGateService
{
    private readonly Func<IFlatpakQuickSetupService?> _getFlatpakQuickSetupService;
    private readonly Func<IAppImageQuickSetupService?> _getAppImageQuickSetupService;

    public DesktopQuickSetupGateService(
        Func<IFlatpakQuickSetupService?> getFlatpakQuickSetupService,
        Func<IAppImageQuickSetupService?> getAppImageQuickSetupService)
    {
        _getFlatpakQuickSetupService = getFlatpakQuickSetupService ?? throw new ArgumentNullException(nameof(getFlatpakQuickSetupService));
        _getAppImageQuickSetupService = getAppImageQuickSetupService ?? throw new ArgumentNullException(nameof(getAppImageQuickSetupService));
    }

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
            if (flatpakQuickSetupService != null && flatpakQuickSetupService.IsApplicable())
            {
                await HandleFlatpakQuickSetupAsync(desktop, startupPreferences, unsupportedSessionReason, startDesktopRuntimeAsync);
                return true;
            }

            ShowUnsupportedSessionDialog(desktop, unsupportedSessionReason);
            return true;
        }

        var appImageQuickSetupService = _getAppImageQuickSetupService();
        if (appImageQuickSetupService?.ShouldPrompt() == true)
        {
            await HandleAppImageQuickSetupAsync(desktop, startupPreferences, startDesktopRuntimeAsync);
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
        if (quickSetupService == null)
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
                    "Quick Setup uses flatpak-spawn + pkexec to enable the direct device mode fallback for your user session.\n\n" +
                    $"Details: {initialReason}";

                var setupDialog = DesktopPermissionGateService.CreateCenteredConfirmationDialog(
                    "Wayland Setup Required",
                    promptMessage,
                    "Run Quick Setup",
                    "Exit",
                    dangerYes: false,
                    dangerNo: true);

                var shouldRunSetup = await setupDialog.ShowDialog<bool>(bootstrapOwner);
                if (!shouldRunSetup)
                {
                    ShowUnsupportedSessionDialog(desktop, initialReason);
                    return;
                }

                var setupResult = await quickSetupService.RunAsync();
                if (!setupResult.Success)
                {
                    ShowUnsupportedSessionDialog(desktop, $"{initialReason}\n\n{setupResult.Message}");
                    return;
                }

                await startDesktopRuntimeAsync(desktop, startupPreferences);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[DesktopStartupCoordinator] Flatpak quick setup flow failed");
                ShowUnsupportedSessionDialog(desktop, "Quick setup failed due to an unexpected error.");
            }
        });
    }

    private async Task HandleAppImageQuickSetupAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        DesktopStartupPreferences startupPreferences,
        Func<IClassicDesktopStyleApplicationLifetime, DesktopStartupPreferences, Task> startDesktopRuntimeAsync)
    {
        var quickSetupService = _getAppImageQuickSetupService();
        if (quickSetupService == null)
        {
            await startDesktopRuntimeAsync(desktop, startupPreferences);
            return;
        }

        await DesktopPermissionGateService.RunWithBootstrapOwnerAsync(desktop, async bootstrapOwner =>
        {
            try
            {
                var promptMessage =
                    "CrossMacro cannot access Linux input devices in this AppImage session.\n\n" +
                    "Run Quick Setup now?\n\n" +
                    "Quick Setup uses pkexec to grant temporary direct device mode access to /dev/uinput and /dev/input/event* for your current user.\n\n" +
                    "These permissions are temporary and may need to be applied again after reboot or device re-enumeration.";

                var setupDialog = DesktopPermissionGateService.CreateCenteredConfirmationDialog(
                    "Linux Input Setup Required",
                    promptMessage,
                    "Run Quick Setup",
                    "Continue",
                    dangerYes: false,
                    dangerNo: false);

                var shouldRunSetup = await setupDialog.ShowDialog<bool>(bootstrapOwner);
                if (shouldRunSetup)
                {
                    var setupResult = await quickSetupService.RunAsync();
                    if (!setupResult.Success)
                    {
                        var failureDialog = DesktopPermissionGateService.CreateCenteredConfirmationDialog(
                            "Quick Setup Failed",
                            $"{setupResult.Message}\n\nCrossMacro will continue without temporary device permissions.",
                            "Continue",
                            null,
                            dangerYes: false);

                        await failureDialog.ShowDialog<bool>(bootstrapOwner);
                    }
                }

                await startDesktopRuntimeAsync(desktop, startupPreferences);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[DesktopStartupCoordinator] AppImage quick setup flow failed");
                await startDesktopRuntimeAsync(desktop, startupPreferences);
            }
        });
    }

    internal static void ShowUnsupportedSessionDialog(IClassicDesktopStyleApplicationLifetime desktop, string reason)
    {
        desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

        var dialog = new ConfirmationDialog(
            "Unsupported Session",
            reason,
            "Exit",
            null);

        desktop.MainWindow = dialog;

        if (!dialog.IsVisible)
        {
            dialog.Show();
        }
    }
}
