
namespace CrossMacro.Platform.Windows.Tests.DependencyInjection;

[SupportedOSPlatform("windows")]
public sealed class WindowsPlatformServiceRegistrarTests
{
    [Fact]
    public void RegisterGuiClipboardServices_RegistersNativeClipboardServices()
    {
        var services = new ServiceCollection();

        WindowsPlatformServiceRegistrar.RegisterGuiClipboardServices(services);

        var descriptor = Assert.Single(services, service => service.ServiceType == typeof(StaMessageThread));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Null(descriptor.ImplementationType);
        Assert.NotNull(descriptor.ImplementationFactory);

        Assert.Equal(
            typeof(WindowsNativeClipboardService),
            Assert.Single(services, service => service.ServiceType == typeof(IClipboardService)).ImplementationType);
        Assert.Equal(
            typeof(WindowsNativeImageClipboardService),
            Assert.Single(services, service => service.ServiceType == typeof(IImageClipboardService)).ImplementationType);
    }

    [Fact]
    public void RegisterCliClipboardServices_DefersStaMessageThreadCreation()
    {
        var services = new ServiceCollection();

        WindowsPlatformServiceRegistrar.RegisterCliClipboardServices(services);

        using var provider = services.BuildServiceProvider();
        var staThread = provider.GetRequiredService<Lazy<StaMessageThread>>();

        Assert.False(staThread.IsValueCreated);
        _ = provider.GetRequiredService<IClipboardService>();
        _ = provider.GetRequiredService<IImageClipboardService>();
        Assert.False(staThread.IsValueCreated);
    }

    [WindowsFact]
    public void RegisterPlatformServices_RegistersKeyboardLayoutService()
    {
        var services = new ServiceCollection();

        new WindowsPlatformServiceRegistrar().RegisterPlatformServices(services);

        var descriptor = services.LastOrDefault(s => s.ServiceType == typeof(IKeyboardLayoutService));
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(WindowsKeyboardLayoutService), descriptor.ImplementationType);
    }

    [WindowsFact]
    public void RegisterPlatformServices_RegistersMousePositionProvider()
    {
        var services = new ServiceCollection();

        new WindowsPlatformServiceRegistrar().RegisterPlatformServices(services);

        var descriptor = services.LastOrDefault(s => s.ServiceType == typeof(IMousePositionProvider));
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(WindowsMousePositionProvider), descriptor.ImplementationType);
    }

    [WindowsFact]
    public void RegisterPlatformServices_RegistersScreenFrameProvider()
    {
        var services = new ServiceCollection();

        new WindowsPlatformServiceRegistrar().RegisterPlatformServices(services);

        var descriptor = services.LastOrDefault(s => s.ServiceType == typeof(IScreenFrameProvider));
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(WindowsScreenFrameProvider), descriptor.ImplementationType);
    }

    [WindowsFact]
    public void RegisterPlatformServices_RegistersEnvironmentInfoProvider()
    {
        var services = new ServiceCollection();

        new WindowsPlatformServiceRegistrar().RegisterPlatformServices(services);

        var descriptor = services.LastOrDefault(s => s.ServiceType == typeof(IEnvironmentInfoProvider));
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(WindowsEnvironmentInfoProvider), descriptor.ImplementationType);
    }

    [WindowsFact]
    public void RegisterPlatformServices_RegistersCoordinateFactoryAndDisplayService()
    {
        var services = new ServiceCollection();

        new WindowsPlatformServiceRegistrar().RegisterPlatformServices(services);

        var strategyDescriptor = services.LastOrDefault(s => s.ServiceType == typeof(ICoordinateStrategyFactory));
        Assert.NotNull(strategyDescriptor);
        Assert.Equal(typeof(WindowsCoordinateStrategyFactory), strategyDescriptor.ImplementationType);

        var displayDescriptor = services.LastOrDefault(s => s.ServiceType == typeof(IDisplaySessionService));
        Assert.NotNull(displayDescriptor);
        Assert.Equal(typeof(GenericDisplaySessionService), displayDescriptor.ImplementationType);
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

}
