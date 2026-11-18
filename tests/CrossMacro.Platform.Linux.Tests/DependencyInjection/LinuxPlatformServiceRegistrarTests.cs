namespace CrossMacro.Platform.Linux.Tests.DependencyInjection;


public sealed class LinuxPlatformServiceRegistrarTests
{
    [Fact]
    public void RegisterPlatformServices_WhenServiceCollectionIsNull_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => LinuxPlatformServiceRegistrar.RegisterPlatformServices(null!, default));

        Assert.Equal("services", exception.ParamName);
    }

    [Fact]
    public void RegisterPlatformServices_RegistersExpectedCoreAbstractions()
    {
        var services = new ServiceCollection();

        LinuxPlatformServiceRegistrar.RegisterPlatformServices(
            services,
            CreateEnvironment(flatpakId: null, appImage: null));

        Assert.Contains(services, d => d.ServiceType == typeof(ILinuxEnvironmentDetector) && d.ImplementationFactory is not null);
        Assert.Contains(services, d => d.ServiceType == typeof(ILinuxEnvironmentVariables) && d.ImplementationFactory is null && d.ImplementationInstance is LinuxEnvironmentVariables);
        Assert.Contains(services, d => d.ServiceType == typeof(ILinuxInputCapabilitySnapshotProvider) && d.ImplementationFactory is not null);
        Assert.Contains(services, d => d.ServiceType == typeof(IPlatformStartupNotificationProvider) && d.ImplementationType == typeof(GsrCompatibilityService));
        Assert.Contains(services, d => d.ServiceType == typeof(ILinuxDaemonHandshakeProbe) && d.ImplementationType == typeof(LinuxDaemonHandshakeProbe));
        Assert.Contains(services, d => d.ServiceType == typeof(ILinuxDaemonSocketAccessProbe) && d.ImplementationType == typeof(LinuxDaemonSocketAccessProbe));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(LinuxInputProbeUtilities));
        Assert.Contains(services, d => d.ServiceType == typeof(IEnvironmentInfoProvider) && d.ImplementationFactory is not null);
        Assert.Contains(services, d => d.ServiceType == typeof(IDisplaySessionService) && d.ImplementationFactory is not null);
        Assert.Contains(services, d => d.ServiceType == typeof(IPermissionChecker) && d.ImplementationType == typeof(LinuxPermissionChecker));
        Assert.Contains(services, d => d.ServiceType == typeof(ICoordinateStrategyFactory) && d.ImplementationType == typeof(LinuxCoordinateStrategyFactory));
        Assert.Contains(services, d => d.ServiceType == typeof(LinuxQuickSetupIdentityResolver) && d.ImplementationType == typeof(LinuxQuickSetupIdentityResolver));
        Assert.Contains(services, d => d.ServiceType == typeof(LinuxQuickSetupExecutor) && d.ImplementationType == typeof(LinuxQuickSetupExecutor));
        Assert.Contains(services, d => d.ServiceType == typeof(FlatpakHostCommandLauncher) && d.ImplementationType == typeof(FlatpakHostCommandLauncher));
        Assert.Contains(services, d => d.ServiceType == typeof(DirectPolkitHostCommandLauncher) && d.ImplementationType == typeof(DirectPolkitHostCommandLauncher));
        Assert.Contains(services, d => d.ServiceType == typeof(IFlatpakQuickSetupService) && d.ImplementationFactory is not null);
        Assert.Contains(services, d => d.ServiceType == typeof(IAppImageQuickSetupService) && d.ImplementationFactory is not null);
        Assert.Contains(services, d => d.ServiceType == typeof(ILinuxDirectInputQuickSetupService) && d.ImplementationFactory is not null);
        Assert.Contains(services, d => d.ServiceType == typeof(PortalScreenCastRestoreTokenStore) && d.ImplementationType == typeof(PortalScreenCastRestoreTokenStore));
        Assert.Contains(services, d => d.ServiceType == typeof(IPortalScreenCastRestoreStateService) && d.ImplementationFactory is not null);
        Assert.Contains(services, d => d.ServiceType == typeof(IPortalScreenCastSessionFactory) && d.ImplementationFactory is not null);
        Assert.Contains(services, d => d.ServiceType == typeof(IX11ScreenCaptureSupportProbe) && d.ImplementationFactory is not null);
        Assert.Contains(services, d => d.ServiceType == typeof(IX11ScreenCapture) && d.ImplementationType == typeof(X11ScreenCapture));
        _ = Assert.Single(services, d => d.ServiceType == typeof(IInputSimulatorPool));
        Assert.Contains(services, d => d.ServiceType == typeof(Func<IInputSimulator>));
        Assert.Contains(services, d => d.ServiceType == typeof(Func<IInputCapture>));
    }

    [Fact]
    public void RegisterPlatformServices_BindsTheProvidedEnvironmentSnapshot()
    {
        var environment = new LinuxEnvironmentSnapshot(
            FlatpakId: "com.example.CrossMacro",
            AppImage: null,
            SessionType: "wayland",
            WaylandDisplay: "wayland-test",
            Display: null,
            CurrentDesktop: "GNOME",
            GdmSession: "gnome",
            HyprlandInstanceSignature: null,
            RuntimeDir: "/run/user/1000",
            WayfireSocket: null,
            SwaySocket: null,
            WindowButtons: "hide");
        var services = new ServiceCollection();

        LinuxPlatformServiceRegistrar.RegisterPlatformServices(services, environment);

        using var provider = services.BuildServiceProvider();
        var variables = provider.GetRequiredService<ILinuxEnvironmentVariables>();

        Assert.Equal(environment, variables.CaptureSnapshot());
    }

    [Theory]
    [InlineData("io.github.alper_han.crossmacro", null)]
    [InlineData(null, "/tmp/CrossMacro.AppImage")]
    public void RegisterPlatformServices_PortablePackagesDoNotRegisterDaemonDiagnostics(string? flatpakId, string? appImage)
    {
        var services = new ServiceCollection();

        LinuxPlatformServiceRegistrar.RegisterPlatformServices(
            services,
            CreateEnvironment(flatpakId, appImage));

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(ILinuxDaemonHandshakeProbe));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(ILinuxDaemonSocketAccessProbe));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(ILinuxDirectInputQuickSetupService));
    }

    private static LinuxEnvironmentSnapshot CreateEnvironment(string? flatpakId, string? appImage) =>
        new(
            FlatpakId: flatpakId,
            AppImage: appImage,
            SessionType: "wayland",
            WaylandDisplay: "wayland-test",
            Display: null,
            CurrentDesktop: "KDE",
            GdmSession: null,
            HyprlandInstanceSignature: null,
            RuntimeDir: "/run/user/1000",
            WayfireSocket: null,
            SwaySocket: null,
            WindowButtons: null);

    [Fact]
    public void RegisterPlatformServices_RegistersRuntimeInputServices()
    {
        var services = new ServiceCollection();
        new LinuxPlatformServiceRegistrar().RegisterPlatformServices(services);

        using var provider = services.BuildServiceProvider();
        _ = Assert.IsType<LinuxEnvironmentVariables>(provider.GetRequiredService<ILinuxEnvironmentVariables>());
        _ = Assert.IsType<LinuxEnvironmentDetector>(provider.GetRequiredService<ILinuxEnvironmentDetector>());
        _ = Assert.IsType<LinuxDisplaySessionService>(provider.GetRequiredService<IDisplaySessionService>());
        _ = Assert.IsType<LinuxEnvironmentInfoProvider>(provider.GetRequiredService<IEnvironmentInfoProvider>());
        var simulatorFactory = provider.GetRequiredService<Func<IInputSimulator>>();
        var captureFactory = provider.GetRequiredService<Func<IInputCapture>>();

        Assert.NotNull(simulatorFactory);
        Assert.NotNull(captureFactory);
    }

    [Fact]
    public void RegisterPlatformServices_RegistersNativeWlrScreencopyCapture()
    {
        var services = new ServiceCollection();
        new LinuxPlatformServiceRegistrar().RegisterPlatformServices(services);

        using var provider = services.BuildServiceProvider();
        var probe = provider.GetRequiredService<IWlrScreencopySupportProbe>();
        var capture = provider.GetRequiredService<IWlrScreencopyCapture>();

        _ = Assert.IsType<WlrScreencopyCapture>(probe);
        _ = Assert.IsType<WlrScreencopyCapture>(capture);
        Assert.NotSame(probe, capture);
    }

    [Fact]
    public void RegisterPlatformServices_RegistersKWinScreenShot2Capture()
    {
        var services = new ServiceCollection();
        new LinuxPlatformServiceRegistrar().RegisterPlatformServices(services);

        var probe = Assert.Single(services, d => d.ServiceType == typeof(IKWinScreenShotSupportProbe));
        var capture = Assert.Single(services, d => d.ServiceType == typeof(IKWinScreenShotCapture));

        Assert.NotNull(probe.ImplementationFactory);
        Assert.NotNull(capture.ImplementationFactory);
    }

    [KWinScreenShotRuntimeFact]
    public void RegisterPlatformServices_KWinScreenShot2CaptureResolvesOnNativeHost()
    {
        var services = new ServiceCollection();
        new LinuxPlatformServiceRegistrar().RegisterPlatformServices(services);

        using var provider = services.BuildServiceProvider();
        var probe = provider.GetRequiredService<IKWinScreenShotSupportProbe>();
        var capture = provider.GetRequiredService<IKWinScreenShotCapture>();

        _ = Assert.IsType<KWinScreenShotCapture>(probe);
        _ = Assert.IsType<KWinScreenShotCapture>(capture);
        Assert.NotSame(probe, capture);
    }

    [Fact]
    public void RegisterPlatformServices_RegistersX11ScreenCapture()
    {
        var services = new ServiceCollection();
        new LinuxPlatformServiceRegistrar().RegisterPlatformServices(services);

        using var provider = services.BuildServiceProvider();
        var probe = provider.GetRequiredService<IX11ScreenCaptureSupportProbe>();
        var capture = provider.GetRequiredService<IX11ScreenCapture>();

        _ = Assert.IsType<X11ScreenCaptureSupportProbe>(probe);
        _ = Assert.IsType<X11ScreenCapture>(capture);
        Assert.NotSame(probe, capture);
    }
}
