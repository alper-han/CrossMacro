namespace CrossMacro.Platform.Linux.Tests.DependencyInjection;

using System.Linq;
using CrossMacro.Core.Services;
using CrossMacro.Core.Services.Recording.Strategies;
using CrossMacro.Platform.Linux.DependencyInjection;
using CrossMacro.Platform.Linux.Services;
using CrossMacro.Platform.Linux.Services.Factories;
using CrossMacro.Platform.Linux.Services.Factories.Selectors;
using CrossMacro.Platform.Linux.Strategies;
using CrossMacro.Platform.Linux.Strategies.Selectors;
using Microsoft.Extensions.DependencyInjection;

public class LinuxPlatformServiceRegistrarTests
{
    [Fact]
    public void RegisterPlatformServices_RegistersExpectedCoreAbstractions()
    {
        var services = new ServiceCollection();

        new LinuxPlatformServiceRegistrar().RegisterPlatformServices(services);

        Assert.Contains(services, d => d.ServiceType == typeof(ILinuxEnvironmentDetector) && d.ImplementationType == typeof(LinuxEnvironmentDetector));
        Assert.Contains(services, d => d.ServiceType == typeof(ILinuxInputCapabilityDetector) && d.ImplementationType == typeof(LinuxInputCapabilityDetector));
        Assert.Contains(services, d => d.ServiceType == typeof(IEnvironmentInfoProvider) && d.ImplementationType == typeof(LinuxEnvironmentInfoProvider));
        Assert.Contains(services, d => d.ServiceType == typeof(IPermissionChecker) && d.ImplementationType == typeof(LinuxPermissionChecker));
        Assert.Contains(services, d => d.ServiceType == typeof(ICoordinateStrategyFactory) && d.ImplementationType == typeof(LinuxCoordinateStrategyFactory));
        Assert.Contains(services, d => d.ServiceType == typeof(InputSimulatorPool));
        Assert.Contains(services, d => d.ServiceType == typeof(Func<IInputSimulator>));
        Assert.Contains(services, d => d.ServiceType == typeof(Func<IInputCapture>));
    }

    [Fact]
    public void RegisterPlatformServices_RegistersAllStrategyAndProviderSelectors()
    {
        var services = new ServiceCollection();

        new LinuxPlatformServiceRegistrar().RegisterPlatformServices(services);

        Assert.Equal(5, services.Count(d => d.ServiceType == typeof(ICoordinateStrategySelector)));
        Assert.Contains(services, d => d.ServiceType == typeof(ICoordinateStrategySelector) && d.ImplementationType == typeof(ForceRelativeStrategySelector));
        Assert.Contains(services, d => d.ServiceType == typeof(ICoordinateStrategySelector) && d.ImplementationType == typeof(WaylandAbsoluteStrategySelector));
        Assert.Contains(services, d => d.ServiceType == typeof(ICoordinateStrategySelector) && d.ImplementationType == typeof(WaylandRelativeStrategySelector));
        Assert.Contains(services, d => d.ServiceType == typeof(ICoordinateStrategySelector) && d.ImplementationType == typeof(X11AbsoluteStrategySelector));
        Assert.Contains(services, d => d.ServiceType == typeof(ICoordinateStrategySelector) && d.ImplementationType == typeof(X11RelativeStrategySelector));

        Assert.Equal(5, services.Count(d => d.ServiceType == typeof(IPositionProviderSelector)));
        Assert.Contains(services, d => d.ServiceType == typeof(IPositionProviderSelector) && d.ImplementationType == typeof(X11PositionProviderSelector));
        Assert.Contains(services, d => d.ServiceType == typeof(IPositionProviderSelector) && d.ImplementationType == typeof(GnomePositionProviderSelector));
        Assert.Contains(services, d => d.ServiceType == typeof(IPositionProviderSelector) && d.ImplementationType == typeof(KdePositionProviderSelector));
        Assert.Contains(services, d => d.ServiceType == typeof(IPositionProviderSelector) && d.ImplementationType == typeof(HyprlandPositionProviderSelector));
        Assert.Contains(services, d => d.ServiceType == typeof(IPositionProviderSelector) && d.ImplementationType == typeof(FallbackPositionProviderSelector));
    }
}
