
namespace CrossMacro.Platform.Windows.Tests.DependencyInjection;

[SupportedOSPlatform("windows")]
public sealed class WindowsPlatformServiceRegistrarTests
{
    [WindowsFact]
    public void RegisterGuiClipboardServices_ResolvesOneSharedImageClipboardServiceWithoutStartingTheStaThread()
    {
        var services = new ServiceCollection();

        WindowsPlatformServiceRegistrar.RegisterGuiClipboardServices(services);

        using var provider = services.BuildServiceProvider();
        var staThread = provider.GetRequiredService<Lazy<StaMessageThread>>();

        _ = Assert.IsType<WindowsNativeClipboardService>(provider.GetRequiredService<IClipboardService>());
        var imageService = Assert.IsType<WindowsNativeImageClipboardService>(provider.GetRequiredService<IImageClipboardService>());
        Assert.Same(imageService, provider.GetRequiredService<IImageClipboardReader>());
        Assert.False(staThread.IsValueCreated);
    }

    [WindowsFact]
    public void RegisterCliClipboardServices_DefersStaMessageThreadCreation()
    {
        var services = new ServiceCollection();

        WindowsPlatformServiceRegistrar.RegisterCliClipboardServices(services);

        using var provider = services.BuildServiceProvider();
        var staThread = provider.GetRequiredService<Lazy<StaMessageThread>>();

        Assert.False(staThread.IsValueCreated);
        _ = provider.GetRequiredService<IClipboardService>();
        _ = provider.GetRequiredService<IImageClipboardService>();
        _ = provider.GetRequiredService<IImageClipboardReader>();
        Assert.False(staThread.IsValueCreated);
    }

    [WindowsTheory]
    [InlineData(typeof(IKeyboardLayoutService), typeof(WindowsKeyboardLayoutService))]
    [InlineData(typeof(IMousePositionProvider), typeof(WindowsMousePositionProvider))]
    [InlineData(typeof(IScreenFrameProvider), typeof(WindowsScreenFrameProvider))]
    [InlineData(typeof(IEnvironmentInfoProvider), typeof(WindowsEnvironmentInfoProvider))]
    [InlineData(typeof(IWindowManager), typeof(WindowsWindowManager))]
    [InlineData(typeof(ICoordinateStrategyFactory), typeof(WindowsCoordinateStrategyFactory))]
    [InlineData(typeof(IDisplaySessionService), typeof(GenericDisplaySessionService))]
    public void RegisterPlatformServices_ResolvesTheWindowsImplementation(Type serviceType, Type expectedImplementation)
    {
        var services = new ServiceCollection();
        new WindowsPlatformServiceRegistrar().RegisterPlatformServices(services);

        using var provider = services.BuildServiceProvider();

        Assert.IsType(expectedImplementation, provider.GetRequiredService(serviceType));
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
}
