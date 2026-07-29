
namespace CrossMacro.Platform.MacOS.Tests.DependencyInjection;

[SupportedOSPlatform("macos")]
public sealed class MacOSPlatformServiceRegistrarTests
{
    [Fact]
    public void RegisterPlatformServices_RegistersCorePlatformServices()
    {
        var services = new ServiceCollection();

        new MacOSPlatformServiceRegistrar().RegisterPlatformServices(services);

        Assert.Equal(typeof(MacKeyboardLayoutService), services.Last(s => s.ServiceType == typeof(IKeyboardLayoutService)).ImplementationType);
        Assert.Equal(typeof(MacOSEnvironmentInfoProvider), services.Last(s => s.ServiceType == typeof(IEnvironmentInfoProvider)).ImplementationType);
        Assert.Equal(typeof(MacOSMousePositionProvider), services.Last(s => s.ServiceType == typeof(IMousePositionProvider)).ImplementationType);
        Assert.Equal(typeof(MacOSScreenFrameProvider), services.Last(s => s.ServiceType == typeof(IScreenFrameProvider)).ImplementationType);
        Assert.Equal(typeof(CoreGraphicsScreenRecordingPermissionProbe), services.Last(s => s.ServiceType == typeof(IMacOSScreenRecordingPermissionProbe)).ImplementationType);
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
        var policy = provider.GetRequiredService<IPlaybackBehaviorPolicy>();

        _ = Assert.IsType<MacOSInputCapture>(captureFactory());
        _ = Assert.IsType<MacOSInputSimulator>(simulatorFactory());
        _ = Assert.IsType<MacOSCoordinateStrategyFactory>(strategyFactory);
        _ = Assert.IsType<GenericDisplaySessionService>(displaySession);
        Assert.Null(notifier);
        Assert.False(policy.UseHybridAbsoluteDragMovement);
    }
}
