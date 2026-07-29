
namespace CrossMacro.Platform.Windows.Tests.DependencyInjection;

[SupportedOSPlatform("windows")]
public sealed class WindowsPlatformServiceRegistrarTests
{
    [Fact]
    public void RegisterGuiImageClipboardServices_RegistersStaMessageThreadFactory()
    {
        var services = new ServiceCollection();

        WindowsPlatformServiceRegistrar.RegisterGuiImageClipboardServices(services);

        var descriptor = Assert.Single(services, service => service.ServiceType == typeof(StaMessageThread));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Null(descriptor.ImplementationType);
        Assert.NotNull(descriptor.ImplementationFactory);
    }

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
    public void RegisterPlatformServices_RegistersScreenFrameProvider()
    {
        var services = new ServiceCollection();

        new WindowsPlatformServiceRegistrar().RegisterPlatformServices(services);

        var descriptor = services.LastOrDefault(s => s.ServiceType == typeof(IScreenFrameProvider));
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(WindowsScreenFrameProvider), descriptor!.ImplementationType);
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

        _ = Assert.IsType<WindowsInputSimulator>(simulatorFactory());
        _ = Assert.IsType<WindowsInputCapture>(captureFactory());
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

    [WindowsFact]
    public void RegisterPlatformServices_RegistersWindowsPlaybackBehaviorPolicy()
    {
        var services = new ServiceCollection();
        new WindowsPlatformServiceRegistrar().RegisterPlatformServices(services);

        using var provider = services.BuildServiceProvider();
        var policy = provider.GetRequiredService<IPlaybackBehaviorPolicy>();

        Assert.False(policy.UseHybridAbsoluteDragMovement);
    }
}
