
namespace CrossMacro.Platform.Linux.DependencyInjection;

internal static class LinuxPlatformServiceCollectionExtensions
{
    internal static void AddLinuxCoreServices(this IServiceCollection services, LinuxEnvironmentSnapshot environment)
    {
        _ = services.AddSingleton<ILinuxLayoutDetector, LinuxLayoutDetector>();
        _ = services.AddSingleton<IXkbStateManager, XkbStateManager>();
        _ = services.AddSingleton<IKeyCodeMapper, KeyCodeMapper>();
        _ = services.AddSingleton<ILinuxKeyCodeMapper>(sp =>
            new LinuxKeyCodeMapper(sp.GetRequiredService<IXkbStateManager>()));
        _ = services.AddSingleton<IKeyboardLayoutService, LinuxKeyboardLayoutService>();
        _ = services.AddSingleton<IpcClient>();
        _ = services.AddSingleton<LinuxNativeClipboardService>();

        _ = services.AddSingleton<ILinuxEnvironmentVariables>(new LinuxEnvironmentVariables(environment));
        _ = services.AddSingleton<ILinuxEnvironmentDetector>(sp => new LinuxEnvironmentDetector(
            sp.GetRequiredService<ILinuxEnvironmentVariables>()));
        var daemonEnabled = !environment.UsesPortableDirectInput;
        if (daemonEnabled)
        {
            _ = services.AddSingleton<ILinuxDaemonHandshakeProbe, LinuxDaemonHandshakeProbe>();
            _ = services.AddSingleton<ILinuxDaemonSocketAccessProbe, LinuxDaemonSocketAccessProbe>();
        }

        _ = services.AddSingleton<ILinuxInputCapabilitySnapshotProvider>(
            new LinuxInputCapabilitySnapshotProvider(daemonEnabled));
        _ = services.AddSingleton<ILinuxInputCapabilityDetector>(
            new LinuxInputCapabilityDetector(daemonEnabled));
        AddLinuxCaptureServices(services, environment);
        _ = services.AddSingleton<IEnvironmentInfoProvider>(sp => new LinuxEnvironmentInfoProvider(
            sp.GetRequiredService<LinuxEnvironmentSnapshot>()));
        _ = services.AddSingleton<IMousePositionProvider>(sp =>
            sp.GetRequiredService<LinuxPositionProviderFactory>().Create());

        AddLinuxQuickSetupServices(services);
        AddLinuxWindowServices(services);

        _ = services.AddSingleton<IExtensionStatusNotifier>(sp =>
        {
            var provider = sp.GetRequiredService<IMousePositionProvider>();
            return provider as IExtensionStatusNotifier ?? CrossMacro.Core.Services.NullExtensionStatusNotifier.Instance;
        });

        _ = services.AddSingleton<IPermissionChecker, LinuxPermissionChecker>();
        _ = services.AddSingleton<IDisplaySessionService>(sp => new LinuxDisplaySessionService(
            sp.GetRequiredService<ILinuxInputCapabilitySnapshotProvider>(),
            sp.GetRequiredService<LinuxEnvironmentSnapshot>()));
    }

    private static void AddLinuxCaptureServices(IServiceCollection services, LinuxEnvironmentSnapshot environment)
    {
        _ = services.AddSingleton<IExtImageCopySupportProbe>(_ =>
            new WaylandExtImageCopySupportProbe(() => WaylandExtImageCopyRegistryProbe.Probe(environment)));
        _ = services.AddTransient<IExtImageCopyCapture, ExtImageCopyCapture>();
        _ = services.AddSingleton<IKWinScreenShotSupportProbe>(sp => new KWinScreenShotCapture(
            sp.GetRequiredService<ILinuxEnvironmentVariables>().CaptureSnapshot()));
        _ = services.AddTransient<IKWinScreenShotCapture>(sp => new KWinScreenShotCapture(
            sp.GetRequiredService<ILinuxCapabilitySnapshotProvider>().GetSnapshot().Environment));
        _ = services.AddSingleton<IWlrScreencopySupportProbe, WlrScreencopyCapture>();
        _ = services.AddTransient<IWlrScreencopyCapture, WlrScreencopyCapture>();
        _ = services.AddSingleton<IPortalScreenCastSupportProbe>(_ => PortalScreenCastSupportProbe.Instance);
        _ = services.AddSingleton<PortalScreenCastRestoreTokenStore>();
        _ = services.AddSingleton<IPortalScreenCastRestoreTokenStore>(sp => sp.GetRequiredService<PortalScreenCastRestoreTokenStore>());
        _ = services.AddSingleton<IPortalScreenCastRestoreStateService>(sp => sp.GetRequiredService<PortalScreenCastRestoreTokenStore>());
        _ = services.AddSingleton<IPortalScreenCastSessionFactory>(sp =>
            new PortalScreenCastDbusSessionFactory(sp.GetRequiredService<IPortalScreenCastRestoreTokenStore>()));
        _ = services.AddSingleton<IPortalPipeWireFrameCaptureFactory>(_ => PortalPipeWireFrameCaptureFactory.Instance);
        _ = services.AddTransient<IPortalScreenCastCapture, PortalScreenCastCapture>();
        _ = services.AddSingleton<IX11ScreenCaptureSupportProbe>(_ =>
            new X11ScreenCaptureSupportProbe(X11NativeApi.Instance, environment));
        _ = services.AddTransient<IX11ScreenCapture, X11ScreenCapture>();
        _ = services.AddSingleton<GnomePositionProvider>(_ => new GnomePositionProvider(environment));
        _ = services.AddSingleton<KdePositionProvider>(_ => new KdePositionProvider(environment));
        _ = services.AddSingleton<ILinuxScreenReaderCapabilityDetector>(sp => new LinuxScreenReaderCapabilityDetector(
            sp.GetRequiredService<IExtImageCopySupportProbe>(),
            sp.GetRequiredService<IWlrScreencopySupportProbe>(),
            sp.GetRequiredService<IPortalScreenCastSupportProbe>(),
            sp.GetRequiredService<IKWinScreenShotSupportProbe>(),
            sp.GetRequiredService<GnomePositionProvider>()));
        _ = services.AddSingleton<ILinuxCapabilitySnapshotProvider>(sp => new LinuxCapabilitySnapshotProvider(
            sp.GetRequiredService<ILinuxEnvironmentVariables>(),
            sp.GetRequiredService<ILinuxInputCapabilityDetector>(),
            sp.GetRequiredService<ILinuxScreenReaderCapabilityDetector>()));
        _ = services.AddSingleton<IScreenReadingCapabilityReadiness>(sp => new LinuxScreenReadingCapabilityReadiness(
            sp.GetRequiredService<ILinuxScreenReaderCapabilityDetector>(),
            sp.GetRequiredService<ILinuxCapabilitySnapshotProvider>()));
        _ = services.AddSingleton<IScreenReadingDiagnosticProvider>(sp => new LinuxScreenReadingDiagnosticProvider(
            sp.GetRequiredService<ILinuxEnvironmentDetector>(),
            sp.GetRequiredService<IRuntimeContext>(),
            sp.GetRequiredService<ILinuxScreenReaderCapabilityDetector>(),
            sp.GetRequiredService<ILinuxCapabilitySnapshotProvider>(),
            sp.GetRequiredService<IX11ScreenCaptureSupportProbe>()));
    }

    private static void AddLinuxQuickSetupServices(IServiceCollection services)
    {
        _ = services.AddSingleton<IPlatformStartupNotificationProvider, GsrCompatibilityService>();
        _ = services.AddSingleton<LinuxQuickSetupIdentityResolver>();
        _ = services.AddSingleton<LinuxQuickSetupExecutor>();
        _ = services.AddSingleton<FlatpakHostCommandLauncher>();
        _ = services.AddSingleton<DirectPolkitHostCommandLauncher>();
        _ = services.AddSingleton<IFlatpakQuickSetupService>(sp => new FlatpakQuickSetupService(
            sp.GetRequiredService<ILinuxCapabilitySnapshotProvider>().GetSnapshot().Environment,
            sp.GetRequiredService<LinuxQuickSetupExecutor>(),
            sp.GetRequiredService<FlatpakHostCommandLauncher>(),
            sp.GetRequiredService<ILinuxCapabilitySnapshotProvider>()));
        _ = services.AddSingleton<IAppImageQuickSetupService>(sp => new AppImageQuickSetupService(
            sp.GetRequiredService<ILinuxCapabilitySnapshotProvider>(),
            sp.GetRequiredService<LinuxQuickSetupExecutor>(),
            sp.GetRequiredService<DirectPolkitHostCommandLauncher>()));
    }

    private static void AddLinuxWindowServices(IServiceCollection services)
    {
        _ = services.AddSingleton<INiriIpcClient>(sp => new NiriIpcClient(sp.GetRequiredService<LinuxEnvironmentSnapshot>()));
        _ = services.AddSingleton<ISwayIpcClient>(sp => new SwayIpcClient(sp.GetRequiredService<LinuxEnvironmentSnapshot>()));
        _ = services.AddSingleton<HyprlandIpcClient>(sp => new HyprlandIpcClient(sp.GetRequiredService<LinuxEnvironmentSnapshot>()));
        _ = services.AddSingleton<IWindowManager>(sp =>
        {
            var ipcClient = sp.GetRequiredService<HyprlandIpcClient>();
            if (ipcClient.IsAvailable)
            {
                return new HyprlandWindowManager(ipcClient);
            }

            var swayClient = sp.GetRequiredService<ISwayIpcClient>();
            if (swayClient.IsAvailable)
            {
                return new DisplayServer.Wayland.SwayWindowManager(swayClient);
            }

            var niriClient = sp.GetRequiredService<INiriIpcClient>();
            if (niriClient.IsAvailable)
            {
                return new DisplayServer.Wayland.NiriWindowManager(niriClient);
            }

            var desktop = sp.GetRequiredService<ILinuxCapabilitySnapshotProvider>().GetSnapshot().Environment.CurrentDesktop;
            if (desktop is not null)
            {
                if (desktop.Contains("KDE", System.StringComparison.OrdinalIgnoreCase))
                {
                    return new DisplayServer.Wayland.KdeWindowManager();
                }
                if (desktop.Contains("GNOME", System.StringComparison.OrdinalIgnoreCase))
                {
                    return new DisplayServer.Wayland.GnomeWindowManager();
                }
            }

            return new NullWindowManager(op =>
                Log.Warning("[NullWindowManager] Window management is not supported on this platform. Operation: {Op}", op));
        });
    }

    internal static void AddLinuxLegacyImplementations(this IServiceCollection services)
    {
        _ = services.AddTransient<LinuxInputSimulator>();
        _ = services.AddSingleton<Func<LinuxInputSimulator>>(sp =>
            () => sp.GetRequiredService<LinuxInputSimulator>());

        _ = services.AddTransient<LinuxInputCapture>();
        _ = services.AddSingleton<Func<LinuxInputCapture>>(sp =>
            () => sp.GetRequiredService<LinuxInputCapture>());
    }

    internal static void AddLinuxIpcImplementations(this IServiceCollection services)
    {
        _ = services.AddTransient<LinuxIpcInputSimulator>(sp =>
            new LinuxIpcInputSimulator(
                sp.GetRequiredService<IpcClient>(),
                () => sp.GetRequiredService<ILinuxInputCapabilityDetector>().CanConnectToDaemon));
        _ = services.AddSingleton<Func<LinuxIpcInputSimulator>>(sp =>
            () => sp.GetRequiredService<LinuxIpcInputSimulator>());

        _ = services.AddTransient<LinuxIpcInputCapture>(sp =>
            new LinuxIpcInputCapture(
                sp.GetRequiredService<IpcClient>(),
                isSupportedProbe: () => sp.GetRequiredService<ILinuxInputCapabilityDetector>().CanConnectToDaemon));
        _ = services.AddSingleton<Func<LinuxIpcInputCapture>>(sp =>
            () => sp.GetRequiredService<LinuxIpcInputCapture>());
    }

    internal static void AddLinuxX11Implementations(this IServiceCollection services)
    {
        _ = services.AddTransient<X11InputSimulator>();
        _ = services.AddSingleton<Func<X11InputSimulator>>(sp =>
            () => sp.GetRequiredService<X11InputSimulator>());

        _ = services.AddTransient<X11AbsoluteCapture>();
        _ = services.AddTransient<X11RelativeCapture>();

        _ = services.AddTransient<X11InputCapture>();
        _ = services.AddSingleton<Func<X11InputCapture>>(sp =>
            () => sp.GetRequiredService<X11InputCapture>());
    }

    internal static void AddLinuxFactories(this IServiceCollection services)
    {
        _ = services.AddSingleton<LinuxPositionProviderFactory>(sp => new LinuxPositionProviderFactory(
            sp.GetServices<IPositionProviderSelector>(),
            sp.GetRequiredService<ILinuxCapabilitySnapshotProvider>(),
            static () => WaylandCursorPositionProvider.TryCreate(CancellationToken.None)));

        _ = services.AddSingleton<LinuxSimulatorFactory>(sp => new LinuxSimulatorFactory(
            sp.GetRequiredService<ILinuxCapabilitySnapshotProvider>(),
            sp.GetRequiredService<Func<LinuxInputSimulator>>(),
            sp.GetRequiredService<Func<LinuxIpcInputSimulator>>(),
            sp.GetRequiredService<Func<X11InputSimulator>>(),
            sp.GetRequiredService<IMousePositionProvider>()));

        _ = services.AddSingleton<LinuxCaptureFactory>(sp => new LinuxCaptureFactory(
            sp.GetRequiredService<ILinuxCapabilitySnapshotProvider>(),
            sp.GetRequiredService<Func<LinuxInputCapture>>(),
            sp.GetRequiredService<Func<LinuxIpcInputCapture>>(),
            sp.GetRequiredService<Func<X11InputCapture>>()));

        _ = services.AddSingleton<LinuxScreenFrameProviderFactory>(sp => new LinuxScreenFrameProviderFactory(
            sp.GetRequiredService<ILinuxEnvironmentDetector>(),
            sp.GetRequiredService<IRuntimeContext>(),
            sp.GetRequiredService<ILinuxScreenReaderCapabilityDetector>(),
            sp.GetRequiredService<ILinuxCapabilitySnapshotProvider>(),
            support => new ExtImageCopyScreenFrameProvider(sp.GetRequiredService<IExtImageCopyCapture>(), support),
            support => new WlrScreencopyScreenFrameProvider(sp.GetRequiredService<IWlrScreencopyCapture>(), support),
            support => new PortalScreenCastScreenFrameProvider(sp.GetRequiredService<IPortalScreenCastCapture>(), support),
            support => new KWinScreenShotScreenFrameProvider(sp.GetRequiredService<IKWinScreenShotCapture>(), support),
            support => new GnomeExtensionScreenFrameProvider(sp.GetRequiredService<GnomePositionProvider>(), support),
            sp.GetRequiredService<IX11ScreenCaptureSupportProbe>(),
            support => new X11ScreenFrameProvider(sp.GetRequiredService<IX11ScreenCapture>(), support)));
        _ = services.AddSingleton<IScreenFrameProvider>(sp => sp.GetRequiredService<LinuxScreenFrameProviderFactory>().Create());
    }

    internal static void AddLinuxInputFactories(this IServiceCollection services)
    {
        _ = services.AddTransient<Func<IInputSimulator>>(sp =>
        {
            var factory = sp.GetRequiredService<LinuxSimulatorFactory>();
            return factory.Create;
        });

        _ = services.AddTransient<Func<IInputCapture>>(sp =>
        {
            var factory = sp.GetRequiredService<LinuxCaptureFactory>();
            return () => factory.Create();
        });
    }

    internal static void AddLinuxStrategySelectors(this IServiceCollection services)
    {
        _ = services.AddSingleton<ICoordinateStrategySelector, ForceRelativeStrategySelector>();
        _ = services.AddSingleton<ICoordinateStrategySelector, WaylandAbsoluteStrategySelector>();
        _ = services.AddSingleton<ICoordinateStrategySelector, WaylandRelativeStrategySelector>();
        _ = services.AddSingleton<ICoordinateStrategySelector, X11AbsoluteStrategySelector>();
        _ = services.AddSingleton<ICoordinateStrategySelector, X11RelativeStrategySelector>();
    }

    internal static void AddLinuxPositionProviderSelectors(this IServiceCollection services)
    {
        _ = services.AddSingleton<IPositionProviderSelector, X11PositionProviderSelector>();
        _ = services.AddSingleton<IPositionProviderSelector, GnomePositionProviderSelector>();
        _ = services.AddSingleton<IPositionProviderSelector, KdePositionProviderSelector>();
        _ = services.AddSingleton<IPositionProviderSelector, HyprlandPositionProviderSelector>();
        _ = services.AddSingleton<IPositionProviderSelector, WayfirePositionProviderSelector>();
        _ = services.AddSingleton<IPositionProviderSelector, NiriPositionProviderSelector>();
        _ = services.AddSingleton<IPositionProviderSelector, CosmicPositionProviderSelector>();
        _ = services.AddSingleton<IPositionProviderSelector, SwayPositionProviderSelector>();
    }

    internal static void AddLinuxCoordinateStrategy(this IServiceCollection services)
    {
        _ = services.AddSingleton<ICoordinateStrategyFactory, LinuxCoordinateStrategyFactory>();
    }

    internal static void AddLinuxInputSimulatorPool(this IServiceCollection services)
    {
        _ = services.AddSingleton<IInputSimulatorPool>(static sp =>
        {
            var factory = sp.GetRequiredService<Func<IInputSimulator>>();
            return new InputSimulatorPool(factory);
        });
    }
}
