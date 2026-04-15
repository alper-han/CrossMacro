using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CrossMacro.Core.Diagnostics;
using CrossMacro.Core.Services;
using CrossMacro.UI.Startup;
using CrossMacro.UI.ViewModels;
using CrossMacro.UI.Views;

namespace CrossMacro.UI.Services;

internal sealed class DesktopStartupCoordinator : IDesktopStartupCoordinator
{
    private readonly record struct DesktopRuntimeStartResult(Window MainWindow, DesktopStartupDisplayMode DisplayMode);

    private readonly IDisplaySessionService _displaySessionService;
    private readonly Func<ISettingsService> _getSettingsService;
    private readonly Func<IThemeService> _getThemeService;
    private readonly Func<ITrayIconService> _getTrayIconService;
    private readonly Func<ITextExpansionService> _getTextExpansionService;
    private readonly Func<MainWindowViewModel> _getMainWindowViewModel;
    private readonly Func<IFlatpakQuickSetupService?> _getFlatpakQuickSetupService;
    private readonly Func<IAppImageQuickSetupService?> _getAppImageQuickSetupService;
    private readonly Func<IPermissionChecker?> _getPermissionChecker;
    private readonly Func<InputSimulatorPool?> _getInputSimulatorPool;
    private readonly Func<IMousePositionProvider?> _getPositionProvider;
    private readonly GuiStartupOptions _startupOptions;

    public DesktopStartupCoordinator(
        IDisplaySessionService displaySessionService,
        Func<ISettingsService> getSettingsService,
        Func<IThemeService> getThemeService,
        Func<ITrayIconService> getTrayIconService,
        Func<ITextExpansionService> getTextExpansionService,
        Func<MainWindowViewModel> getMainWindowViewModel,
        Func<IFlatpakQuickSetupService?> getFlatpakQuickSetupService,
        Func<IAppImageQuickSetupService?> getAppImageQuickSetupService,
        Func<IPermissionChecker?> getPermissionChecker,
        Func<InputSimulatorPool?> getInputSimulatorPool,
        Func<IMousePositionProvider?> getPositionProvider,
        GuiStartupOptions startupOptions)
    {
        _displaySessionService = displaySessionService ?? throw new ArgumentNullException(nameof(displaySessionService));
        _getSettingsService = getSettingsService ?? throw new ArgumentNullException(nameof(getSettingsService));
        _getThemeService = getThemeService ?? throw new ArgumentNullException(nameof(getThemeService));
        _getTrayIconService = getTrayIconService ?? throw new ArgumentNullException(nameof(getTrayIconService));
        _getTextExpansionService = getTextExpansionService ?? throw new ArgumentNullException(nameof(getTextExpansionService));
        _getMainWindowViewModel = getMainWindowViewModel ?? throw new ArgumentNullException(nameof(getMainWindowViewModel));
        _getFlatpakQuickSetupService = getFlatpakQuickSetupService ?? throw new ArgumentNullException(nameof(getFlatpakQuickSetupService));
        _getAppImageQuickSetupService = getAppImageQuickSetupService ?? throw new ArgumentNullException(nameof(getAppImageQuickSetupService));
        _getPermissionChecker = getPermissionChecker ?? throw new ArgumentNullException(nameof(getPermissionChecker));
        _getInputSimulatorPool = getInputSimulatorPool ?? throw new ArgumentNullException(nameof(getInputSimulatorPool));
        _getPositionProvider = getPositionProvider ?? throw new ArgumentNullException(nameof(getPositionProvider));
        _startupOptions = startupOptions ?? throw new ArgumentNullException(nameof(startupOptions));
    }

    public Task StartAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        ArgumentNullException.ThrowIfNull(desktop);

        var permissionChecker = _getPermissionChecker();
        if (IsStartupPermissionBlocked(permissionChecker))
        {
            return HandleMacAccessibilityPermissionGateAsync(desktop, permissionChecker!);
        }

        if (!_displaySessionService.IsSessionSupported(out var reason))
        {
            var flatpakQuickSetupService = _getFlatpakQuickSetupService();
            if (flatpakQuickSetupService != null && flatpakQuickSetupService.IsApplicable())
            {
                return HandleFlatpakQuickSetupAsync(desktop, reason);
            }

            ShowUnsupportedSessionDialog(desktop, reason);
            return Task.CompletedTask;
        }

        var appImageQuickSetupService = _getAppImageQuickSetupService();
        if (appImageQuickSetupService?.ShouldPrompt() == true)
        {
            return HandleAppImageQuickSetupAsync(desktop);
        }

        StartDesktopRuntime(desktop);
        return Task.CompletedTask;
    }

    private void StartDesktopRuntime(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var startupPreferences = LoadStartupPreferences();
        var startupResult = InitializeDesktopRuntime(desktop, startupPreferences);
        ShowWindowForStartup(startupResult.MainWindow, startupResult.DisplayMode);
    }

    private DesktopStartupPreferences LoadStartupPreferences()
    {
        var settingsService = _getSettingsService();
        settingsService.Load();

        ApplyTheme(settingsService);
        return DesktopStartupPreferences.Resolve(settingsService.Current, _startupOptions);
    }

    private void ApplyTheme(ISettingsService settingsService)
    {
        ArgumentNullException.ThrowIfNull(settingsService);

        var themeService = _getThemeService();
        if (!themeService.TryApplyTheme(settingsService.Current.Theme, out var themeError))
        {
            Serilog.Log.Warning("[App] Theme apply fallback triggered for '{Theme}': {Error}", settingsService.Current.Theme, themeError);
            settingsService.Current.Theme = themeService.CurrentTheme;
            try
            {
                settingsService.Save();
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "[App] Failed to persist fallback theme '{Theme}'", settingsService.Current.Theme);
            }
        }
    }

    private DesktopRuntimeStartResult InitializeDesktopRuntime(
        IClassicDesktopStyleApplicationLifetime desktop,
        DesktopStartupPreferences startupPreferences)
    {
        ArgumentNullException.ThrowIfNull(desktop);

        var mainWindowViewModel = _getMainWindowViewModel();
        var mainWindow = new MainWindow
        {
            DataContext = mainWindowViewModel
        };

        var trayIconService = _getTrayIconService();
        desktop.MainWindow = mainWindow;
        trayIconService.Initialize();

        var inputSimulatorPool = _getInputSimulatorPool();
        if (inputSimulatorPool != null)
        {
            _ = WarmUpInputSimulatorPoolAsync(inputSimulatorPool, _getPositionProvider());
        }

        _getTextExpansionService().Start();
        trayIconService.SetEnabled(startupPreferences.ShouldEnableTrayDuringStartup);
        mainWindowViewModel.TrayIconEnabledChanged += (_, enabled) => trayIconService.SetEnabled(enabled);

        var displayMode = startupPreferences.ResolveDisplayMode(trayIconService.IsAvailable);
        if (displayMode == DesktopStartupDisplayMode.HiddenToTray)
        {
            mainWindow.ShowInTaskbar = false;

            if (startupPreferences.UseStartupTrayOnly)
            {
                DisableStartupOnlyTrayAfterInitialRestore(mainWindow, trayIconService);
            }
        }
        else if (displayMode == DesktopStartupDisplayMode.Minimized)
        {
            mainWindow.ShowActivated = false;
            mainWindow.WindowState = WindowState.Minimized;
        }

        desktop.ShutdownMode = displayMode == DesktopStartupDisplayMode.HiddenToTray
            ? ShutdownMode.OnExplicitShutdown
            : ShutdownMode.OnLastWindowClose;

        switch (displayMode)
        {
            case DesktopStartupDisplayMode.Visible:
                Serilog.Log.Information("[DesktopStartupCoordinator] Started visible.");
                break;
            case DesktopStartupDisplayMode.Minimized:
                Serilog.Log.Information("[DesktopStartupCoordinator] Started minimized.");
                break;
            case DesktopStartupDisplayMode.HiddenToTray:
                Serilog.Log.Information("[DesktopStartupCoordinator] Started hidden to tray.");
                break;
        }

        return new DesktopRuntimeStartResult(mainWindow, displayMode);
    }

    private static void DisableStartupOnlyTrayAfterInitialRestore(Window mainWindow, ITrayIconService trayIconService)
    {
        ArgumentNullException.ThrowIfNull(mainWindow);
        ArgumentNullException.ThrowIfNull(trayIconService);

        void OnOpened(object? sender, EventArgs e)
        {
            mainWindow.Opened -= OnOpened;
            trayIconService.SetEnabled(false);
            Serilog.Log.Information("[DesktopStartupCoordinator] Disabled startup-only tray after initial restore.");
        }

        mainWindow.Opened += OnOpened;
    }

    private static void ShowWindowForStartup(Window mainWindow, DesktopStartupDisplayMode displayMode)
    {
        ArgumentNullException.ThrowIfNull(mainWindow);

        switch (displayMode)
        {
            case DesktopStartupDisplayMode.HiddenToTray:
                return;
            case DesktopStartupDisplayMode.Minimized:
                if (!mainWindow.IsVisible)
                {
                    mainWindow.ShowActivated = false;
                    mainWindow.ShowInTaskbar = true;
                    mainWindow.Show();
                    Dispatcher.UIThread.Post(() => mainWindow.ShowActivated = true);
                }
                return;
            case DesktopStartupDisplayMode.Visible:
                if (!mainWindow.IsVisible)
                {
                    mainWindow.Show();
                }
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(displayMode), displayMode, "Unknown initial display mode.");
        }
    }

    private async Task HandleFlatpakQuickSetupAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        string initialReason)
    {
        var quickSetupService = _getFlatpakQuickSetupService();
        if (quickSetupService == null)
        {
            ShowUnsupportedSessionDialog(desktop, initialReason);
            return;
        }

        await RunWithBootstrapOwnerAsync(desktop, async bootstrapOwner =>
        {
            try
            {
                var promptMessage =
                    "CrossMacro cannot access host input devices in Flatpak on Wayland.\n\n" +
                    "Run Quick Setup now?\n\n" +
                    "Quick Setup uses flatpak-spawn + pkexec to apply session permissions for your user.\n\n" +
                    $"Details: {initialReason}";

                var setupDialog = CreateCenteredConfirmationDialog(
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

                if (!_displaySessionService.IsSessionSupported(out var reasonAfterSetup))
                {
                    ShowUnsupportedSessionDialog(desktop, reasonAfterSetup);
                    return;
                }

                StartDesktopRuntime(desktop);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[DesktopStartupCoordinator] Flatpak quick setup flow failed");
                ShowUnsupportedSessionDialog(desktop, "Quick setup failed due to an unexpected error.");
            }
        });
    }

    private async Task HandleAppImageQuickSetupAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var quickSetupService = _getAppImageQuickSetupService();
        if (quickSetupService == null)
        {
            StartDesktopRuntime(desktop);
            return;
        }

        await RunWithBootstrapOwnerAsync(desktop, async bootstrapOwner =>
        {
            try
            {
                var promptMessage =
                    "CrossMacro cannot access Linux input devices in this AppImage session.\n\n" +
                    "Run Quick Setup now?\n\n" +
                    "Quick Setup uses pkexec to grant temporary access to /dev/uinput and /dev/input/event* for your current user.\n\n" +
                    "These permissions are temporary and may need to be applied again after reboot or device re-enumeration.";

                var setupDialog = CreateCenteredConfirmationDialog(
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
                        var failureDialog = CreateCenteredConfirmationDialog(
                            "Quick Setup Failed",
                            $"{setupResult.Message}\n\nCrossMacro will continue without temporary device permissions.",
                            "Continue",
                            null,
                            dangerYes: false);

                        await failureDialog.ShowDialog<bool>(bootstrapOwner);
                    }
                }

                StartDesktopRuntime(desktop);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[DesktopStartupCoordinator] AppImage quick setup flow failed");
                StartDesktopRuntime(desktop);
            }
        });
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
                Serilog.Log.Error(ex, "[DesktopStartupCoordinator] macOS accessibility permission gate flow failed");
            }
        });

        try
        {
            desktop.Shutdown();
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "[DesktopStartupCoordinator] Failed to shutdown app after macOS permission gate");
        }
    }

    private static bool IsStartupPermissionBlocked(IPermissionChecker? permissionChecker)
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

    private static CrossMacro.UI.Views.Dialogs.ConfirmationDialog CreateCenteredConfirmationDialog(
        string title,
        string message,
        string yesText,
        string? noText,
        bool dangerYes = false,
        bool dangerNo = false)
    {
        return new CrossMacro.UI.Views.Dialogs.ConfirmationDialog(
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

    private static async Task RunWithBootstrapOwnerAsync(
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

    private static async Task WarmUpInputSimulatorPoolAsync(
        InputSimulatorPool simulatorPool,
        IMousePositionProvider? positionProvider)
    {
        try
        {
            var width = 0;
            var height = 0;

            if (positionProvider?.IsSupported == true)
            {
                var resolution = await positionProvider.GetScreenResolutionAsync();
                if (resolution.HasValue)
                {
                    width = resolution.Value.Width;
                    height = resolution.Value.Height;
                }
            }

            await simulatorPool.WarmUpAsync(width, height);
        }
        catch (Exception ex)
        {
            if (InputBackendErrorClassifier.IsKnownUnavailable(ex))
            {
                Serilog.Log.Warning("[DesktopStartupCoordinator] Input simulator warm-up skipped: {Error}", ex.Message);
                return;
            }

            Serilog.Log.Error(ex, "[DesktopStartupCoordinator] Failed to warm up InputSimulatorPool");
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
            SystemDecorations = SystemDecorations.None,
            WindowStartupLocation = WindowStartupLocation.Manual
        };
    }

    private static void ShowUnsupportedSessionDialog(IClassicDesktopStyleApplicationLifetime desktop, string reason)
    {
        desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

        var dialog = new CrossMacro.UI.Views.Dialogs.ConfirmationDialog(
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
