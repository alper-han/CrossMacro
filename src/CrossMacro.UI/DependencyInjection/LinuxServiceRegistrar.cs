using System;
using Microsoft.Extensions.DependencyInjection;
using CrossMacro.Core.Services;
using CrossMacro.Core.Services.Recording.Strategies;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Platform.Linux;
using CrossMacro.Platform.Linux.Ipc;
using CrossMacro.Platform.Linux.Services;
using CrossMacro.Platform.Linux.Services.Factories;
using CrossMacro.Platform.Linux.Services.Keyboard;
using CrossMacro.Platform.Linux.Strategies;
using CrossMacro.Platform.Linux.Strategies.Selectors;
using CrossMacro.Platform.Linux.Services.Factories.Selectors;

namespace CrossMacro.UI.DependencyInjection;

/// <summary>
/// Linux platform service registrar.
/// Handles the most complex platform with Wayland/X11/Legacy fallbacks.
/// Refactored to use specialized factories following SRP.
/// </summary>
public class LinuxServiceRegistrar : IPlatformServiceRegistrar
{
    public void RegisterPlatformServices(IServiceCollection services)
    {
        RegisterCoreServices(services);
        RegisterLegacyImplementations(services);
        RegisterIpcImplementations(services);
        RegisterX11Implementations(services);
        RegisterFactories(services);
        RegisterInputFactories(services);
        RegisterStrategySelectors(services);
        RegisterPositionProviderSelectors(services);
        RegisterCoordinateStrategy(services);
        RegisterInputSimulatorPool(services);
    }

    private static void RegisterCoreServices(IServiceCollection services)
    {
        // Keyboard layout components (SRP: each has single responsibility)
        services.AddSingleton<ILinuxLayoutDetector, LinuxLayoutDetector>();
        services.AddSingleton<IXkbStateManager, XkbStateManager>();
        services.AddSingleton<ILinuxKeyCodeMapper>(sp => 
            new LinuxKeyCodeMapper(sp.GetRequiredService<IXkbStateManager>()));
        services.AddSingleton<IKeyboardLayoutService, LinuxKeyboardLayoutService>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IpcClient>();
        
        // Register detection services
        services.AddSingleton<ILinuxEnvironmentDetector, LinuxEnvironmentDetector>();
        services.AddSingleton<ILinuxInputCapabilityDetector, LinuxInputCapabilityDetector>();
        
        // Environment info provider for UI layer (DIP: UI depends on Core interface)
        services.AddSingleton<IEnvironmentInfoProvider, LinuxEnvironmentInfoProvider>();
        
        // Position provider via dedicated factory
        services.AddSingleton<IMousePositionProvider>(sp => 
            sp.GetRequiredService<LinuxPositionProviderFactory>().Create());
        
        // Extension status notifier - expose as optional interface if provider supports it
        // This allows UI to subscribe to extension events without knowing about GnomePositionProvider
        #pragma warning disable CS8634, CS8621 // Intentionally nullable for optional service
        services.AddSingleton(sp => 
        {
            var provider = sp.GetRequiredService<IMousePositionProvider>();
            return provider as IExtensionStatusNotifier;
        });
        #pragma warning restore CS8634, CS8621

        // Register permission checker
        services.AddSingleton<IPermissionChecker, LinuxPermissionChecker>();
        
        // Register display session service (Wayland guard for Flatpak)
        services.AddSingleton<IDisplaySessionService, LinuxDisplaySessionService>();
    }

    private static void RegisterLegacyImplementations(IServiceCollection services)
    {
        // Legacy UInput implementations (for root/input group access)
        services.AddTransient<LinuxInputSimulator>();
        services.AddSingleton<Func<LinuxInputSimulator>>(sp => 
            () => sp.GetRequiredService<LinuxInputSimulator>());
        
        services.AddTransient<LinuxInputCapture>();
        services.AddSingleton<Func<LinuxInputCapture>>(sp => 
            () => sp.GetRequiredService<LinuxInputCapture>());
    }

    private static void RegisterIpcImplementations(IServiceCollection services)
    {
        // IPC implementations (for daemon communication)
        services.AddTransient<LinuxIpcInputSimulator>();
        services.AddSingleton<Func<LinuxIpcInputSimulator>>(sp => 
            () => sp.GetRequiredService<LinuxIpcInputSimulator>());

        // SINGLETON: Both GlobalHotkeyService and MacroRecorder share the same capture instance.
        // This prevents duplicate StartCapture commands to daemon, which was causing initial event loss.
        services.AddSingleton<LinuxIpcInputCapture>();
        services.AddSingleton<Func<LinuxIpcInputCapture>>(sp => 
            () => sp.GetRequiredService<LinuxIpcInputCapture>());
    }

    private static void RegisterX11Implementations(IServiceCollection services)
    {
        // X11 native implementations
        services.AddTransient<X11InputSimulator>();
        services.AddSingleton<Func<X11InputSimulator>>(sp => 
            () => sp.GetRequiredService<X11InputSimulator>());

        // X11 sub-capturers
        services.AddTransient<X11AbsoluteCapture>();
        services.AddTransient<X11RelativeCapture>();
        
        services.AddTransient<X11InputCapture>();
        services.AddSingleton<Func<X11InputCapture>>(sp => 
            () => sp.GetRequiredService<X11InputCapture>());
    }

    private static void RegisterFactories(IServiceCollection services)
    {
        // Position provider factory (SRP: only creates position providers)
        services.AddSingleton<LinuxPositionProviderFactory>();
        
        // Simulator factory (SRP: only creates simulators)
        services.AddSingleton<LinuxSimulatorFactory>(sp => new LinuxSimulatorFactory(
            sp.GetRequiredService<ILinuxEnvironmentDetector>(),
            sp.GetRequiredService<ILinuxInputCapabilityDetector>(),
            sp.GetRequiredService<Func<LinuxInputSimulator>>(),
            sp.GetRequiredService<Func<LinuxIpcInputSimulator>>(),
            sp.GetRequiredService<Func<X11InputSimulator>>()
        ));
        
        // Capture factory (SRP: only creates captures)
        services.AddSingleton<LinuxCaptureFactory>(sp => new LinuxCaptureFactory(
            sp.GetRequiredService<ILinuxEnvironmentDetector>(),
            sp.GetRequiredService<ILinuxInputCapabilityDetector>(),
            sp.GetRequiredService<Func<LinuxInputCapture>>(),
            sp.GetRequiredService<Func<LinuxIpcInputCapture>>(),
            sp.GetRequiredService<Func<X11InputCapture>>()
        ));
    }

    private static void RegisterInputFactories(IServiceCollection services)
    {
        // Abstract factories that use specialized factories
        services.AddTransient<Func<IInputSimulator>>(sp =>
        {
            var factory = sp.GetRequiredService<LinuxSimulatorFactory>();
            return () => factory.Create();
        });

        services.AddTransient<Func<IInputCapture>>(sp =>
        {
            var factory = sp.GetRequiredService<LinuxCaptureFactory>();
            return () => factory.Create();
        });
    }

    private static void RegisterStrategySelectors(IServiceCollection services)
    {
        services.AddSingleton<ICoordinateStrategySelector, ForceRelativeStrategySelector>();
        services.AddSingleton<ICoordinateStrategySelector, WaylandAbsoluteStrategySelector>();
        services.AddSingleton<ICoordinateStrategySelector, WaylandRelativeStrategySelector>();
        services.AddSingleton<ICoordinateStrategySelector, X11AbsoluteStrategySelector>();
        services.AddSingleton<ICoordinateStrategySelector, X11RelativeStrategySelector>();
    }

    private static void RegisterPositionProviderSelectors(IServiceCollection services)
    {
        services.AddSingleton<IPositionProviderSelector, X11PositionProviderSelector>();
        services.AddSingleton<IPositionProviderSelector, GnomePositionProviderSelector>();
        services.AddSingleton<IPositionProviderSelector, KdePositionProviderSelector>();
        services.AddSingleton<IPositionProviderSelector, HyprlandPositionProviderSelector>();
        services.AddSingleton<IPositionProviderSelector, FallbackPositionProviderSelector>();
    }

    private static void RegisterCoordinateStrategy(IServiceCollection services)
    {
        services.AddSingleton<ICoordinateStrategyFactory, LinuxCoordinateStrategyFactory>();
    }

    private static void RegisterInputSimulatorPool(IServiceCollection services)
    {
        // InputSimulatorPool for zero-delay device acquisition
        services.AddSingleton<InputSimulatorPool>(sp =>
        {
            var factory = sp.GetRequiredService<Func<IInputSimulator>>();
            return new InputSimulatorPool(factory);
        });
    }
}
