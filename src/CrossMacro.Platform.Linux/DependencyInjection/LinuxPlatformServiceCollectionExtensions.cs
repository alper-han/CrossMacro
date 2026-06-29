using System;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Infrastructure.Services.Recording.Strategies;
using CrossMacro.Packaging.Abstractions;
using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Abstractions.Diagnostics;
using CrossMacro.Platform.Linux.DisplayServer;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;
using CrossMacro.Platform.Linux.DisplayServer.X11;
using CrossMacro.Platform.Linux.Ipc;
using CrossMacro.Platform.Linux.Services;
using CrossMacro.Platform.Linux.Services.Factories;
using CrossMacro.Platform.Linux.Services.Factories.Selectors;
using CrossMacro.Platform.Linux.Services.Keyboard;
using CrossMacro.Platform.Linux.Services.QuickSetup;
using CrossMacro.Platform.Linux.Services.ScreenReading;
using CrossMacro.Platform.Linux.Strategies;
using CrossMacro.Platform.Linux.Strategies.Selectors;
using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.Platform.Linux.DependencyInjection;

internal static class LinuxPlatformServiceCollectionExtensions
{
    internal static void AddLinuxCoreServices(this IServiceCollection services)
    {
        services.AddSingleton<ILinuxLayoutDetector, LinuxLayoutDetector>();
        services.AddSingleton<IXkbStateManager, XkbStateManager>();
        services.AddSingleton<ILinuxKeyCodeMapper>(sp =>
            new LinuxKeyCodeMapper(sp.GetRequiredService<IXkbStateManager>()));
        services.AddSingleton<IKeyboardLayoutService, LinuxKeyboardLayoutService>();
        services.AddSingleton<IpcClient>();

        services.AddSingleton<ILinuxEnvironmentVariables, LinuxEnvironmentVariables>();
        services.AddSingleton<ILinuxEnvironmentDetector, LinuxEnvironmentDetector>();
        services.AddSingleton<ILinuxDaemonHandshakeProbe, LinuxDaemonHandshakeProbe>();
        services.AddSingleton<ILinuxDaemonSocketAccessProbe, LinuxDaemonSocketAccessProbe>();
        services.AddSingleton<ILinuxInputCapabilitySnapshotProvider, LinuxInputCapabilitySnapshotProvider>();
        services.AddSingleton<ILinuxInputCapabilityDetector, LinuxInputCapabilityDetector>();
        services.AddSingleton<IExtImageCopySupportProbe>(_ => WaylandExtImageCopySupportProbe.Instance);
        services.AddTransient<IExtImageCopyCapture, ExtImageCopyCapture>();
        services.AddSingleton<IKWinScreenShotSupportProbe, KWinScreenShotCapture>();
        services.AddTransient<IKWinScreenShotCapture, KWinScreenShotCapture>();
        services.AddSingleton<IWlrScreencopySupportProbe, WlrScreencopyCapture>();
        services.AddTransient<IWlrScreencopyCapture, WlrScreencopyCapture>();
        services.AddSingleton<IPortalScreenCastSupportProbe>(_ => PortalScreenCastSupportProbe.Instance);
        services.AddSingleton<IPortalScreenCastRestoreTokenStore, PortalScreenCastRestoreTokenStore>();
        services.AddSingleton<IPortalScreenCastSessionFactory>(sp =>
            new PortalScreenCastDbusSessionFactory(sp.GetRequiredService<IPortalScreenCastRestoreTokenStore>()));
        services.AddSingleton<IPortalPipeWireFrameCaptureFactory>(_ => PortalPipeWireFrameCaptureFactory.Instance);
        services.AddTransient<IPortalScreenCastCapture, PortalScreenCastCapture>();
        services.AddSingleton<IX11ScreenCaptureSupportProbe>(_ => X11ScreenCaptureSupportProbe.Instance);
        services.AddTransient<IX11ScreenCapture, X11ScreenCapture>();
        services.AddSingleton<GnomePositionProvider>();
        services.AddSingleton<ILinuxScreenReaderCapabilityDetector>(sp => new LinuxScreenReaderCapabilityDetector(
            sp.GetRequiredService<IExtImageCopySupportProbe>(),
            sp.GetRequiredService<IWlrScreencopySupportProbe>(),
            sp.GetRequiredService<IPortalScreenCastSupportProbe>(),
            sp.GetRequiredService<IKWinScreenShotSupportProbe>(),
            sp.GetRequiredService<GnomePositionProvider>()));
        services.AddSingleton<IScreenReadingDiagnosticProvider, LinuxScreenReadingDiagnosticProvider>();
        services.AddSingleton<IPlatformStartupNotificationProvider, GsrCompatibilityService>();
        services.AddSingleton<LinuxQuickSetupIdentityResolver>();
        services.AddSingleton<LinuxQuickSetupScriptBuilder>();
        services.AddSingleton<LinuxQuickSetupExecutor>();
        services.AddSingleton<FlatpakHostCommandLauncher>();
        services.AddSingleton<DirectPkexecHostCommandLauncher>();
        services.AddSingleton<IPlaybackBehaviorPolicy>(
            _ => new PlaybackBehaviorPolicy(useHybridAbsoluteDragMovement: true));
        services.AddSingleton<IFlatpakQuickSetupService>(sp =>
            new FlatpakQuickSetupService(
                Environment.GetEnvironmentVariable,
                sp.GetRequiredService<LinuxQuickSetupExecutor>(),
                sp.GetRequiredService<FlatpakHostCommandLauncher>()));
        services.AddSingleton<IAppImageQuickSetupService>(sp =>
            new AppImageQuickSetupService(
                sp.GetRequiredService<ILinuxInputCapabilityDetector>(),
                Environment.GetEnvironmentVariable,
                sp.GetRequiredService<LinuxQuickSetupExecutor>(),
                sp.GetRequiredService<DirectPkexecHostCommandLauncher>()));

        services.AddSingleton<IEnvironmentInfoProvider, LinuxEnvironmentInfoProvider>();
        services.AddSingleton<IMousePositionProvider>(sp =>
            sp.GetRequiredService<LinuxPositionProviderFactory>().Create());

        // Window manager: Hyprland or KDE when available, NullWindowManager otherwise
        services.AddSingleton<HyprlandIpcClient>();
        services.AddSingleton<IWindowManager>(sp =>
        {
            var ipcClient = sp.GetRequiredService<HyprlandIpcClient>();
            if (ipcClient.IsAvailable)
                return new HyprlandWindowManager(ipcClient);

            var desktop = System.Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP");
            if (desktop != null)
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

            return new NullWindowManager();
        });

        services.AddSingleton<IExtensionStatusNotifier>(sp =>
        {
            var provider = sp.GetRequiredService<IMousePositionProvider>();
            return provider as IExtensionStatusNotifier ?? NullExtensionStatusNotifier.Instance;
        });

        services.AddSingleton<IPermissionChecker, LinuxPermissionChecker>();
        services.AddSingleton<IDisplaySessionService, LinuxDisplaySessionService>();
    }

    internal static void AddLinuxLegacyImplementations(this IServiceCollection services)
    {
        services.AddTransient<LinuxInputSimulator>();
        services.AddSingleton<Func<LinuxInputSimulator>>(sp =>
            () => sp.GetRequiredService<LinuxInputSimulator>());

        services.AddTransient<LinuxInputCapture>();
        services.AddSingleton<Func<LinuxInputCapture>>(sp =>
            () => sp.GetRequiredService<LinuxInputCapture>());
    }

    internal static void AddLinuxIpcImplementations(this IServiceCollection services)
    {
        services.AddTransient<LinuxIpcInputSimulator>(sp =>
            new LinuxIpcInputSimulator(
                sp.GetRequiredService<IpcClient>(),
                () => sp.GetRequiredService<ILinuxInputCapabilityDetector>().CanConnectToDaemon));
        services.AddSingleton<Func<LinuxIpcInputSimulator>>(sp =>
            () => sp.GetRequiredService<LinuxIpcInputSimulator>());

        services.AddTransient<LinuxIpcInputCapture>(sp =>
            new LinuxIpcInputCapture(
                sp.GetRequiredService<IpcClient>(),
                isSupportedProbe: () => sp.GetRequiredService<ILinuxInputCapabilityDetector>().CanConnectToDaemon));
        services.AddSingleton<Func<LinuxIpcInputCapture>>(sp =>
            () => sp.GetRequiredService<LinuxIpcInputCapture>());
    }

    internal static void AddLinuxX11Implementations(this IServiceCollection services)
    {
        services.AddTransient<X11InputSimulator>();
        services.AddSingleton<Func<X11InputSimulator>>(sp =>
            () => sp.GetRequiredService<X11InputSimulator>());

        services.AddTransient<X11AbsoluteCapture>();
        services.AddTransient<X11RelativeCapture>();

        services.AddTransient<X11InputCapture>();
        services.AddSingleton<Func<X11InputCapture>>(sp =>
            () => sp.GetRequiredService<X11InputCapture>());
    }

    internal static void AddLinuxFactories(this IServiceCollection services)
    {
        services.AddSingleton<LinuxPositionProviderFactory>();

        services.AddSingleton<LinuxSimulatorFactory>(sp => new LinuxSimulatorFactory(
            sp.GetRequiredService<ILinuxEnvironmentDetector>(),
            sp.GetRequiredService<ILinuxInputCapabilityDetector>(),
            sp.GetRequiredService<Func<LinuxInputSimulator>>(),
            sp.GetRequiredService<Func<LinuxIpcInputSimulator>>(),
            sp.GetRequiredService<Func<X11InputSimulator>>()));

        services.AddSingleton<LinuxCaptureFactory>(sp => new LinuxCaptureFactory(
            sp.GetRequiredService<ILinuxEnvironmentDetector>(),
            sp.GetRequiredService<ILinuxInputCapabilityDetector>(),
            sp.GetRequiredService<Func<LinuxInputCapture>>(),
            sp.GetRequiredService<Func<LinuxIpcInputCapture>>(),
            sp.GetRequiredService<Func<X11InputCapture>>()));

         services.AddSingleton<LinuxScreenFrameProviderFactory>(sp => new LinuxScreenFrameProviderFactory(
             sp.GetRequiredService<ILinuxEnvironmentDetector>(),
             sp.GetRequiredService<IRuntimeContext>(),
             sp.GetRequiredService<ILinuxScreenReaderCapabilityDetector>(),
             support => new ExtImageCopyScreenFrameProvider(sp.GetRequiredService<IExtImageCopyCapture>(), support),
             support => new WlrScreencopyScreenFrameProvider(sp.GetRequiredService<IWlrScreencopyCapture>(), support),
             support => new PortalScreenCastScreenFrameProvider(sp.GetRequiredService<IPortalScreenCastCapture>(), support),
             support => new KWinScreenShotScreenFrameProvider(sp.GetRequiredService<IKWinScreenShotCapture>(), support),
             support => new GnomeExtensionScreenFrameProvider(sp.GetRequiredService<GnomePositionProvider>(), support),
             sp.GetRequiredService<IX11ScreenCaptureSupportProbe>(),
             support => new X11ScreenFrameProvider(sp.GetRequiredService<IX11ScreenCapture>(), support)));
        services.AddSingleton<IScreenFrameProvider>(sp => sp.GetRequiredService<LinuxScreenFrameProviderFactory>().Create());
    }

    internal static void AddLinuxInputFactories(this IServiceCollection services)
    {
        services.AddTransient<Func<IInputSimulator>>(sp =>
        {
            var factory = sp.GetRequiredService<LinuxSimulatorFactory>();
            return () => factory.Create();
        });

        services.AddTransient<Func<IInputCapture>>(sp =>
        {
            var factory = sp.GetRequiredService<LinuxCaptureFactory>();
            return () => factory.Create();
        });
    }

    internal static void AddLinuxStrategySelectors(this IServiceCollection services)
    {
        services.AddSingleton<ICoordinateStrategySelector, ForceRelativeStrategySelector>();
        services.AddSingleton<ICoordinateStrategySelector, WaylandAbsoluteStrategySelector>();
        services.AddSingleton<ICoordinateStrategySelector, WaylandRelativeStrategySelector>();
        services.AddSingleton<ICoordinateStrategySelector, X11AbsoluteStrategySelector>();
        services.AddSingleton<ICoordinateStrategySelector, X11RelativeStrategySelector>();
    }

    internal static void AddLinuxPositionProviderSelectors(this IServiceCollection services)
    {
        services.AddSingleton<IPositionProviderSelector, X11PositionProviderSelector>();
        services.AddSingleton<IPositionProviderSelector, GnomePositionProviderSelector>();
        services.AddSingleton<IPositionProviderSelector, KdePositionProviderSelector>();
        services.AddSingleton<IPositionProviderSelector, HyprlandPositionProviderSelector>();
        services.AddSingleton<IPositionProviderSelector, WayfirePositionProviderSelector>();
        services.AddSingleton<IPositionProviderSelector, NiriPositionProviderSelector>();
        services.AddSingleton<IPositionProviderSelector, CosmicPositionProviderSelector>();
    }

    internal static void AddLinuxCoordinateStrategy(this IServiceCollection services)
    {
        services.AddSingleton<ICoordinateStrategyFactory, LinuxCoordinateStrategyFactory>();
    }

    internal static void AddLinuxInputSimulatorPool(this IServiceCollection services)
    {
        services.AddSingleton<InputSimulatorPool>(sp =>
        {
            var factory = sp.GetRequiredService<Func<IInputSimulator>>();
            return new InputSimulatorPool(factory);
        });
    }

    private sealed class NullExtensionStatusNotifier : IExtensionStatusNotifier
    {
        public static NullExtensionStatusNotifier Instance { get; } = new();

        public event EventHandler<ExtensionStatusChangedEventArgs>? ExtensionStatusUpdated
        {
            add { }
            remove { }
        }

        public event EventHandler<string>? ExtensionStatusChanged
        {
            add { }
            remove { }
        }

        public ExtensionStatusChangedEventArgs? CurrentExtensionStatus => null;

        private NullExtensionStatusNotifier()
        {
        }
    }
}
