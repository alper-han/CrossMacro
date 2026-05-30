using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CrossMacro.Core.Logging;
using CrossMacro.Platform.Abstractions;
using CrossMacro.UI.Localization;
using CrossMacro.UI.Views.Dialogs;

namespace CrossMacro.UI.Services;

internal sealed class DesktopPermissionGateService
{
    internal readonly record struct GateResult(bool Handled, string? UnsupportedSessionReason)
    {
        public static GateResult Continue() => new(false, null);
        public static GateResult HandledByDialog() => new(true, null);

        public static GateResult UnsupportedSession(string reason)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(reason);
            return new(false, reason);
        }
    }

    internal enum StartupPermissionGateKind
    {
        None,
        InputMonitoring,
        Accessibility
    }

    private readonly IDisplaySessionService _displaySessionService;
    private readonly Func<IPermissionChecker?> _getPermissionChecker;

    public DesktopPermissionGateService(
        IDisplaySessionService displaySessionService,
        Func<IPermissionChecker?> getPermissionChecker)
    {
        _displaySessionService = displaySessionService ?? throw new ArgumentNullException(nameof(displaySessionService));
        _getPermissionChecker = getPermissionChecker ?? throw new ArgumentNullException(nameof(getPermissionChecker));
    }

    public async Task<GateResult> TryHandleAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        ArgumentNullException.ThrowIfNull(desktop);

        var permissionChecker = _getPermissionChecker();
        var startupGateKind = GetStartupPermissionGateKind(permissionChecker);
        if (startupGateKind != StartupPermissionGateKind.None)
        {
            var permissionResolved = await HandleStartupPermissionGateAsync(desktop, permissionChecker!, startupGateKind);
            if (!permissionResolved)
            {
                return GateResult.HandledByDialog();
            }
        }

        if (!_displaySessionService.IsSessionSupported(out var reason))
        {
            return GateResult.UnsupportedSession(reason);
        }

        return GateResult.Continue();
    }

    internal static bool IsStartupPermissionBlocked(IPermissionChecker? permissionChecker)
    {
        return GetStartupPermissionGateKind(permissionChecker) != StartupPermissionGateKind.None;
    }

    internal static StartupPermissionGateKind GetStartupPermissionGateKind(IPermissionChecker? permissionChecker)
    {
        if (permissionChecker == null || !permissionChecker.IsSupported)
        {
            return StartupPermissionGateKind.None;
        }

        if (!permissionChecker.RequiresStartupPermissionGate)
        {
            return StartupPermissionGateKind.None;
        }

        if (permissionChecker is IMacOSPermissionChecker macOSPermissionChecker)
        {
            if (!macOSPermissionChecker.IsListenEventAccessGranted())
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
            WindowStartupLocation = WindowStartupLocation.CenterScreen
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
            await action(bootstrapOwner);
        }
        finally
        {
            try
            {
                bootstrapOwner.Close();
            }
            catch
            {
                // Ignore close races if owner was already disposed by the windowing backend.
            }
        }
    }

    private async Task<bool> HandleStartupPermissionGateAsync(
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
                while (currentGateKind != StartupPermissionGateKind.None)
                {
                    var permissionDialog = CreateCenteredConfirmationDialog(
                        UIStrings.PermissionRequiredTitle,
                        GetStartupPermissionMessage(currentGateKind),
                        UIStrings.OpenSettingsButton,
                        UIStrings.ExitButton,
                        dangerYes: false,
                        dangerNo: true);

                    var shouldOpenSettings = await permissionDialog.ShowDialog<bool>(bootstrapOwner);
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

                    var shouldRecheck = await recheckDialog.ShowDialog<bool>(bootstrapOwner);
                    if (!shouldRecheck)
                    {
                        return;
                    }

                    currentGateKind = GetStartupPermissionGateKind(permissionChecker);
                    if (currentGateKind != StartupPermissionGateKind.None)
                    {
                        await ShowApprovalPendingDialogAsync(bootstrapOwner, currentGateKind);
                        return;
                    }
                }

                permissionResolved = true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[DesktopStartupCoordinator] macOS startup permission gate flow failed");
            }
        });

        if (!permissionResolved)
        {
            try
            {
                desktop.Shutdown();
            }
            catch (Exception ex)
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
            WindowStartupLocation = WindowStartupLocation.Manual
        };
    }

    private static string GetStartupPermissionMessage(StartupPermissionGateKind gateKind)
    {
        return gateKind == StartupPermissionGateKind.InputMonitoring
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

        await pendingDialog.ShowDialog<bool>(bootstrapOwner);
    }

    private static string GetApprovalPendingMessage(StartupPermissionGateKind gateKind)
    {
        return gateKind == StartupPermissionGateKind.InputMonitoring
            ? UIStrings.MacOSInputMonitoringApprovalPendingMessage
            : UIStrings.MacOSAccessibilityApprovalPendingMessage;
    }

    internal static void OpenStartupPermissionSettings(IPermissionChecker permissionChecker, StartupPermissionGateKind gateKind)
    {
        if (gateKind != StartupPermissionGateKind.InputMonitoring)
        {
            if (permissionChecker is IMacOSPermissionChecker accessibilityPermissionChecker)
            {
                accessibilityPermissionChecker.RequestPermission(MacOSPermissionRequirement.Accessibility);
            }

            permissionChecker.OpenAccessibilitySettings();
            return;
        }

        if (permissionChecker is IMacOSPermissionChecker macOSPermissionChecker)
        {
            macOSPermissionChecker.RequestListenEventAccess();
            macOSPermissionChecker.OpenInputMonitoringSettings();
            return;
        }

        permissionChecker.OpenAccessibilitySettings();
    }
}
