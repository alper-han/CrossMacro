using CrossMacro.Platform.Linux.Clipboard;

namespace CrossMacro.Platform.Linux.Tests.DependencyInjection;

public sealed class LinuxPlatformServiceRegistrarTests
{
    [Fact]
    public void RegisterPlatformServices_WhenServiceCollectionIsNull_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => new LinuxPlatformServiceRegistrar(default).RegisterPlatformServices(null!));

        Assert.Equal("services", exception.ParamName);
    }

    [Theory]
    [InlineData(typeof(ILinuxEnvironmentVariables), typeof(LinuxEnvironmentVariables))]
    [InlineData(typeof(ILinuxEnvironmentDetector), typeof(LinuxEnvironmentDetector))]
    [InlineData(typeof(IDisplaySessionService), typeof(LinuxDisplaySessionService))]
    [InlineData(typeof(IEnvironmentInfoProvider), typeof(LinuxEnvironmentInfoProvider))]
    [InlineData(typeof(IPermissionChecker), typeof(LinuxPermissionChecker))]
    [InlineData(typeof(ICoordinateStrategyFactory), typeof(LinuxCoordinateStrategyFactory))]
    [InlineData(typeof(IPlatformStartupNotificationProvider), typeof(GsrCompatibilityService))]
    [InlineData(typeof(ILinuxDaemonHandshakeProbe), typeof(LinuxDaemonHandshakeProbe))]
    [InlineData(typeof(ILinuxDaemonSocketAccessProbe), typeof(LinuxDaemonSocketAccessProbe))]
    [InlineData(typeof(IX11ScreenCaptureSupportProbe), typeof(X11ScreenCaptureSupportProbe))]
    [InlineData(typeof(IX11ScreenCapture), typeof(X11ScreenCapture))]
    [InlineData(typeof(IWlrScreencopyCapture), typeof(WlrScreencopyCapture))]
    [InlineData(typeof(IKWinScreenShotCapture), typeof(KWinScreenShotCapture))]
    public void RegisterPlatformServices_ResolvesTheLinuxImplementation(Type serviceType, Type expectedImplementation)
    {
        var services = new ServiceCollection();
        new LinuxPlatformServiceRegistrar(CreateEnvironment(flatpakId: null, appImage: null)).RegisterPlatformServices(services);

        using var provider = services.BuildServiceProvider();

        Assert.IsType(expectedImplementation, provider.GetRequiredService(serviceType));
    }

    [Fact]
    public void RegisterPlatformServices_ResolvesInputFactories()
    {
        var services = new ServiceCollection();
        new LinuxPlatformServiceRegistrar(CreateEnvironment(flatpakId: null, appImage: null)).RegisterPlatformServices(services);

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<Func<IInputSimulator>>());
        Assert.NotNull(provider.GetRequiredService<Func<IInputCapture>>());
    }

    [Fact]
    public void RegisterPlatformServices_BindsTheProvidedEnvironmentSnapshot()
    {
        var environment = default(LinuxEnvironmentSnapshot) with
        {
            FlatpakId = "com.example.CrossMacro",
            SessionType = "wayland",
            WaylandDisplay = "wayland-test",
            CurrentDesktop = "GNOME",
            GdmSession = "gnome",
            RuntimeDir = "/run/user/1000",
            WindowButtons = "hide",
        };
        var services = new ServiceCollection();

        new LinuxPlatformServiceRegistrar(environment).RegisterPlatformServices(services);

        using var provider = services.BuildServiceProvider();
        var variables = provider.GetRequiredService<ILinuxEnvironmentVariables>();

        Assert.Equal(environment, variables.CaptureSnapshot());
    }

    [Fact]
    public void RegisterPlatformServices_SuppliesTheLinuxRuntimeContextForTheSnapshot()
    {
        var environment = CreateEnvironment(flatpakId: null, appImage: null);
        var services = new ServiceCollection();

        new LinuxPlatformServiceRegistrar(environment).RegisterPlatformServices(services);

        using var provider = services.BuildServiceProvider();
        var runtimeContext = Assert.IsType<LinuxRuntimeContext>(provider.GetRequiredService<IRuntimeContext>());
        Assert.Equal("wayland", runtimeContext.SessionType);
        Assert.Equal("wayland-test", runtimeContext.WaylandDisplay);
    }

    [Fact]
    public void RegisterPlatformServices_RoutesEveryClipboardPortToOneNativeService()
    {
        var services = new ServiceCollection();

        new LinuxPlatformServiceRegistrar(CreateEnvironment(flatpakId: null, appImage: null)).RegisterPlatformServices(services);

        using var provider = services.BuildServiceProvider();
        var native = provider.GetRequiredService<LinuxNativeClipboardService>();
        Assert.Same(native, provider.GetRequiredService<IClipboardService>());
        Assert.Same(native, provider.GetRequiredService<ILinuxClipboardService>());
        Assert.Same(native, provider.GetRequiredService<IImageClipboardService>());
        Assert.Same(native, provider.GetRequiredService<IImageClipboardReader>());
    }

    [Theory]
    [InlineData("io.github.alper_han.crossmacro", null)]
    [InlineData(null, "/tmp/CrossMacro.AppImage")]
    public void RegisterPlatformServices_PortablePackagesDoNotRegisterDaemonDiagnostics(string? flatpakId, string? appImage)
    {
        var services = new ServiceCollection();

        new LinuxPlatformServiceRegistrar(CreateEnvironment(flatpakId, appImage)).RegisterPlatformServices(services);

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(ILinuxDaemonHandshakeProbe));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(ILinuxDaemonSocketAccessProbe));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(ILinuxDirectInputQuickSetupService));
    }

    private static LinuxEnvironmentSnapshot CreateEnvironment(string? flatpakId, string? appImage) =>
        default(LinuxEnvironmentSnapshot) with
        {
            FlatpakId = flatpakId,
            AppImage = appImage,
            SessionType = "wayland",
            WaylandDisplay = "wayland-test",
            CurrentDesktop = "KDE",
            RuntimeDir = "/run/user/1000",
        };
}
