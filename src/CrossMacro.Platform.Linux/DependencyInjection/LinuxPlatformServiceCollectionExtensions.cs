
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
        _ = services.AddSingleton<ITrayHostMonitor, LinuxTrayHostMonitor>();

        _ = services.AddSingleton<ILinuxEnvironmentVariables>(new LinuxEnvironmentVariables(environment));
        _ = services.AddSingleton<ILinuxEnvironmentDetector>(sp => new LinuxEnvironmentDetector(
            sp.GetRequiredService<ILinuxEnvironmentVariables>()));
        var daemonEnabled = !environment.UsesPortableDirectInput;
        if (daemonEnabled)
        {
            _ = services.AddSingleton<ILinuxDaemonSocketAccessProbe, LinuxDaemonSocketAccessProbe>();
        }

        _ = services.AddSingleton<ILinuxInputCapabilitySnapshotProvider>(_ =>
            new LinuxInputCapabilitySnapshotProvider(daemonEnabled));
        _ = services.AddSingleton<ILinuxInputDiagnostics>(sp => new LinuxInputDiagnostics(
            sp.GetRequiredService<ILinuxInputCapabilitySnapshotProvider>(),
            sp.GetService<ILinuxDaemonSocketAccessProbe>(),
            sp.GetRequiredService<GsrCompatibilityService>().IsGsrVirtualKeyboardActive));
        AddLinuxCaptureServices(services, environment);
        _ = services.AddSingleton<IEnvironmentInfoProvider>(sp => new LinuxEnvironmentInfoProvider(
            sp.GetRequiredService<LinuxEnvironmentSnapshot>()));
        _ = services.AddSingleton<IMousePositionProvider>(sp =>
            sp.GetRequiredService<LinuxPositionProviderFactory>().Create());

        AddLinuxQuickSetupServices(services, daemonEnabled);
        AddLinuxWindowServices(services);

        _ = services.AddSingleton<IExtensionStatusNotifier>(sp =>
        {
            var provider = sp.GetRequiredService<IMousePositionProvider>();
            return provider as IExtensionStatusNotifier ?? CrossMacro.Core.Services.Extensions.NullExtensionStatusNotifier.Instance;
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
        _ = services.AddSingleton<IKWinScreenShotSupportProbe>(_ => new KWinScreenShotCapture(environment));
        _ = services.AddTransient<IKWinScreenShotCapture>(_ => new KWinScreenShotCapture(environment));
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
        _ = services.AddSingleton<IGnomeScreenReadingReadiness, GnomeScreenReadingReadiness>();
        _ = services.AddSingleton<LinuxScreenBackendRegistry>(sp => new LinuxScreenBackendRegistry([
            LinuxScreenBackendDescriptors.Ext(sp.GetRequiredService<IExtImageCopySupportProbe>(),
                support => new ExtImageCopyScreenFrameProvider(sp.GetRequiredService<IExtImageCopyCapture>(), support)),
            LinuxScreenBackendDescriptors.Wlr(sp.GetRequiredService<IWlrScreencopySupportProbe>(),
                support => new WlrScreencopyScreenFrameProvider(sp.GetRequiredService<IWlrScreencopyCapture>(), support)),
            LinuxScreenBackendDescriptors.Portal(sp.GetRequiredService<IPortalScreenCastSupportProbe>(),
                support => new PortalScreenCastScreenFrameProvider(sp.GetRequiredService<IPortalScreenCastCapture>(), support)),
            LinuxScreenBackendDescriptors.KWin(sp.GetRequiredService<IKWinScreenShotSupportProbe>(),
                support => new KWinScreenShotScreenFrameProvider(sp.GetRequiredService<IKWinScreenShotCapture>(), support)),
            LinuxScreenBackendDescriptors.Gnome(sp.GetRequiredService<IGnomeScreenReadingReadiness>(),
                support => new GnomeExtensionScreenFrameProvider(sp.GetRequiredService<GnomePositionProvider>(), support))]));
        _ = services.AddSingleton<ILinuxScreenReaderCapabilityDetector>(sp => new LinuxScreenReaderCapabilityDetector(
            sp.GetRequiredService<LinuxScreenBackendRegistry>(),
            sp.GetRequiredService<IGnomeScreenReadingReadiness>()));
        _ = services.AddSingleton<ILinuxCapabilitySnapshotProvider>(sp => new LinuxCapabilitySnapshotProvider(
            sp.GetRequiredService<ILinuxEnvironmentVariables>(),
            sp.GetRequiredService<ILinuxInputCapabilitySnapshotProvider>(),
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

    private static void AddLinuxQuickSetupServices(IServiceCollection services, bool daemonEnabled)
    {
        _ = services.AddSingleton<GsrCompatibilityService>();
        _ = services.AddSingleton<IPlatformStartupNotificationProvider>(sp => sp.GetRequiredService<GsrCompatibilityService>());
        _ = services.AddSingleton<LinuxQuickSetupIdentityResolver>();
        _ = services.AddSingleton<LinuxQuickSetupExecutor>();
        _ = services.AddSingleton<FlatpakHostCommandLauncher>();
        _ = services.AddSingleton<DirectPolkitHostCommandLauncher>();
        _ = services.AddSingleton<IFlatpakQuickSetupService>(sp => new FlatpakQuickSetupService(
            sp.GetRequiredService<LinuxEnvironmentSnapshot>(),
            sp.GetRequiredService<LinuxQuickSetupExecutor>(),
            sp.GetRequiredService<FlatpakHostCommandLauncher>(),
            sp.GetRequiredService<ILinuxCapabilitySnapshotProvider>()));
        _ = services.AddSingleton<IAppImageQuickSetupService>(sp => new AppImageQuickSetupService(
            sp.GetRequiredService<LinuxEnvironmentSnapshot>(),
            sp.GetRequiredService<ILinuxCapabilitySnapshotProvider>(),
            sp.GetRequiredService<LinuxQuickSetupExecutor>(),
            sp.GetRequiredService<DirectPolkitHostCommandLauncher>()));
        if (daemonEnabled)
        {
            _ = services.AddSingleton<ILinuxDirectInputQuickSetupService>(sp => new DaemonFallbackQuickSetupService(
                sp.GetRequiredService<ILinuxCapabilitySnapshotProvider>(),
                sp.GetRequiredService<LinuxQuickSetupExecutor>(),
                sp.GetRequiredService<DirectPolkitHostCommandLauncher>()));
        }
    }

    private static void AddLinuxWindowServices(IServiceCollection services)
    {
        _ = services.AddSingleton<INiriIpcClient>(sp => new NiriIpcClient(sp.GetRequiredService<LinuxEnvironmentSnapshot>()));
        _ = services.AddSingleton<ISwayIpcClient>(sp => new SwayIpcClient(sp.GetRequiredService<LinuxEnvironmentSnapshot>()));
        _ = services.AddSingleton<HyprlandIpcClient>(sp => new HyprlandIpcClient(sp.GetRequiredService<LinuxEnvironmentSnapshot>()));
        _ = services.AddSingleton<IWindowManager>(sp => LinuxWindowManagerFactory.Create(
            sp.GetRequiredService<HyprlandIpcClient>(),
            sp.GetRequiredService<ISwayIpcClient>(),
            sp.GetRequiredService<INiriIpcClient>(),
            sp.GetRequiredService<LinuxEnvironmentSnapshot>()));
    }

    internal static void AddLinuxLegacyImplementations(this IServiceCollection services)
    {
        _ = services.AddTransient<LinuxInputSimulator>();

        _ = services.AddTransient<LinuxInputCapture>();
    }

    internal static void AddLinuxIpcImplementations(this IServiceCollection services)
    {
        _ = services.AddTransient<LinuxIpcInputSimulator>(sp =>
            new LinuxIpcInputSimulator(
                sp.GetRequiredService<IpcClient>(),
                () => sp.GetRequiredService<ILinuxInputCapabilitySnapshotProvider>().GetSnapshot().DaemonHandshake.Status is LinuxDaemonHandshakeStatus.Success));

        _ = services.AddTransient<LinuxIpcInputCapture>(sp =>
            new LinuxIpcInputCapture(
                sp.GetRequiredService<IpcClient>(),
                isSupportedProbe: () => sp.GetRequiredService<ILinuxInputCapabilitySnapshotProvider>().GetSnapshot().DaemonHandshake.Status is LinuxDaemonHandshakeStatus.Success));
    }

    internal static void AddLinuxX11Implementations(this IServiceCollection services)
    {
        _ = services.AddTransient<X11InputSimulator>();

        _ = services.AddTransient<X11AbsoluteCapture>();
        _ = services.AddTransient<X11RelativeCapture>();

        _ = services.AddTransient<X11InputCapture>();
    }

    internal static void AddLinuxFactories(this IServiceCollection services)
    {
        _ = services.AddSingleton<LinuxPositionProviderFactory>(sp => new LinuxPositionProviderFactory(
            sp.GetRequiredService<ILinuxCapabilitySnapshotProvider>(),
            sp.GetRequiredService<GnomePositionProvider>(),
            sp.GetRequiredService<KdePositionProvider>(),
            static () => WaylandCursorPositionProvider.TryCreate(CancellationToken.None)));

        _ = services.AddSingleton<LinuxSimulatorFactory>(sp => new LinuxSimulatorFactory(
            sp.GetRequiredService<ILinuxCapabilitySnapshotProvider>(),
            () => sp.GetRequiredService<LinuxInputSimulator>(),
            () => sp.GetRequiredService<LinuxIpcInputSimulator>(),
            () => sp.GetRequiredService<X11InputSimulator>(),
            sp.GetRequiredService<IMousePositionProvider>()));

        _ = services.AddSingleton<LinuxCaptureFactory>(sp => new LinuxCaptureFactory(
            sp.GetRequiredService<ILinuxCapabilitySnapshotProvider>(),
            () => sp.GetRequiredService<LinuxInputCapture>(),
            () => sp.GetRequiredService<LinuxIpcInputCapture>(),
            () => sp.GetRequiredService<X11InputCapture>()));

        _ = services.AddSingleton<LinuxScreenFrameProviderFactory>(sp => new LinuxScreenFrameProviderFactory(
            sp.GetRequiredService<ILinuxCapabilitySnapshotProvider>(),
            sp.GetRequiredService<ILinuxScreenReaderCapabilityDetector>(),
            sp.GetRequiredService<LinuxScreenBackendRegistry>(),
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
