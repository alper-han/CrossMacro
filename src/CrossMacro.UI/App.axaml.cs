using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using CrossMacro.Core.Services;
using CrossMacro.UI.ViewModels;
using CrossMacro.UI.Views;
using CrossMacro.UI.DependencyInjection;
using CrossMacro.UI.Services;

namespace CrossMacro.UI;

public partial class App : Application
{
    private sealed class AppRuntimeServices
    {
        private readonly Lazy<IDisplaySessionService> _displaySessionService;
        private readonly Lazy<ISettingsService> _settingsService;
        private readonly Lazy<IThemeService> _themeService;
        private readonly Lazy<ITrayIconService> _trayIconService;
        private readonly Lazy<ITextExpansionService> _textExpansionService;
        private readonly Lazy<IFlatpakQuickSetupService?> _flatpakQuickSetupService;
        private readonly Lazy<IPermissionChecker?> _permissionChecker;
        private readonly Lazy<InputSimulatorPool?> _inputSimulatorPool;
        private readonly Lazy<IMousePositionProvider?> _positionProvider;
        private readonly Lazy<MainWindowViewModel> _mainWindowViewModel;

        public AppRuntimeServices(IServiceProvider serviceProvider)
        {
            _displaySessionService = new Lazy<IDisplaySessionService>(() => serviceProvider.GetRequiredService<IDisplaySessionService>());
            _settingsService = new Lazy<ISettingsService>(() => serviceProvider.GetRequiredService<ISettingsService>());
            _themeService = new Lazy<IThemeService>(() => serviceProvider.GetRequiredService<IThemeService>());
            _trayIconService = new Lazy<ITrayIconService>(() => serviceProvider.GetRequiredService<ITrayIconService>());
            _textExpansionService = new Lazy<ITextExpansionService>(() => serviceProvider.GetRequiredService<ITextExpansionService>());
            _flatpakQuickSetupService = new Lazy<IFlatpakQuickSetupService?>(() => serviceProvider.GetService<IFlatpakQuickSetupService>());
            _permissionChecker = new Lazy<IPermissionChecker?>(() => serviceProvider.GetService<IPermissionChecker>());
            _inputSimulatorPool = new Lazy<InputSimulatorPool?>(() => serviceProvider.GetService<InputSimulatorPool>());
            _positionProvider = new Lazy<IMousePositionProvider?>(() => serviceProvider.GetService<IMousePositionProvider>());
            _mainWindowViewModel = new Lazy<MainWindowViewModel>(() => serviceProvider.GetRequiredService<MainWindowViewModel>());
        }

        public IDisplaySessionService DisplaySessionService => _displaySessionService.Value;
        public ISettingsService SettingsService => _settingsService.Value;
        public IThemeService ThemeService => _themeService.Value;
        public ITrayIconService TrayIconService => _trayIconService.Value;
        public ITextExpansionService TextExpansionService => _textExpansionService.Value;
        public IFlatpakQuickSetupService? FlatpakQuickSetupService => _flatpakQuickSetupService.Value;
        public IPermissionChecker? PermissionChecker => _permissionChecker.Value;
        public InputSimulatorPool? InputSimulatorPool => _inputSimulatorPool.Value;
        public IMousePositionProvider? PositionProvider => _positionProvider.Value;

        public MainWindowViewModel ResolveMainWindowViewModel()
        {
            return _mainWindowViewModel.Value;
        }
    }

    internal static IPlatformServiceRegistrar? PlatformServiceRegistrar { get; set; }
    private IServiceProvider? _serviceProvider;
    public IServiceProvider? Services => _serviceProvider;
    
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        ConfigureServices();
    }
    
    private void ConfigureServices()
    {
        if (PlatformServiceRegistrar == null)
        {
            // Allow tooling/design-time hosts to construct App without a platform host project.
            _serviceProvider = new ServiceCollection().BuildServiceProvider();
            return;
        }

        var services = new ServiceCollection();
        services.AddCrossMacroServices(PlatformServiceRegistrar);
        _serviceProvider = services.BuildServiceProvider();
    }
    
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (!Design.IsDesignMode && PlatformServiceRegistrar == null)
            {
                throw new InvalidOperationException(
                    "Platform service registrar is not configured. Start the app via a platform host project.");
            }

            DisableAvaloniaDataAnnotationValidation();
            
            if (_serviceProvider == null)
            {
                throw new InvalidOperationException("Service provider is not initialized");
            }

            var runtimeServices = new AppRuntimeServices(_serviceProvider);

            // Startup permission gates are platform-defined (currently macOS accessibility).
            if (IsStartupPermissionBlocked(runtimeServices.PermissionChecker))
            {
                _ = HandleMacAccessibilityPermissionGateAsync(desktop, runtimeServices.PermissionChecker!);
            }
            // Check if current display session is supported (Wayland guard for Flatpak)
            else if (!runtimeServices.DisplaySessionService.IsSessionSupported(out var reason))
            {
                var quickSetupService = runtimeServices.FlatpakQuickSetupService;
                if (quickSetupService != null && quickSetupService.IsApplicable())
                {
                    _ = HandleFlatpakQuickSetupAsync(desktop, runtimeServices, reason);
                }
                else
                {
                    ShowUnsupportedSessionDialog(desktop, reason);
                }
            }
            else
            {
                InitializeDesktopRuntime(desktop, runtimeServices);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void InitializeDesktopRuntime(
        IClassicDesktopStyleApplicationLifetime desktop,
        AppRuntimeServices runtimeServices)
    {
        var settingsService = runtimeServices.SettingsService;
        settingsService.Load();
        var themeService = runtimeServices.ThemeService;
        if (!themeService.TryApplyTheme(settingsService.Current.Theme, out var themeError))
        {
            Serilog.Log.Warning("[App] Theme apply fallback triggered for '{Theme}': {Error}", settingsService.Current.Theme, themeError);
            settingsService.Current.Theme = themeService.CurrentTheme;
            _ = settingsService.SaveAsync();
        }

        var viewModel = runtimeServices.ResolveMainWindowViewModel();

        desktop.MainWindow = new MainWindow
        {
            DataContext = viewModel
        };

        var trayIconService = runtimeServices.TrayIconService;
        trayIconService.Initialize();

        // Warm up InputSimulatorPool
        var simulatorPool = runtimeServices.InputSimulatorPool;
        if (simulatorPool != null)
        {
            _ = WarmUpInputSimulatorPoolAsync(simulatorPool, runtimeServices.PositionProvider);
        }

        runtimeServices.TextExpansionService.Start();

        trayIconService.SetEnabled(settingsService.Current.EnableTrayIcon);

        viewModel.TrayIconEnabledChanged += (sender, enabled) =>
        {
            trayIconService.SetEnabled(enabled);
        };
    }

    private async Task HandleFlatpakQuickSetupAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        AppRuntimeServices runtimeServices,
        string initialReason)
    {
        var quickSetupService = runtimeServices.FlatpakQuickSetupService;
        if (quickSetupService == null)
        {
            ShowUnsupportedSessionDialog(desktop, initialReason);
            return;
        }

        var bootstrapOwner = CreateBootstrapOwnerWindow();
        desktop.MainWindow = bootstrapOwner;
        bootstrapOwner.Show();

        try
        {
            var promptMessage =
                "CrossMacro cannot access host input devices in Flatpak on Wayland.\n\n" +
                "Run Quick Setup now?\n\n" +
                "Quick Setup uses flatpak-spawn + pkexec to apply session permissions for your user.\n\n" +
                $"Details: {initialReason}";

            var setupDialog = new CrossMacro.UI.Views.Dialogs.ConfirmationDialog(
                "Wayland Setup Required",
                promptMessage,
                "Run Quick Setup",
                "Exit",
                dangerYes: false,
                dangerNo: true)
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            var shouldRunSetup = await setupDialog.ShowDialog<bool>(bootstrapOwner);
            if (!shouldRunSetup)
            {
                ShowUnsupportedSessionDialog(desktop, initialReason);
                ShowReplacementMainWindowIfNeeded(desktop, bootstrapOwner);
                return;
            }

            var setupResult = await quickSetupService.RunAsync();
            if (!setupResult.Success)
            {
                ShowUnsupportedSessionDialog(desktop, $"{initialReason}\n\n{setupResult.Message}");
                ShowReplacementMainWindowIfNeeded(desktop, bootstrapOwner);
                return;
            }

            if (!runtimeServices.DisplaySessionService.IsSessionSupported(out var reasonAfterSetup))
            {
                ShowUnsupportedSessionDialog(desktop, reasonAfterSetup);
                ShowReplacementMainWindowIfNeeded(desktop, bootstrapOwner);
                return;
            }

            InitializeDesktopRuntime(desktop, runtimeServices);
            ShowReplacementMainWindowIfNeeded(desktop, bootstrapOwner);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[App] Flatpak quick setup flow failed");
            ShowUnsupportedSessionDialog(desktop, "Quick setup failed due to an unexpected error.");
            ShowReplacementMainWindowIfNeeded(desktop, bootstrapOwner);
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

    private async Task HandleMacAccessibilityPermissionGateAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        IPermissionChecker permissionChecker)
    {
        var bootstrapOwner = CreateBootstrapOwnerWindow();
        desktop.MainWindow = bootstrapOwner;
        bootstrapOwner.Show();

        try
        {
            var permissionDialog = new CrossMacro.UI.Views.Dialogs.ConfirmationDialog(
                UIStrings.PermissionRequiredTitle,
                UIStrings.MacOSAccessibilityStartupBlockMessage,
                UIStrings.OpenSettingsButton,
                UIStrings.ExitButton,
                dangerYes: false,
                dangerNo: true)
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            var shouldOpenSettings = await permissionDialog.ShowDialog<bool>(bootstrapOwner);
            if (shouldOpenSettings)
            {
                permissionChecker.OpenAccessibilitySettings();
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[App] macOS accessibility permission gate flow failed");
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

            try
            {
                desktop.Shutdown();
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "[App] Failed to shutdown app after macOS permission gate");
            }
        }
    }

    private static async Task WarmUpInputSimulatorPoolAsync(
        InputSimulatorPool simulatorPool,
        IMousePositionProvider? positionProvider)
    {
        try
        {
            int width = 0;
            int height = 0;

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
            Serilog.Log.Error(ex, "[App] Failed to warm up InputSimulatorPool");
        }
    }

    private static void ShowReplacementMainWindowIfNeeded(
        IClassicDesktopStyleApplicationLifetime desktop,
        Window bootstrapOwner)
    {
        var replacementWindow = desktop.MainWindow;
        if (replacementWindow == null || ReferenceEquals(replacementWindow, bootstrapOwner))
        {
            return;
        }

        if (!replacementWindow.IsVisible)
        {
            replacementWindow.Show();
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
        // Show error dialog as the main window.
        // When user closes this dialog, the application will exit (ShutdownMode.OnMainWindowClose)
        desktop.MainWindow = new CrossMacro.UI.Views.Dialogs.ConfirmationDialog(
            "Unsupported Session",
            reason,
            "Exit",
            null);
    }
    
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "We are removing a validator plugin. If it's trimmed, it's not there to remove, which is fine.")]
    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
