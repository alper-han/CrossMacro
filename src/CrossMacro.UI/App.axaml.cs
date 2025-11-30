using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System;
using System.Linq;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using CrossMacro.Core.Services;
using CrossMacro.UI.ViewModels;
using CrossMacro.UI.Views;
using CrossMacro.Core.Wayland;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Infrastructure.Wayland;

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
        
        // Register Core services
        services.AddSingleton<IGlobalHotkeyService, GlobalHotkeyService>();
        services.AddSingleton<IMacroFileManager, MacroFileManager>();
        
        // Register Native services
        services.AddSingleton<IMousePositionProvider>(sp => 
            MousePositionProviderFactory.CreateProvider());
            
        // Register Validators
        services.AddTransient<PlaybackValidator>();

        services.AddTransient<IMacroRecorder, MacroRecorder>();
        services.AddTransient<IMacroPlayer, MacroPlayer>();
        
        // Register ViewModels
        services.AddTransient<MainWindowViewModel>();
        
        _serviceProvider = services.BuildServiceProvider();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            
            // Resolve ViewModel from DI container
            var viewModel = _serviceProvider!.GetRequiredService<MainWindowViewModel>();
            
            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel
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