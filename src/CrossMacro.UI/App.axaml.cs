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
using CrossMacro.Platform.Linux;
using CrossMacro.Platform.Windows;

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
        
        services.AddSingleton<IHotkeyConfigurationService, HotkeyConfigurationService>();
        services.AddSingleton<ISettingsService, SettingsService>();

        services.AddSingleton<HotkeySettings>(sp => {
            var configService = sp.GetRequiredService<IHotkeyConfigurationService>();
            return configService.Load();
        });
        
        services.AddSingleton<IGlobalHotkeyService, GlobalHotkeyService>();
        services.AddSingleton<IMacroFileManager, MacroFileManager>();
        
        if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<IKeyboardLayoutService, WindowsKeyboardLayoutService>();
            services.AddSingleton<IMousePositionProvider, WindowsMousePositionProvider>();
            
            services.AddTransient<Func<IInputSimulator>>(sp => () => new WindowsInputSimulator());
            services.AddTransient<Func<IInputCapture>>(sp => () => new WindowsInputCapture());
            
            services.AddTransient<IMacroRecorder>(sp =>
            {
                var positionProvider = sp.GetRequiredService<IMousePositionProvider>();
                Func<IInputSimulator> simulatorFactory = () => new WindowsInputSimulator();
                Func<IInputCapture> captureFactory = () => new WindowsInputCapture();
                return new MacroRecorder(positionProvider, simulatorFactory, captureFactory);
            });
        }
        else
        {
            services.AddSingleton<IKeyboardLayoutService, KeyboardLayoutService>();
            
            services.AddSingleton<IMousePositionProvider>(sp => 
                MousePositionProviderFactory.CreateProvider());
                
            services.AddTransient<Func<IInputSimulator>>(sp => () => new LinuxInputSimulator());
            services.AddTransient<Func<IInputCapture>>(sp => () => new LinuxInputCapture());
            
            services.AddTransient<IMacroRecorder>(sp =>
            {
                var positionProvider = sp.GetRequiredService<IMousePositionProvider>();
                Func<IInputSimulator> simulatorFactory = () => new LinuxInputSimulator();
                Func<IInputCapture> captureFactory = () => new LinuxInputCapture();
                return new MacroRecorder(positionProvider, simulatorFactory, captureFactory);
            });
        }
        
        services.AddTransient<PlaybackValidator>();
        
        services.AddTransient<IMacroPlayer, MacroPlayer>();
        
        services.AddSingleton<RecordingViewModel>();
        services.AddSingleton<PlaybackViewModel>();
        services.AddSingleton<FilesViewModel>();
        services.AddSingleton<TextExpansionViewModel>();
        services.AddSingleton<SettingsViewModel>();
        
        services.AddSingleton<MainWindowViewModel>();
        
        services.AddSingleton<ITrayIconService, TrayIconService>();
        
        services.AddSingleton<AvaloniaClipboardService>();

        if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<IClipboardService>(sp => sp.GetRequiredService<AvaloniaClipboardService>());
        }
        else
        {
            services.AddSingleton<LinuxShellClipboardService>();
            services.AddSingleton<IClipboardService, CompositeClipboardService>();
        }

        services.AddSingleton<TextExpansionStorageService>();
        services.AddSingleton<ITextExpansionService, TextExpansionService>();
        services.AddSingleton<IDialogService, DialogService>();

        _serviceProvider = services.BuildServiceProvider();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            
            if (_serviceProvider == null)
            {
                throw new InvalidOperationException("Service provider is not initialized");
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
            
            var expansionService = _serviceProvider.GetRequiredService<ITextExpansionService>();
            _ = System.Threading.Tasks.Task.Run(() => expansionService.Start());
            
            trayIconService.SetEnabled(settingsService.Current.EnableTrayIcon);
            
            viewModel.TrayIconEnabledChanged += (sender, enabled) =>
            {
                trayIconService.SetEnabled(enabled);
            };

            var settingsVM = _serviceProvider.GetRequiredService<SettingsViewModel>();
            settingsVM.PropertyChanged += (sender, e) => {
                if (e.PropertyName == nameof(SettingsViewModel.EnableTextExpansion))
                {
                     _ = System.Threading.Tasks.Task.Run(() => {
                         if (settingsVM.EnableTextExpansion) expansionService.Start();
                         else expansionService.Stop();
                     });
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

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