
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
        Assert.Equal(typeof(MacOSWindowManager), services.Last(s => s.ServiceType == typeof(IWindowManager)).ImplementationType);
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

        _ = Assert.IsType<MacOSInputCapture>(captureFactory());
        _ = Assert.IsType<MacOSInputSimulator>(simulatorFactory());
        _ = Assert.IsType<MacOSCoordinateStrategyFactory>(strategyFactory);
        _ = Assert.IsType<GenericDisplaySessionService>(displaySession);
        Assert.Null(notifier);
    }

    [Fact]
    public void RegisterNativeClipboardServices_UsesOneNativePasteboardServiceForAllClipboardContracts()
    {
        var services = new ServiceCollection();

        MacOSPlatformServiceRegistrar.RegisterNativeClipboardServices(services);
        using var provider = services.BuildServiceProvider();

        var clipboard = provider.GetRequiredService<IClipboardService>();
        var imageWriter = provider.GetRequiredService<IImageClipboardService>();
        var imageReader = provider.GetRequiredService<IImageClipboardReader>();

        _ = Assert.IsType<MacOSNativeClipboardService>(clipboard);
        Assert.Same(clipboard, imageWriter);
        Assert.Same(clipboard, imageReader);
    }

    [Fact]
    public void Registrations_BuildAndResolveWithValidationEnabled()
    {
        var services = new ServiceCollection();
        new MacOSPlatformServiceRegistrar().RegisterPlatformServices(services);
        MacOSPlatformServiceRegistrar.RegisterNativeClipboardServices(services);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        Assert.NotNull(provider.GetRequiredService<IWindowManager>());
        Assert.NotNull(provider.GetRequiredService<IClipboardService>());
        Assert.NotNull(provider.GetRequiredService<IImageClipboardService>());
        Assert.NotNull(provider.GetRequiredService<IImageClipboardReader>());
        Assert.NotNull(provider.GetRequiredService<Func<IInputCapture>>());
        Assert.NotNull(provider.GetRequiredService<Func<IInputSimulator>>());
    }
}
