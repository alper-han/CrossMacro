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
                // Show error dialog as the main window. 
                // When user closes this dialog, the application will exit (ShutdownMode.OnMainWindowClose)
                desktop.MainWindow = new CrossMacro.UI.Views.Dialogs.ConfirmationDialog(
                    "Unsupported Session", 
                    reason, 
                    "Exit", 
                    null);
                
                // Prevent further initialization
                return;
            }
            
            var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
            settingsService.Load();
            
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

        if (OperatingSystem.IsMacOS())
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
