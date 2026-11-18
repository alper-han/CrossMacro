using System.Runtime.Versioning;
using CrossMacro.Core.Services.Clipboard;
using CrossMacro.Platform.Abstractions.Input.Keyboard;
using CrossMacro.Platform.Abstractions.Input.Simulation;
using CrossMacro.Platform.Abstractions.Recording.Strategies;
using CrossMacro.Platform.Abstractions.Runtime;
using CrossMacro.Platform.Abstractions.WindowManagement;
using CrossMacro.Platform.MacOS.DependencyInjection;
using CrossMacro.Platform.MacOS.Services.Clipboard;
using CrossMacro.Platform.MacOS.Services.Permissions;
using CrossMacro.Platform.MacOS.Services.Runtime;
using CrossMacro.Platform.MacOS.Services.ScreenReading;
using CrossMacro.Platform.MacOS.Strategies;
using CrossMacro.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.Platform.MacOS.Tests.DependencyInjection;

[SupportedOSPlatform("macos")]
public sealed class MacOSPlatformServiceRegistrarTests
{
    [Fact]
    public void RegisterPlatformServices_WhenServiceCollectionIsNull_ThrowsArgumentNullException()
    {
        var argumentException = Assert.Throws<ArgumentNullException>(() => new MacOSPlatformServiceRegistrar().RegisterPlatformServices(null!));

        Assert.Equal("services", argumentException.ParamName);
    }

    [MacOSTheory]
    [InlineData(typeof(IKeyboardLayoutService), typeof(MacKeyboardLayoutService))]
    [InlineData(typeof(IEnvironmentInfoProvider), typeof(MacOSEnvironmentInfoProvider))]
    [InlineData(typeof(IMousePositionProvider), typeof(MacOSMousePositionProvider))]
    [InlineData(typeof(IScreenFrameProvider), typeof(MacOSScreenFrameProvider))]
    [InlineData(typeof(IMacOSPermissionNative), typeof(MacOSPermissionNative))]
    [InlineData(typeof(MacOSPermissionCheckerService), typeof(MacOSPermissionCheckerService))]
    [InlineData(typeof(IWindowManager), typeof(MacOSWindowManager))]
    public void RegisterPlatformServices_ResolvesTheMacOSImplementation(Type serviceType, Type expectedImplementation)
    {
        var services = new ServiceCollection();
        new MacOSPlatformServiceRegistrar().RegisterPlatformServices(services);
        using var provider = services.BuildServiceProvider();

        Assert.IsType(expectedImplementation, provider.GetRequiredService(serviceType));
    }

    [MacOSFact]
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

    [MacOSFact]
    public void RegisterPlatformServices_UsesOneNativePasteboardServiceForAllClipboardContracts()
    {
        var services = new ServiceCollection();

        new MacOSPlatformServiceRegistrar().RegisterPlatformServices(services);
        using var provider = services.BuildServiceProvider();

        var clipboard = provider.GetRequiredService<IClipboardService>();
        var imageWriter = provider.GetRequiredService<IImageClipboardService>();
        var imageReader = provider.GetRequiredService<IImageClipboardReader>();

        _ = Assert.IsType<MacOSNativeClipboardService>(clipboard);
        Assert.Same(clipboard, imageWriter);
        Assert.Same(clipboard, imageReader);
    }

    [MacOSFact]
    public void Registrations_BuildAndResolveWithValidationEnabled()
    {
        var services = new ServiceCollection();
        new MacOSPlatformServiceRegistrar().RegisterPlatformServices(services);

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
