namespace CrossMacro.Platform.Linux.Tests.DependencyInjection;

using System.Linq;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Packaging.Abstractions;
using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Abstractions.Diagnostics;
using CrossMacro.Platform.Linux.DependencyInjection;
using CrossMacro.Platform.Linux.Services;
using CrossMacro.Platform.Linux.Services.Factories;
using CrossMacro.Platform.Linux.Services.Factories.Selectors;
using CrossMacro.Platform.Linux.Services.QuickSetup;
using CrossMacro.Platform.Linux.Strategies;
using CrossMacro.Platform.Linux.Strategies.Selectors;
using Microsoft.Extensions.DependencyInjection;

public class LinuxPlatformServiceRegistrarTests
{
    [Fact]
    public void RegisterPlatformServices_RegistersExpectedCoreAbstractions()
    {
        var services = new ServiceCollection();

        var registrar = new LinuxPlatformServiceRegistrar();

        Assert.Equal(PlatformClipboardRegistration.Linux, registrar.ClipboardRegistration);

        registrar.RegisterPlatformServices(services);

        Assert.Contains(services, d => d.ServiceType == typeof(ILinuxEnvironmentDetector) && d.ImplementationType == typeof(LinuxEnvironmentDetector));
        Assert.Contains(services, d => d.ServiceType == typeof(ILinuxEnvironmentVariables) && d.ImplementationType == typeof(LinuxEnvironmentVariables));
        Assert.Contains(services, d => d.ServiceType == typeof(ILinuxInputCapabilityDetector) && d.ImplementationType == typeof(LinuxInputCapabilityDetector));
        Assert.Contains(services, d => d.ServiceType == typeof(IPlatformStartupNotificationProvider) && d.ImplementationType == typeof(GsrCompatibilityService));
        Assert.Contains(services, d => d.ServiceType == typeof(ILinuxDaemonSocketAccessProbe) && d.ImplementationType == typeof(LinuxDaemonSocketAccessProbe));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(LinuxInputProbeUtilities));
        Assert.Contains(services, d => d.ServiceType == typeof(IEnvironmentInfoProvider) && d.ImplementationType == typeof(LinuxEnvironmentInfoProvider));
        Assert.Contains(services, d => d.ServiceType == typeof(IDisplaySessionService) && d.ImplementationType == typeof(LinuxDisplaySessionService));
        Assert.Contains(services, d => d.ServiceType == typeof(IPermissionChecker) && d.ImplementationType == typeof(LinuxPermissionChecker));
        Assert.Contains(services, d => d.ServiceType == typeof(ICoordinateStrategyFactory) && d.ImplementationType == typeof(LinuxCoordinateStrategyFactory));
        Assert.Contains(services, d => d.ServiceType == typeof(IPlaybackBehaviorPolicy));
        Assert.Contains(services, d => d.ServiceType == typeof(LinuxQuickSetupIdentityResolver) && d.ImplementationType == typeof(LinuxQuickSetupIdentityResolver));
        Assert.Contains(services, d => d.ServiceType == typeof(LinuxQuickSetupScriptBuilder) && d.ImplementationType == typeof(LinuxQuickSetupScriptBuilder));
        Assert.Contains(services, d => d.ServiceType == typeof(LinuxQuickSetupExecutor) && d.ImplementationType == typeof(LinuxQuickSetupExecutor));
        Assert.Contains(services, d => d.ServiceType == typeof(FlatpakHostCommandLauncher) && d.ImplementationType == typeof(FlatpakHostCommandLauncher));
        Assert.Contains(services, d => d.ServiceType == typeof(DirectPkexecHostCommandLauncher) && d.ImplementationType == typeof(DirectPkexecHostCommandLauncher));
        Assert.Contains(services, d => d.ServiceType == typeof(IFlatpakQuickSetupService) && d.ImplementationFactory != null);
        Assert.Contains(services, d => d.ServiceType == typeof(IAppImageQuickSetupService) && d.ImplementationFactory != null);
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

        Assert.Equal(7, services.Count(d => d.ServiceType == typeof(IPositionProviderSelector)));
        Assert.Contains(services, d => d.ServiceType == typeof(IPositionProviderSelector) && d.ImplementationType == typeof(X11PositionProviderSelector));
        Assert.Contains(services, d => d.ServiceType == typeof(IPositionProviderSelector) && d.ImplementationType == typeof(GnomePositionProviderSelector));
        Assert.Contains(services, d => d.ServiceType == typeof(IPositionProviderSelector) && d.ImplementationType == typeof(KdePositionProviderSelector));
        Assert.Contains(services, d => d.ServiceType == typeof(IPositionProviderSelector) && d.ImplementationType == typeof(HyprlandPositionProviderSelector));
        Assert.Contains(services, d => d.ServiceType == typeof(IPositionProviderSelector) && d.ImplementationType == typeof(WayfirePositionProviderSelector));
        Assert.Contains(services, d => d.ServiceType == typeof(IPositionProviderSelector) && d.ImplementationType == typeof(NiriPositionProviderSelector));
        Assert.Contains(services, d => d.ServiceType == typeof(IPositionProviderSelector) && d.ImplementationType == typeof(CosmicPositionProviderSelector));
    }

    [Fact]
    public void RegisterPlatformServices_RegistersLinuxPlaybackBehaviorPolicy()
    {
        var services = new ServiceCollection();
        new LinuxPlatformServiceRegistrar().RegisterPlatformServices(services);

        using var provider = services.BuildServiceProvider();
        Assert.IsType<LinuxEnvironmentVariables>(provider.GetRequiredService<ILinuxEnvironmentVariables>());
        Assert.IsType<LinuxEnvironmentDetector>(provider.GetRequiredService<ILinuxEnvironmentDetector>());
        Assert.IsType<LinuxDisplaySessionService>(provider.GetRequiredService<IDisplaySessionService>());
        Assert.IsType<LinuxEnvironmentInfoProvider>(provider.GetRequiredService<IEnvironmentInfoProvider>());
        var policy = provider.GetRequiredService<IPlaybackBehaviorPolicy>();
        var simulatorFactory = provider.GetRequiredService<Func<IInputSimulator>>();
        var captureFactory = provider.GetRequiredService<Func<IInputCapture>>();

        Assert.True(policy.PreferRelativeForAbsoluteMoves);
        Assert.True(policy.UseHybridAbsoluteDragMovement);
        Assert.NotNull(simulatorFactory);
        Assert.NotNull(captureFactory);
    }
}
