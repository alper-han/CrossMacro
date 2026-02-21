using System;
using System.Linq;
using System.Runtime.Versioning;
using CrossMacro.Core.Services;
using CrossMacro.Core.Services.Recording.Strategies;
using CrossMacro.Platform.MacOS.DependencyInjection;
using CrossMacro.Platform.MacOS.Services;
using CrossMacro.Platform.MacOS.Strategies;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CrossMacro.Platform.MacOS.Tests.DependencyInjection;

[SupportedOSPlatform("macos")]
public class MacOSPlatformServiceRegistrarTests
{
    [Fact]
    public void RegisterPlatformServices_RegistersCorePlatformServices()
    {
        var services = new ServiceCollection();

        new MacOSPlatformServiceRegistrar().RegisterPlatformServices(services);

        Assert.Equal(typeof(MacKeyboardLayoutService), services.Last(s => s.ServiceType == typeof(IKeyboardLayoutService)).ImplementationType);
        Assert.Equal(typeof(MacOSEnvironmentInfoProvider), services.Last(s => s.ServiceType == typeof(IEnvironmentInfoProvider)).ImplementationType);
        Assert.Equal(typeof(MacOSMousePositionProvider), services.Last(s => s.ServiceType == typeof(IMousePositionProvider)).ImplementationType);
        Assert.Equal(typeof(MacOSPermissionCheckerService), services.Last(s => s.ServiceType == typeof(IPermissionChecker)).ImplementationType);
    }

    [Fact]
    public void RegisterPlatformServices_RegistersFactoriesAndCoordinateStrategy()
    {
        var services = new ServiceCollection();
        new MacOSPlatformServiceRegistrar().RegisterPlatformServices(services);
        using var provider = services.BuildServiceProvider();

        var captureFactory = provider.GetRequiredService<Func<IInputCapture>>();
        var simulatorFactory = provider.GetRequiredService<Func<IInputSimulator>>();
        var strategyFactory = provider.GetRequiredService<ICoordinateStrategyFactory>();
        var displaySession = provider.GetRequiredService<IDisplaySessionService>();
        var notifier = provider.GetService<IExtensionStatusNotifier>();

        Assert.IsType<MacOSInputCapture>(captureFactory());
        Assert.IsType<MacOSInputSimulator>(simulatorFactory());
        Assert.IsType<MacOSCoordinateStrategyFactory>(strategyFactory);
        Assert.IsType<GenericDisplaySessionService>(displaySession);
        Assert.Null(notifier);
    }
}
