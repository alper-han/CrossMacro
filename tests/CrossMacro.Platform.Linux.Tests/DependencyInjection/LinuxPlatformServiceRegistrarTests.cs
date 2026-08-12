namespace CrossMacro.Platform.Linux.Tests.DependencyInjection;


public sealed class LinuxPlatformServiceRegistrarTests
{
    [Fact]
    public void RegisterPlatformServices_RegistersExpectedCoreAbstractions()
    {
        var services = new ServiceCollection();

        var registrar = new LinuxPlatformServiceRegistrar();

        registrar.RegisterPlatformServices(services);

        Assert.Contains(services, d => d.ServiceType == typeof(ILinuxEnvironmentDetector) && d.ImplementationFactory is not null);
        Assert.Contains(services, d => d.ServiceType == typeof(ILinuxEnvironmentVariables) && d.ImplementationFactory is null && d.ImplementationInstance is LinuxEnvironmentVariables);
        Assert.Contains(services, d => d.ServiceType == typeof(ILinuxInputCapabilityDetector) && d.ImplementationType == typeof(LinuxInputCapabilityDetector));
        Assert.Contains(services, d => d.ServiceType == typeof(IPlatformStartupNotificationProvider) && d.ImplementationType == typeof(GsrCompatibilityService));
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
            UseDaemon: "1",
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

        Assert.Equal(8, services.Count(d => d.ServiceType == typeof(IPositionProviderSelector)));
        Assert.Contains(services, d => d.ServiceType == typeof(IPositionProviderSelector) && d.ImplementationType == typeof(X11PositionProviderSelector));
        Assert.Contains(services, d => d.ServiceType == typeof(IPositionProviderSelector) && d.ImplementationType == typeof(GnomePositionProviderSelector));
        Assert.Contains(services, d => d.ServiceType == typeof(IPositionProviderSelector) && d.ImplementationType == typeof(KdePositionProviderSelector));
        Assert.Contains(services, d => d.ServiceType == typeof(IPositionProviderSelector) && d.ImplementationType == typeof(HyprlandPositionProviderSelector));
        Assert.Contains(services, d => d.ServiceType == typeof(IPositionProviderSelector) && d.ImplementationType == typeof(WayfirePositionProviderSelector));
        Assert.Contains(services, d => d.ServiceType == typeof(IPositionProviderSelector) && d.ImplementationType == typeof(NiriPositionProviderSelector));
        Assert.Contains(services, d => d.ServiceType == typeof(IPositionProviderSelector) && d.ImplementationType == typeof(CosmicPositionProviderSelector));
        Assert.Contains(services, d => d.ServiceType == typeof(IPositionProviderSelector) && d.ImplementationType == typeof(SwayPositionProviderSelector));
    }

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
