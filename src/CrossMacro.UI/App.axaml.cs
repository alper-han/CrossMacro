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
using CrossMacro.Infrastructure.Services;
using CrossMacro.Infrastructure.Wayland;
using CrossMacro.UI.Services;
using CrossMacro.Platform.Linux;
using CrossMacro.Platform.Windows;
using CrossMacro.Platform.MacOS;

namespace CrossMacro.UI;

public partial class App : Application
{
    private IServiceProvider? _serviceProvider;
    public IServiceProvider? Services => _serviceProvider;
    
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
        
        services.AddSingleton<ITimeProvider, SystemTimeProvider>();

        // GlobalHotkeyService is registered after platform-specific factories below
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
        else if (OperatingSystem.IsMacOS())
        {
            services.AddMacOSServices();
            services.AddSingleton<IKeyboardLayoutService, KeyboardLayoutService>();

            // Register factories for macOS
            services.AddTransient<Func<IInputSimulator>>(sp => () => sp.GetRequiredService<IInputSimulator>());
            services.AddTransient<Func<IInputCapture>>(sp => () => sp.GetRequiredService<IInputCapture>());
            
            services.AddTransient<IMacroRecorder>(sp =>
            {
                var positionProvider = sp.GetRequiredService<IMousePositionProvider>();
                Func<IInputSimulator> simulatorFactory = () => sp.GetRequiredService<IInputSimulator>();
                Func<IInputCapture> captureFactory = () => sp.GetRequiredService<IInputCapture>();
                return new MacroRecorder(positionProvider, simulatorFactory, captureFactory);
            });
        }
        else
        {
            services.AddSingleton<IKeyboardLayoutService, KeyboardLayoutService>();
            services.AddSingleton<IProcessRunner, ProcessRunner>();
            
            services.AddSingleton<CrossMacro.Platform.Linux.Ipc.IpcClient>();
            
            services.AddSingleton<IMousePositionProvider>(sp => 
                MousePositionProviderFactory.CreateProvider());
            
            // Register Legacy implementations (Transient)
            services.AddTransient<CrossMacro.Platform.Linux.LinuxInputSimulator>();
            services.AddSingleton<Func<CrossMacro.Platform.Linux.LinuxInputSimulator>>(sp => () => sp.GetRequiredService<CrossMacro.Platform.Linux.LinuxInputSimulator>());
            
            services.AddTransient<CrossMacro.Platform.Linux.LinuxInputCapture>();
            services.AddSingleton<Func<CrossMacro.Platform.Linux.LinuxInputCapture>>(sp => () => sp.GetRequiredService<CrossMacro.Platform.Linux.LinuxInputCapture>());
            
            // Register IPC implementations (Transient)
            services.AddTransient<CrossMacro.Platform.Linux.Ipc.LinuxIpcInputSimulator>();
            services.AddSingleton<Func<CrossMacro.Platform.Linux.Ipc.LinuxIpcInputSimulator>>(sp => () => sp.GetRequiredService<CrossMacro.Platform.Linux.Ipc.LinuxIpcInputSimulator>());

            services.AddTransient<CrossMacro.Platform.Linux.Ipc.LinuxIpcInputCapture>();
            services.AddSingleton<Func<CrossMacro.Platform.Linux.Ipc.LinuxIpcInputCapture>>(sp => () => sp.GetRequiredService<CrossMacro.Platform.Linux.Ipc.LinuxIpcInputCapture>());
            
            // Register Factory
            services.AddSingleton<LinuxInputProviderFactory>(sp => new LinuxInputProviderFactory(
                sp.GetRequiredService<CrossMacro.Platform.Linux.Ipc.IpcClient>(),
                sp.GetRequiredService<Func<CrossMacro.Platform.Linux.LinuxInputSimulator>>(),
                sp.GetRequiredService<Func<CrossMacro.Platform.Linux.LinuxInputCapture>>(),
                sp.GetRequiredService<Func<CrossMacro.Platform.Linux.Ipc.LinuxIpcInputSimulator>>(),
                sp.GetRequiredService<Func<CrossMacro.Platform.Linux.Ipc.LinuxIpcInputCapture>>()
            ));
            
            // Use Factory for IInputSimulator and IInputCapture
            services.AddTransient<Func<IInputSimulator>>(sp => 
            {
                var factory = sp.GetRequiredService<LinuxInputProviderFactory>();
                return () => factory.CreateSimulator();
            });
            
            services.AddTransient<Func<IInputCapture>>(sp => 
            {
                var factory = sp.GetRequiredService<LinuxInputProviderFactory>();
                return () => factory.CreateCapture();
            });
            
            services.AddTransient<IMacroRecorder>(sp =>
            {
                var positionProvider = sp.GetRequiredService<IMousePositionProvider>();
                var factory = sp.GetRequiredService<LinuxInputProviderFactory>();
                
                Func<IInputSimulator> simulatorFactory = () => factory.CreateSimulator();
                Func<IInputCapture> captureFactory = () => factory.CreateCapture();
                
                return new MacroRecorder(positionProvider, simulatorFactory, captureFactory);
            });
        }
        
        // GlobalHotkeyService must be registered after Func<IInputCapture> factories
        services.AddSingleton<IGlobalHotkeyService>(sp =>
        {
            var configService = sp.GetRequiredService<IHotkeyConfigurationService>();
            var layoutService = sp.GetRequiredService<IKeyboardLayoutService>();
            var captureFactory = sp.GetService<Func<IInputCapture>>();
            return new GlobalHotkeyService(configService, layoutService, captureFactory);
        });
        
        services.AddTransient<PlaybackValidator>();
        
        services.AddTransient<IMacroPlayer, MacroPlayer>();
        
        // Factory for creating new player instances (used by SchedulerService)
        services.AddSingleton<Func<IMacroPlayer>>(sp => () => sp.GetRequiredService<IMacroPlayer>());
        
        services.AddSingleton<RecordingViewModel>();
        services.AddSingleton<PlaybackViewModel>();
        services.AddSingleton<FilesViewModel>();
        services.AddSingleton<TextExpansionViewModel>();
        services.AddSingleton<ISchedulerService, SchedulerService>();
        services.AddSingleton<ScheduleViewModel>();
        services.AddSingleton<IShortcutService, ShortcutService>();
        services.AddSingleton<ShortcutViewModel>();
        services.AddSingleton<SettingsViewModel>();
        
        services.AddSingleton<MainWindowViewModel>();
        
        services.AddSingleton<ITrayIconService, TrayIconService>();
        
        services.AddSingleton<AvaloniaClipboardService>();

        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
        {
            services.AddSingleton<IClipboardService>(sp => sp.GetRequiredService<AvaloniaClipboardService>());
        }
        else
        {
            services.AddSingleton<LinuxShellClipboardService>();
            services.AddSingleton<IClipboardService, CompositeClipboardService>();
        }

        services.AddSingleton<ITextExpansionStorageService, TextExpansionStorageService>();
        services.AddSingleton<ITextExpansionService, TextExpansionService>();
        services.AddSingleton<IDialogService, DialogService>();


        services.AddSingleton<IUpdateService, GitHubUpdateService>();

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

        if (OperatingSystem.IsMacOS())
        {
             if (!CrossMacro.Platform.MacOS.MacOSPermissionChecker.IsAccessibilityTrusted())
             {
                 var dialogService = _serviceProvider?.GetRequiredService<IDialogService>();
                 if (dialogService != null)
                 {
                     _ = Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () => {
                        var result = await dialogService.ShowConfirmationAsync(
                             "Permission Required", 
                             "CrossMacro requires Accessibility permissions to capture keyboard and mouse input.\n\nWould you like to open System Settings now?",
                             "Open Settings",
                             "Later");
                        
                        if (result)
                        {
                            CrossMacro.Platform.MacOS.MacOSPermissionChecker.OpenAccessibilitySettings();
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