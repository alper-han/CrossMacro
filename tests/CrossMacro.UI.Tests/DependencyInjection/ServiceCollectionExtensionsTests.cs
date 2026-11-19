using Avalonia;
using CrossMacro.Application.Profiles;
using CrossMacro.Cli.DependencyInjection;
using CrossMacro.Core.Models.Automation.TextExpansion;
using CrossMacro.Core.Services.Clipboard;
using CrossMacro.Core.Services.Runtime;
using CrossMacro.Core.Services.Updates;
using CrossMacro.Infrastructure.Services.Macros;
using CrossMacro.Infrastructure.Services.ScreenCapture;
using CrossMacro.Infrastructure.Services.Shell;
using CrossMacro.Mcp.DependencyInjection;
using CrossMacro.Platform.Abstractions.Input.Pointer;
using CrossMacro.Platform.Abstractions.Input.Simulation;
using CrossMacro.Platform.Abstractions.ScreenCapture;
using CrossMacro.UI.DependencyInjection;
using CrossMacro.UI.Hosting;
using CrossMacro.UI.Services.ScreenReading;
using Microsoft.Extensions.DependencyInjection;

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
        Assert.Contains(services, sd => sd.ServiceType == typeof(DesktopStartupCoordinator));
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
    public async Task GuiComposition_ResolvesShortcutViewModelWithItsSingletonDependencies()
    {
        var services = new ServiceCollection();
        ComposeGuiServices(services, new PlatformRegistrar(FakeRuntimeContext.Windows()));

        await using var provider = services.BuildServiceProvider();
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
        ComposeGuiServices(services, new PlatformRegistrar(FakeRuntimeContext.Windows()));
        _ = services.AddCliServices();
        _ = services.AddCrossMacroMcp();

        Type[] sharedContracts =
        [
            typeof(IManageRunnableAutomationTasks<ScheduledTask>), typeof(IManageRunnableAutomationTasks<ShortcutTask>), typeof(IManageAutomationTasks<TriggerTask>),
            typeof(IManageProfile), typeof(IManageTextExpansion),
            typeof(CrossMacro.Application.Automation.AutomationTaskMutationGate),
            typeof(CrossMacro.Application.Automation.AutomationTaskAuthorization),
            typeof(CrossMacro.Application.Settings.SettingsChangeCoordinator),
        ];
        Assert.All(sharedContracts, contract => Assert.Single(services, descriptor => descriptor.ServiceType == contract));
    }

    private static void ComposeGuiServices(IServiceCollection services, IDesktopPlatform platform)
    {
        _ = services.AddSingleton(GuiStartupOptions.Default);
        _ = services.AddCrossMacroGuiHostServices(platform);
        _ = services.AddCrossMacroServices();
    }

    private sealed class PlatformRegistrar(IRuntimeContext runtimeContext, IClipboardService? clipboard = null) : IDesktopPlatform
    {
        public AppBuilder ConfigureAppBuilder(AppBuilder builder) => builder;

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
