using System;
using System.Linq;
using CrossMacro.Core.Services;
using CrossMacro.Core.Services.Recording.Strategies;
using CrossMacro.Platform.Windows.DependencyInjection;
using CrossMacro.Platform.Windows.Services;
using CrossMacro.Platform.Windows.Strategies;
using CrossMacro.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CrossMacro.Platform.Windows.Tests.DependencyInjection;

public class WindowsPlatformServiceRegistrarTests
{
    [WindowsFact]
    public void RegisterPlatformServices_RegistersKeyboardLayoutService()
    {
        var services = new ServiceCollection();

        new WindowsPlatformServiceRegistrar().RegisterPlatformServices(services);

        var descriptor = services.LastOrDefault(s => s.ServiceType == typeof(IKeyboardLayoutService));
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(WindowsKeyboardLayoutService), descriptor!.ImplementationType);
    }

    [WindowsFact]
    public void RegisterPlatformServices_RegistersMousePositionProvider()
    {
        var services = new ServiceCollection();

        new WindowsPlatformServiceRegistrar().RegisterPlatformServices(services);

        var descriptor = services.LastOrDefault(s => s.ServiceType == typeof(IMousePositionProvider));
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(WindowsMousePositionProvider), descriptor!.ImplementationType);
    }

    [WindowsFact]
    public void RegisterPlatformServices_RegistersEnvironmentInfoProvider()
    {
        var services = new ServiceCollection();

        new WindowsPlatformServiceRegistrar().RegisterPlatformServices(services);

        var descriptor = services.LastOrDefault(s => s.ServiceType == typeof(IEnvironmentInfoProvider));
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(WindowsEnvironmentInfoProvider), descriptor!.ImplementationType);
    }

    [WindowsFact]
    public void RegisterPlatformServices_RegistersCoordinateFactoryAndDisplayService()
    {
        var services = new ServiceCollection();

        new WindowsPlatformServiceRegistrar().RegisterPlatformServices(services);

        var strategyDescriptor = services.LastOrDefault(s => s.ServiceType == typeof(ICoordinateStrategyFactory));
        Assert.NotNull(strategyDescriptor);
        Assert.Equal(typeof(WindowsCoordinateStrategyFactory), strategyDescriptor!.ImplementationType);

        var displayDescriptor = services.LastOrDefault(s => s.ServiceType == typeof(IDisplaySessionService));
        Assert.NotNull(displayDescriptor);
        Assert.Equal(typeof(GenericDisplaySessionService), displayDescriptor!.ImplementationType);
    }

    [WindowsFact]
    public void RegisterPlatformServices_RegistersInputFactories_ThatCreateWindowsImplementations()
    {
        var services = new ServiceCollection();
        new WindowsPlatformServiceRegistrar().RegisterPlatformServices(services);

        using var provider = services.BuildServiceProvider();

        var simulatorFactory = provider.GetRequiredService<Func<IInputSimulator>>();
        var captureFactory = provider.GetRequiredService<Func<IInputCapture>>();

        Assert.IsType<WindowsInputSimulator>(simulatorFactory());
        Assert.IsType<WindowsInputCapture>(captureFactory());
    }

    [WindowsFact]
    public void RegisterPlatformServices_RegistersNullableExtensionNotifier()
    {
        var services = new ServiceCollection();
        new WindowsPlatformServiceRegistrar().RegisterPlatformServices(services);

        using var provider = services.BuildServiceProvider();
        var notifier = provider.GetService<IExtensionStatusNotifier>();

        Assert.Null(notifier);
    }
}
