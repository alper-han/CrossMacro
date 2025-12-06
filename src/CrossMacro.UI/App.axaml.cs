using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System;
using System.Linq;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using CrossMacro.Core.Services;
using CrossMacro.Core.Models;
using CrossMacro.UI.ViewModels;
using CrossMacro.UI.Views;
using CrossMacro.Core.Wayland;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Infrastructure.Wayland;
using CrossMacro.UI.Services;

namespace CrossMacro.UI;

public partial class App : Application
{
    private IServiceProvider? _serviceProvider;
    
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        ConfigureServices();
    }
    
    private void ConfigureServices()
    {
        var services = new ServiceCollection();
        
        // Register Core Services
        services.AddSingleton<IHotkeyConfigurationService, HotkeyConfigurationService>();
        services.AddSingleton<ISettingsService, SettingsService>();

        // Register Models (Load from config)
        services.AddSingleton<HotkeySettings>(sp => {
            var configService = sp.GetRequiredService<IHotkeyConfigurationService>();
            return configService.Load();
        });
        
        services.AddSingleton<IGlobalHotkeyService, GlobalHotkeyService>();
        services.AddSingleton<IMacroFileManager, MacroFileManager>();
        
        // Register Native services
        services.AddSingleton<IMousePositionProvider>(sp => 
            MousePositionProviderFactory.CreateProvider());
            
        // Register Validators
        services.AddTransient<PlaybackValidator>();

        services.AddTransient<IMacroRecorder, MacroRecorder>();
        services.AddTransient<IMacroPlayer, MacroPlayer>();
        
        // Register Child ViewModels (Singleton for state persistence)
        services.AddSingleton<RecordingViewModel>();
        services.AddSingleton<PlaybackViewModel>();
        services.AddSingleton<FilesViewModel>();
        services.AddSingleton<SettingsViewModel>();
        
        // Register Main ViewModel (Coordinator)
        services.AddSingleton<MainWindowViewModel>();
        
        // Register Tray Icon Service
        services.AddSingleton<ITrayIconService, TrayIconService>();
        
        _serviceProvider = services.BuildServiceProvider();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            
            // Load settings synchronously to avoid deadlock
            var settingsService = _serviceProvider!.GetRequiredService<ISettingsService>();
            settingsService.Load();
            
            // Resolve ViewModel from DI container
            var viewModel = _serviceProvider!.GetRequiredService<MainWindowViewModel>();
            
            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel
            };
            
            // Initialize tray icon service after main window is created
            var trayIconService = _serviceProvider!.GetRequiredService<ITrayIconService>();
            trayIconService.Initialize();
            
            // Set initial tray icon state
            trayIconService.SetEnabled(settingsService.Current.EnableTrayIcon);
            
            // Listen for tray icon setting changes
            viewModel.TrayIconEnabledChanged += (sender, enabled) =>
            {
                trayIconService.SetEnabled(enabled);
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}