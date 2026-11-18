using CrossMacro.Application.Profiles;
using CrossMacro.Cli.DependencyInjection;
using CrossMacro.UI.Hosting;

namespace CrossMacro.UI.Tests.DependencyInjection;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void GuiComposition_RegistersGuiOnlyServices()
    {
        var services = new ServiceCollection();

        ComposeGuiServices(services, new PlatformRegistrar(FakeRuntimeContext.Windows()));

        Assert.Contains(services, sd => sd.ServiceType == typeof(ITrayIconService));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IDialogService));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IPortalScreenReadingGuidanceService));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IUpdateService));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IExternalUrlOpener));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IThemeService));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IDesktopStartupCoordinator));
        Assert.Contains(services, sd => sd.ServiceType == typeof(ProfileLoadedMacroSessionPersistenceService));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IProfileRuntimeParticipant));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IRuntimeLogLevelService));
    }

    [Fact]
    public void GuiComposition_RegistersImageAssetPorts()
    {
        var services = new ServiceCollection();

        ComposeGuiServices(services, new PlatformRegistrar(FakeRuntimeContext.Windows()));

        Assert.Contains(services, sd => sd.ServiceType == typeof(IImageAssetCodec));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IImageAssetPreviewDecoder));
    }

    [Fact]
    public void GuiComposition_ResolvesShortcutViewModelWithItsSingletonDependencies()
    {
        var services = new ServiceCollection();
        ComposeGuiServices(services, new PlatformRegistrar(FakeRuntimeContext.Windows()));

        using var provider = services.BuildServiceProvider();
        using var viewModel = provider.GetRequiredService<ShortcutViewModel>();

        Assert.Same(provider.GetRequiredService<IGlobalHotkeyService>(), viewModel.GlobalHotkeyService);
        Assert.Same(provider.GetRequiredService<ILocalizationService>(), viewModel.LocalizationService);
        _ = Assert.IsType<MacroFileManager>(provider.GetRequiredService<IMacroFileManager>());
    }

    [Fact]
    public async Task GuiComposition_ResolvesTextExpansionViewModelThroughManagedPort()
    {
        var services = new ServiceCollection();
        ComposeGuiServices(services, new PlatformRegistrar(FakeRuntimeContext.Windows()));
        _ = services.AddSingleton(Substitute.For<IClipboardService>());
        _ = services.AddSingleton(Substitute.For<IImageClipboardService>());
        _ = services.AddSingleton(Substitute.For<IEnvironmentInfoProvider>());
        var manager = Substitute.For<IManageTextExpansion>();
        _ = manager.ListAsync(cancellationToken: CancellationToken.None).Returns([new TextExpansionEntry(":managed", "value")]);
        _ = services.AddSingleton(manager);
        _ = services.AddSingleton<IUiDispatcher>(ImmediateUiDispatcher.Instance);
        await using var provider = services.BuildServiceProvider();

        using var viewModel = provider.GetRequiredService<TextExpansionViewModel>();
        await viewModel.InitializeAsync();

        Assert.Equal(":managed", Assert.Single(viewModel.Expansions).Trigger);
        _ = await manager.Received(1).ListAsync(cancellationToken: CancellationToken.None);
    }

    [Fact]
    public void GuiComposition_LeavesClipboardCompositionToExecutableRoot()
    {
        var clipboard = Substitute.For<IClipboardService>();
        var services = new ServiceCollection();
        ComposeGuiServices(services, new PlatformRegistrar(FakeRuntimeContext.Windows(), clipboard));

        using var provider = services.BuildServiceProvider();
        Assert.Same(clipboard, provider.GetRequiredService<IClipboardService>());
        _ = Assert.IsType<ShellCommandRunner>(provider.GetRequiredService<IShellCommandRunner>());
    }

    [Fact]
    public void GuiComposition_OnLinux_DoesNotRegisterClipboardPorts()
    {
        var services = new ServiceCollection();
        ComposeGuiServices(services, new PlatformRegistrar(FakeRuntimeContext.Linux(sessionType: "wayland")));

        Assert.DoesNotContain(services, sd => sd.ServiceType == typeof(IClipboardService));
        Assert.DoesNotContain(services, sd => sd.ServiceType == typeof(IImageClipboardService));
    }

    [Fact]
    public void GuiComposition_OnLinux_ResolvesScreenshotCaptureWithoutImageClipboard()
    {
        var services = new ServiceCollection();
        ComposeGuiServices(services, new PlatformRegistrar(FakeRuntimeContext.Linux(sessionType: "wayland")));

        using var provider = services.BuildServiceProvider();
        _ = Assert.IsType<ScreenshotCaptureService>(provider.GetRequiredService<IScreenshotCaptureService>());
        Assert.Null(provider.GetService<IImageClipboardService>());
    }

    [Fact]
    public void CombinedEntryPoints_ShareApplicationRegistrations()
    {
        var services = new ServiceCollection();
        _ = services.AddCrossMacroCommonRuntimeServices();
        _ = services.AddCrossMacroSharedPostPlatformRuntimeServices(_ => null);
        _ = services.AddCrossMacroGuiRuntimeServices();
        _ = services.AddCliServices();
        _ = CrossMacro.Mcp.DependencyInjection.McpServiceCollectionExtensions.AddCrossMacroMcp(services);

        Type[] sharedContracts =
        [
            typeof(IManageSchedule), typeof(IManageShortcut), typeof(IManageTrigger),
            typeof(IManageProfile), typeof(IManageTextExpansion),
            typeof(CrossMacro.Application.Automation.AutomationTaskMutationGate),
            typeof(CrossMacro.Application.Automation.AutomationTaskAuthorization),
            typeof(CrossMacro.Application.Settings.SettingsChangeCoordinator),
        ];
        Assert.All(sharedContracts, contract => Assert.Single(services, descriptor => descriptor.ServiceType == contract));
    }

    private static void ComposeGuiServices(IServiceCollection services, IPlatformServiceRegistrar registrar)
    {
        registrar.RegisterPlatformServices(services);
        _ = services.AddSingleton(GuiStartupOptions.Default);
        GuiHostBootstrap.AddRuntimeDiagnostics(services);
        GuiHostBootstrap.ConfigureGuiRuntimeServices(services);
        GuiHostBootstrap.AddCommonGuiServices(services);
        _ = services.AddCrossMacroServices();
    }

    private sealed class PlatformRegistrar(IRuntimeContext runtimeContext, IClipboardService? clipboard = null) : IPlatformServiceRegistrar
    {
        public void RegisterPlatformServices(IServiceCollection services)
        {
            _ = services.AddSingleton(runtimeContext);
            _ = services.AddSingleton(Substitute.For<IKeyboardLayoutService>());
            _ = services.AddSingleton<IDisplaySessionService, GenericDisplaySessionService>();
            _ = services.AddSingleton(Substitute.For<IMousePositionProvider>());
            _ = services.AddTransient<Func<IInputSimulator>>(_ => () => Substitute.For<IInputSimulator>());
            _ = services.AddTransient<Func<IInputCapture>>(_ => () => Substitute.For<IInputCapture>());
            _ = services.AddSingleton(Substitute.For<IInputSimulatorPool>());
            if (clipboard is not null)
            {
                _ = services.AddSingleton(clipboard);
            }
        }
    }
}
