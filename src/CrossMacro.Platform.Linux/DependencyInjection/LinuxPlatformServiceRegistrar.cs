using System;
using CrossMacro.Core.Services;
using CrossMacro.Core.Services.Recording.Strategies;
using CrossMacro.Platform.Linux.Ipc;
using CrossMacro.Platform.Linux.Services;
using CrossMacro.Platform.Linux.Services.Factories;
using CrossMacro.Platform.Linux.Services.Factories.Selectors;
using CrossMacro.Platform.Linux.Services.Keyboard;
using CrossMacro.Platform.Linux.Strategies;
using CrossMacro.Platform.Linux.Strategies.Selectors;
using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.Platform.Linux.DependencyInjection;

/// <summary>
/// Linux platform service registrar.
/// Handles Wayland/X11/legacy fallback service selection.
/// </summary>
public sealed class LinuxPlatformServiceRegistrar : IPlatformServiceRegistrar
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
        services.AddSingleton<ILinuxLayoutDetector, LinuxLayoutDetector>();
        services.AddSingleton<IXkbStateManager, XkbStateManager>();
        services.AddSingleton<ILinuxKeyCodeMapper>(sp =>
            new LinuxKeyCodeMapper(sp.GetRequiredService<IXkbStateManager>()));
        services.AddSingleton<IKeyboardLayoutService, LinuxKeyboardLayoutService>();
        services.AddSingleton<IpcClient>();

        services.AddSingleton<ILinuxEnvironmentDetector, LinuxEnvironmentDetector>();
        services.AddSingleton<ILinuxInputCapabilityDetector, LinuxInputCapabilityDetector>();

        services.AddSingleton<IEnvironmentInfoProvider, LinuxEnvironmentInfoProvider>();
        services.AddSingleton<IMousePositionProvider>(sp =>
            sp.GetRequiredService<LinuxPositionProviderFactory>().Create());

#pragma warning disable CS8634, CS8621 // Intentionally nullable for optional service
        services.AddSingleton(sp =>
        {
            var provider = sp.GetRequiredService<IMousePositionProvider>();
            return provider as IExtensionStatusNotifier;
        });
#pragma warning restore CS8634, CS8621

        services.AddSingleton<IPermissionChecker, LinuxPermissionChecker>();
        services.AddSingleton<IDisplaySessionService, LinuxDisplaySessionService>();
    }

    private static void RegisterLegacyImplementations(IServiceCollection services)
    {
        services.AddTransient<LinuxInputSimulator>();
        services.AddSingleton<Func<LinuxInputSimulator>>(sp =>
            () => sp.GetRequiredService<LinuxInputSimulator>());

        services.AddTransient<LinuxInputCapture>();
        services.AddSingleton<Func<LinuxInputCapture>>(sp =>
            () => sp.GetRequiredService<LinuxInputCapture>());
    }

    private static void RegisterIpcImplementations(IServiceCollection services)
    {
        services.AddTransient<LinuxIpcInputSimulator>();
        services.AddSingleton<Func<LinuxIpcInputSimulator>>(sp =>
            () => sp.GetRequiredService<LinuxIpcInputSimulator>());

        services.AddTransient<LinuxIpcInputCapture>();
        services.AddSingleton<Func<LinuxIpcInputCapture>>(sp =>
            () => sp.GetRequiredService<LinuxIpcInputCapture>());
    }

    private static void RegisterX11Implementations(IServiceCollection services)
    {
        services.AddTransient<X11InputSimulator>();
        services.AddSingleton<Func<X11InputSimulator>>(sp =>
            () => sp.GetRequiredService<X11InputSimulator>());

        services.AddTransient<X11AbsoluteCapture>();
        services.AddTransient<X11RelativeCapture>();

        services.AddTransient<X11InputCapture>();
        services.AddSingleton<Func<X11InputCapture>>(sp =>
            () => sp.GetRequiredService<X11InputCapture>());
    }

    private static void RegisterFactories(IServiceCollection services)
    {
        services.AddSingleton<LinuxPositionProviderFactory>();

        services.AddSingleton<LinuxSimulatorFactory>(sp => new LinuxSimulatorFactory(
            sp.GetRequiredService<ILinuxEnvironmentDetector>(),
            sp.GetRequiredService<ILinuxInputCapabilityDetector>(),
            sp.GetRequiredService<Func<LinuxInputSimulator>>(),
            sp.GetRequiredService<Func<LinuxIpcInputSimulator>>(),
            sp.GetRequiredService<Func<X11InputSimulator>>()));

        services.AddSingleton<LinuxCaptureFactory>(sp => new LinuxCaptureFactory(
            sp.GetRequiredService<ILinuxEnvironmentDetector>(),
            sp.GetRequiredService<ILinuxInputCapabilityDetector>(),
            sp.GetRequiredService<Func<LinuxInputCapture>>(),
            sp.GetRequiredService<Func<LinuxIpcInputCapture>>(),
            sp.GetRequiredService<Func<X11InputCapture>>()));
    }

    private static void RegisterInputFactories(IServiceCollection services)
    {
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
        services.AddSingleton<InputSimulatorPool>(sp =>
        {
            var factory = sp.GetRequiredService<Func<IInputSimulator>>();
            return new InputSimulatorPool(factory);
        });
    }
}
