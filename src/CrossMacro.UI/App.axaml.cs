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
        bool desktopInitializationDeferred = false;

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

            // Check if current display session is supported (Wayland guard for Flatpak)
            var displaySessionService = _serviceProvider.GetRequiredService<IDisplaySessionService>();
            if (!displaySessionService.IsSessionSupported(out var reason))
            {
                var quickSetupService = _serviceProvider.GetService<IFlatpakQuickSetupService>();
                if (quickSetupService != null && quickSetupService.IsApplicable())
                {
                    desktopInitializationDeferred = true;
                    _ = HandleFlatpakQuickSetupAsync(desktop, displaySessionService, quickSetupService, reason);
                }
                else
                {
                    ShowUnsupportedSessionDialog(desktop, reason);
                }
            }
            else
            {
                InitializeDesktopRuntime(desktop);
            }
        }

        if (!desktopInitializationDeferred && OperatingSystem.IsMacOS())
        {
             var permissionChecker = _serviceProvider?.GetService<IPermissionChecker>();
             if (permissionChecker != null && 
                 permissionChecker.IsSupported && 
                 !permissionChecker.IsAccessibilityTrusted())
             {
                 var dialogService = _serviceProvider?.GetRequiredService<IDialogService>();
                 if (dialogService != null)
                 {
                     _ = Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () => {
                        var result = await dialogService.ShowConfirmationAsync(
                             UIStrings.PermissionRequiredTitle, 
                             UIStrings.MacOSAccessibilityMessage,
                             UIStrings.OpenSettingsButton,
                             UIStrings.LaterButton);
                        
                        if (result)
                        {
                            permissionChecker.OpenAccessibilitySettings();
                        }
                     });
                 }
             }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void InitializeDesktopRuntime(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (_serviceProvider == null)
        {
            throw new InvalidOperationException("Service provider is not initialized");
        }

        var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
        settingsService.Load();
        var themeService = _serviceProvider.GetRequiredService<IThemeService>();
        if (!themeService.TryApplyTheme(settingsService.Current.Theme, out var themeError))
        {
            Serilog.Log.Warning("[App] Theme apply fallback triggered for '{Theme}': {Error}", settingsService.Current.Theme, themeError);
            settingsService.Current.Theme = themeService.CurrentTheme;
            _ = settingsService.SaveAsync();
        }

        var viewModel = _serviceProvider.GetRequiredService<MainWindowViewModel>();

        desktop.MainWindow = new MainWindow
        {
            DataContext = viewModel
        };

        var trayIconService = _serviceProvider.GetRequiredService<ITrayIconService>();
        trayIconService.Initialize();

        // Warm up InputSimulatorPool
        var simulatorPool = _serviceProvider.GetService<InputSimulatorPool>();
        if (simulatorPool != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var positionProvider = _serviceProvider.GetService<IMousePositionProvider>();
                    int width = 0, height = 0;

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
            });
        }

        var expansionService = _serviceProvider.GetRequiredService<ITextExpansionService>();
        expansionService.Start();

        trayIconService.SetEnabled(settingsService.Current.EnableTrayIcon);

        viewModel.TrayIconEnabledChanged += (sender, enabled) =>
        {
            trayIconService.SetEnabled(enabled);
        };
    }

    private async Task HandleFlatpakQuickSetupAsync(
        IClassicDesktopStyleApplicationLifetime desktop,
        IDisplaySessionService displaySessionService,
        IFlatpakQuickSetupService quickSetupService,
        string initialReason)
    {
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

            if (!displaySessionService.IsSessionSupported(out var reasonAfterSetup))
            {
                ShowUnsupportedSessionDialog(desktop, reasonAfterSetup);
                ShowReplacementMainWindowIfNeeded(desktop, bootstrapOwner);
                return;
            }

            InitializeDesktopRuntime(desktop);
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
