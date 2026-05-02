using System;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CrossMacro.Core.Logging;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using CrossMacro.UI.Startup;
using CrossMacro.UI.ViewModels;
using CrossMacro.UI.Views;

namespace CrossMacro.UI.Services;

internal sealed class DesktopStartupRuntimeService
{
    internal readonly record struct DesktopStartupDisplayPlan(
        DesktopStartupDisplayMode DisplayMode,
        ShutdownMode ShutdownMode,
        bool ShowInTaskbar,
        bool ShowActivated,
        WindowState WindowState,
        bool DisableStartupOnlyTrayAfterInitialRestore);

    private readonly Func<MainWindow> _getMainWindow;
    private readonly Func<ITrayIconService> _getTrayIconService;
    private readonly Func<ITextExpansionService> _getTextExpansionService;
    private readonly Func<MainWindowViewModel> _getMainWindowViewModel;
    private readonly Func<InputSimulatorPool?> _getInputSimulatorPool;
    private readonly Func<IMousePositionProvider?> _getPositionProvider;
    private readonly IDesktopLifetimeContext _desktopLifetimeContext;
    private readonly InputSimulatorWarmupService _inputSimulatorWarmupService;

    public DesktopStartupRuntimeService(
        Func<MainWindow> getMainWindow,
        Func<ITrayIconService> getTrayIconService,
        Func<ITextExpansionService> getTextExpansionService,
        Func<MainWindowViewModel> getMainWindowViewModel,
        Func<InputSimulatorPool?> getInputSimulatorPool,
        Func<IMousePositionProvider?> getPositionProvider,
        IDesktopLifetimeContext desktopLifetimeContext,
        InputSimulatorWarmupService inputSimulatorWarmupService)
    {
        _getMainWindow = getMainWindow ?? throw new ArgumentNullException(nameof(getMainWindow));
        _getTrayIconService = getTrayIconService ?? throw new ArgumentNullException(nameof(getTrayIconService));
        _getTextExpansionService = getTextExpansionService ?? throw new ArgumentNullException(nameof(getTextExpansionService));
        _getMainWindowViewModel = getMainWindowViewModel ?? throw new ArgumentNullException(nameof(getMainWindowViewModel));
        _getInputSimulatorPool = getInputSimulatorPool ?? throw new ArgumentNullException(nameof(getInputSimulatorPool));
        _getPositionProvider = getPositionProvider ?? throw new ArgumentNullException(nameof(getPositionProvider));
        _desktopLifetimeContext = desktopLifetimeContext ?? throw new ArgumentNullException(nameof(desktopLifetimeContext));
        _inputSimulatorWarmupService = inputSimulatorWarmupService ?? throw new ArgumentNullException(nameof(inputSimulatorWarmupService));
    }

    public void Start(
        IClassicDesktopStyleApplicationLifetime desktop,
        DesktopStartupPreferences startupPreferences)
    {
        ArgumentNullException.ThrowIfNull(desktop);

        var mainWindowViewModel = _getMainWindowViewModel();
        var mainWindow = _getMainWindow();
        mainWindow.DataContext = mainWindowViewModel;

        var trayIconService = _getTrayIconService();
        PublishMainWindow(desktop, mainWindow);
        trayIconService.Initialize();

        var inputSimulatorPool = _getInputSimulatorPool();
        if (inputSimulatorPool != null)
        {
            _ = _inputSimulatorWarmupService.WarmUpAsync(inputSimulatorPool, _getPositionProvider());
        }

        _getTextExpansionService().Start();
        trayIconService.SetEnabled(startupPreferences.ShouldEnableTrayDuringStartup);
        mainWindowViewModel.TrayIconEnabledChanged += (_, enabled) => trayIconService.SetEnabled(enabled);

        var displayMode = ConfigureMainWindow(desktop, mainWindow, startupPreferences, trayIconService);
        ShowWindowForStartup(mainWindow, displayMode);
    }

    internal void PublishMainWindow(IClassicDesktopStyleApplicationLifetime desktop, Window mainWindow)
    {
        ArgumentNullException.ThrowIfNull(desktop);
        ArgumentNullException.ThrowIfNull(mainWindow);

        if (!ReferenceEquals(_desktopLifetimeContext.DesktopLifetime, desktop))
        {
            _desktopLifetimeContext.Attach(desktop);
        }

        _desktopLifetimeContext.SetMainWindow(mainWindow);
    }

    internal DesktopStartupDisplayMode ConfigureMainWindow(
        IClassicDesktopStyleApplicationLifetime desktop,
        Window mainWindow,
        DesktopStartupPreferences startupPreferences,
        ITrayIconService trayIconService)
    {
        ArgumentNullException.ThrowIfNull(desktop);
        ArgumentNullException.ThrowIfNull(mainWindow);
        ArgumentNullException.ThrowIfNull(trayIconService);

        var plan = CreateDisplayPlan(startupPreferences, trayIconService.IsAvailable);

        mainWindow.ShowInTaskbar = plan.ShowInTaskbar;
        mainWindow.ShowActivated = plan.ShowActivated;
        mainWindow.WindowState = plan.WindowState;
        desktop.ShutdownMode = plan.ShutdownMode;

        if (plan.DisableStartupOnlyTrayAfterInitialRestore)
        {
            DisableStartupOnlyTrayAfterInitialRestore(mainWindow, trayIconService);
        }

        switch (plan.DisplayMode)
        {
            case DesktopStartupDisplayMode.Visible:
                Log.Information("[DesktopStartupCoordinator] Started visible.");
                break;
            case DesktopStartupDisplayMode.Minimized:
                Log.Information("[DesktopStartupCoordinator] Started minimized.");
                break;
            case DesktopStartupDisplayMode.HiddenToTray:
                Log.Information("[DesktopStartupCoordinator] Started hidden to tray.");
                break;
        }

        return plan.DisplayMode;
    }

    internal DesktopStartupDisplayPlan CreateDisplayPlan(
        DesktopStartupPreferences startupPreferences,
        bool trayAvailable)
    {
        var displayMode = startupPreferences.ResolveDisplayMode(trayAvailable);

        return displayMode switch
        {
            DesktopStartupDisplayMode.Visible => new DesktopStartupDisplayPlan(
                DisplayMode: displayMode,
                ShutdownMode: ShutdownMode.OnLastWindowClose,
                ShowInTaskbar: true,
                ShowActivated: true,
                WindowState: WindowState.Normal,
                DisableStartupOnlyTrayAfterInitialRestore: false),
            DesktopStartupDisplayMode.Minimized => new DesktopStartupDisplayPlan(
                DisplayMode: displayMode,
                ShutdownMode: ShutdownMode.OnLastWindowClose,
                ShowInTaskbar: true,
                ShowActivated: false,
                WindowState: WindowState.Minimized,
                DisableStartupOnlyTrayAfterInitialRestore: false),
            DesktopStartupDisplayMode.HiddenToTray => new DesktopStartupDisplayPlan(
                DisplayMode: displayMode,
                ShutdownMode: ShutdownMode.OnExplicitShutdown,
                ShowInTaskbar: false,
                ShowActivated: true,
                WindowState: WindowState.Normal,
                DisableStartupOnlyTrayAfterInitialRestore: startupPreferences.UseStartupTrayOnly),
            _ => throw new ArgumentOutOfRangeException(nameof(displayMode), displayMode, "Unknown initial display mode.")
        };
    }

    private static void DisableStartupOnlyTrayAfterInitialRestore(Window mainWindow, ITrayIconService trayIconService)
    {
        ArgumentNullException.ThrowIfNull(mainWindow);
        ArgumentNullException.ThrowIfNull(trayIconService);

        void OnOpened(object? sender, EventArgs e)
        {
            mainWindow.Opened -= OnOpened;
            trayIconService.SetEnabled(false);
            Log.Information("[DesktopStartupCoordinator] Disabled startup-only tray after initial restore.");
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
}
