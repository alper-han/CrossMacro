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
        if (IsStartupPermissionBlocked(permissionChecker))
        {
            await HandleMacAccessibilityPermissionGateAsync(desktop, permissionChecker!);
            return GateResult.HandledByDialog();
        }

        if (!_displaySessionService.IsSessionSupported(out var reason))
        {
            return GateResult.UnsupportedSession(reason);
        }

        return GateResult.Continue();
    }

    internal static bool IsStartupPermissionBlocked(IPermissionChecker? permissionChecker)
    {
        if (permissionChecker == null || !permissionChecker.IsSupported)
        {
            return false;
        }

        if (!permissionChecker.RequiresStartupPermissionGate)
        {
            return false;
        }

        return !permissionChecker.IsAccessibilityTrusted();
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

    private async Task HandleMacAccessibilityPermissionGateAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        IPermissionChecker permissionChecker)
    {
        await RunWithBootstrapOwnerAsync(desktop, async bootstrapOwner =>
        {
            try
            {
                var permissionDialog = CreateCenteredConfirmationDialog(
                    UIStrings.PermissionRequiredTitle,
                    UIStrings.MacOSAccessibilityStartupBlockMessage,
                    UIStrings.OpenSettingsButton,
                    UIStrings.ExitButton,
                    dangerYes: false,
                    dangerNo: true);

                var shouldOpenSettings = await permissionDialog.ShowDialog<bool>(bootstrapOwner);
                if (shouldOpenSettings)
                {
                    permissionChecker.OpenAccessibilitySettings();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[DesktopStartupCoordinator] macOS accessibility permission gate flow failed");
            }
        });

        try
        {
            desktop.Shutdown();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[DesktopStartupCoordinator] Failed to shutdown app after macOS permission gate");
        }
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
}
