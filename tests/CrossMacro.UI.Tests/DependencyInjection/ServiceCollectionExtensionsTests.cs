using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using CrossMacro.Application.Automation;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Infrastructure.Logging;
using CrossMacro.Infrastructure.DependencyInjection;
using CrossMacro.Infrastructure.Services.ScreenCapture;
using CrossMacro.Infrastructure.Services.Recording.Strategies;
using CrossMacro.Platform.Abstractions;
using CrossMacro.Cli.DependencyInjection;
using CrossMacro.Cli.Services;
using CrossMacro.Cli;
using CrossMacro.UI.DependencyInjection;
using CrossMacro.UI.Services;
using CrossMacro.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace CrossMacro.UI.Tests.DependencyInjection;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCrossMacroCliRuntimeServices_DoesNotRegisterGuiOnlyServices()
    {
        var services = new ServiceCollection();

        services.AddCrossMacroCliRuntimeServices(new NoOpPlatformServiceRegistrar());

        Assert.DoesNotContain(services, sd => sd.ServiceType == typeof(ITrayIconService));
        Assert.DoesNotContain(services, sd => sd.ServiceType == typeof(IDialogService));
        Assert.DoesNotContain(services, sd => sd.ServiceType == typeof(IPortalScreenReadingGuidanceService));
        Assert.DoesNotContain(services, sd => sd.ServiceType == typeof(IUpdateService));
        Assert.DoesNotContain(services, sd => sd.ServiceType == typeof(IExternalUrlOpener));
        Assert.DoesNotContain(services, sd => sd.ServiceType == typeof(IThemeService));
    }

    [Fact]
    public void AddCrossMacroCliRuntimeServices_ResolvesScreenshotCliService()
    {
        var services = new ServiceCollection();

        services.AddCrossMacroCliRuntimeServices(new NoOpPlatformServiceRegistrar());
        services.AddCliServices();

        using var provider = services.BuildServiceProvider();

        Assert.IsType<ScreenshotCliService>(provider.GetRequiredService<IScreenshotCliService>());
    }

    [Fact]
    public void AddCrossMacroGuiRuntimeServices_RegistersGuiOnlyServices()
    {
        var services = new ServiceCollection();

        ComposeGuiServices(services, new NoOpPlatformServiceRegistrar());

        Assert.Contains(services, sd => sd.ServiceType == typeof(ITrayIconService));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IDialogService));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IPortalScreenReadingGuidanceService));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IUpdateService));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IExternalUrlOpener));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IThemeService));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IDesktopStartupCoordinator));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IRuntimeLogLevelService));
    }

    [Fact]
    public void AddCrossMacroGuiRuntimeServices_RegistersImageAssetPorts()
    {
        var services = new ServiceCollection();

        ComposeGuiServices(services, new NoOpPlatformServiceRegistrar());

        Assert.Contains(services, sd => sd.ServiceType == typeof(IImageAssetCodec));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IImageAssetPreviewDecoder));
    }

    [Fact]
    public void AddCrossMacroServices_ResolvesShortcutViewModelWithHotkeyDependency()
    {
        var services = new ServiceCollection();
        ComposeGuiServices(services, new PoolAwarePlatformServiceRegistrar());

        using var provider = services.BuildServiceProvider();
        var viewModel = provider.GetRequiredService<ShortcutViewModel>();

        Assert.Same(provider.GetRequiredService<IGlobalHotkeyService>(), viewModel.GlobalHotkeyService);
        Assert.Same(provider.GetRequiredService<ILocalizationService>(), viewModel.LocalizationService);
        Assert.IsType<MacroFileManager>(provider.GetRequiredService<IMacroFileManager>());
    }

    [Fact]
    public void AddCrossMacroServices_ResolvesTextExpansionViewModelThroughManagedPort()
    {
        var services = new ServiceCollection();
        ComposeGuiServices(services, new PoolAwarePlatformServiceRegistrar());
        services.AddSingleton<IClipboardService, DummyClipboardService>();
        services.AddSingleton<IImageClipboardService, DummyImageClipboardService>();
        services.AddSingleton<IEnvironmentInfoProvider>(Substitute.For<IEnvironmentInfoProvider>());

        using var provider = services.BuildServiceProvider();
        var viewModel = provider.GetRequiredService<TextExpansionViewModel>();
        var managedPort = typeof(TextExpansionViewModel)
            .GetField("_manageTextExpansion", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(managedPort);
        Assert.Same(provider.GetRequiredService<IManageTextExpansion>(), managedPort!.GetValue(viewModel));
    }

    [Fact]
    public void AddCrossMacroCliRuntimeServices_LeavesClipboardCompositionToExecutableRoot()
    {
        var services = new ServiceCollection();
        services.AddCrossMacroCliRuntimeServices(new WindowsLikePlatformServiceRegistrar());

        using var provider = services.BuildServiceProvider();
        Assert.IsType<DummyClipboardService>(provider.GetRequiredService<IClipboardService>());
        Assert.IsType<ShellCommandRunner>(provider.GetRequiredService<IShellCommandRunner>());
    }

    [Fact]
    public void AddCrossMacroGuiRuntimeServices_LeavesClipboardCompositionToExecutableRoot()
    {
        var services = new ServiceCollection();
        ComposeGuiServices(services, new WindowsLikePlatformServiceRegistrar());

        using var provider = services.BuildServiceProvider();
        Assert.IsType<DummyClipboardService>(provider.GetRequiredService<IClipboardService>());
        Assert.IsType<ShellCommandRunner>(provider.GetRequiredService<IShellCommandRunner>());
    }

    [Fact]
    public void AddCrossMacroCliRuntimeServices_DoesNotRegisterLinuxClipboardCompatibilityBinding()
    {
        var services = new ServiceCollection();
        services.AddCrossMacroCliRuntimeServices(new LinuxLikePlatformServiceRegistrar());

        Assert.DoesNotContain(services, sd => sd.ServiceType == typeof(IClipboardService));
    }

    [Fact]
    public void AddCrossMacroGuiRuntimeServices_DoesNotRegisterCompositeClipboardCompatibilityBinding()
    {
        var services = new ServiceCollection();
        ComposeGuiServices(services, new LinuxLikePlatformServiceRegistrar());

        Assert.DoesNotContain(services, sd => sd.ServiceType == typeof(IClipboardService));
    }

    [Fact]
    public void AddCrossMacroGuiRuntimeServices_DoesNotRegisterLinuxImageClipboardCompatibilityBinding()
    {
        var services = new ServiceCollection();
        ComposeGuiServices(services, new LinuxLikePlatformServiceRegistrar());

        Assert.DoesNotContain(services, sd => sd.ServiceType == typeof(IImageClipboardService));
    }

    [Fact]
    public void AddCrossMacroGuiRuntimeServices_LeavesScreenshotClipboardCompositionToExecutableRoot()
    {
        var services = new ServiceCollection();
        ComposeGuiServices(services, new LinuxLikePlatformServiceRegistrar());

        Assert.DoesNotContain(services, sd => sd.ServiceType == typeof(IImageClipboardService));
    }

    [Fact]
    public void AddCliServices_ResolvesPreflightService_WhenPlatformRegistersFactoryBasedInput()
    {
        var services = new ServiceCollection();
        services.AddCrossMacroCliRuntimeServices(new FactoryInputPlatformServiceRegistrar());
        services.AddCliServices();

        using var provider = services.BuildServiceProvider();
        var preflight = provider.GetRequiredService<ICliPreflightService>();

        Assert.NotNull(preflight);
    }

    [Fact]
    public void AddCrossMacroCliRuntimeServices_OneShot_DoesNotInjectPoolIntoMacroPlayer()
    {
        var services = new ServiceCollection();
        services.AddCrossMacroCliRuntimeServices(
            new PoolAwarePlatformServiceRegistrar(),
            CliRuntimeProfile.OneShot);

        using var provider = services.BuildServiceProvider();
        var player = Assert.IsType<MacroPlayer>(provider.GetRequiredService<IMacroPlayer>());
        var poolField = typeof(MacroPlayer).GetField("_simulatorPool", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(poolField);
        Assert.Null(poolField.GetValue(player));
    }

    [Fact]
    public void AddCrossMacroCliRuntimeServices_Persistent_InjectsPoolIntoMacroPlayer()
    {
        var services = new ServiceCollection();
        services.AddCrossMacroCliRuntimeServices(
            new PoolAwarePlatformServiceRegistrar(),
            CliRuntimeProfile.Persistent);

        using var provider = services.BuildServiceProvider();
        var player = Assert.IsType<MacroPlayer>(provider.GetRequiredService<IMacroPlayer>());
        var poolField = typeof(MacroPlayer).GetField("_simulatorPool", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(poolField);
        Assert.NotNull(poolField.GetValue(player));
    }

    private static void ComposeGuiServices(IServiceCollection services, IPlatformServiceRegistrar registrar)
    {
        registrar.RegisterPlatformServices(services);
        services.AddSingleton<IRuntimeLogLevelService, RuntimeLogLevelService>();
        services.AddCrossMacroCommonRuntimeServices();
        services.AddCrossMacroSharedPostPlatformRuntimeServices(sp => sp.GetService<IInputSimulatorPool>());
        services.AddSingleton<IUpdateService, GitHubUpdateService>();
        services.AddCrossMacroServices();
    }

    private sealed class NoOpPlatformServiceRegistrar : IPlatformServiceRegistrar
    {

        public void RegisterPlatformServices(IServiceCollection services)
        {
            services.AddSingleton<IRuntimeContext, TestRuntimeContext>();
        }
    }

    private sealed class WindowsLikePlatformServiceRegistrar : IPlatformServiceRegistrar
    {

        public void RegisterPlatformServices(IServiceCollection services)
        {
            services.AddSingleton<IRuntimeContext, TestRuntimeContext>();
            services.AddSingleton<IClipboardService, DummyClipboardService>();
            services.AddSingleton<IImageClipboardService, DummyImageClipboardService>();
        }
    }

    private sealed class LinuxLikePlatformServiceRegistrar : IPlatformServiceRegistrar
    {

        public void RegisterPlatformServices(IServiceCollection services)
        {
            services.AddSingleton<IRuntimeContext, TestRuntimeContext>();
        }
    }

    private sealed class FactoryInputPlatformServiceRegistrar : IPlatformServiceRegistrar
    {

        public void RegisterPlatformServices(IServiceCollection services)
        {
            services.AddSingleton<IRuntimeContext, TestRuntimeContext>();
            services.AddSingleton<IDisplaySessionService, GenericDisplaySessionService>();
            services.AddTransient<Func<IInputSimulator>>(_ => () => new DummyInputSimulator());
            services.AddTransient<Func<IInputCapture>>(_ => () => new DummyInputCapture());
        }
    }

    private sealed class PoolAwarePlatformServiceRegistrar : IPlatformServiceRegistrar
    {

        public void RegisterPlatformServices(IServiceCollection services)
        {
            services.AddSingleton<IRuntimeContext, TestRuntimeContext>();
            services.AddSingleton<IKeyboardLayoutService, DummyKeyboardLayoutService>();
            services.AddSingleton<IDisplaySessionService, GenericDisplaySessionService>();
            services.AddSingleton<IMousePositionProvider, DummyMousePositionProvider>();
            services.AddTransient<Func<IInputSimulator>>(_ => () => new DummyInputSimulator());
            services.AddTransient<Func<IInputCapture>>(_ => () => new DummyInputCapture());
            services.AddSingleton<IInputSimulatorPool>(sp => new InputSimulatorPool(sp.GetRequiredService<Func<IInputSimulator>>()));
        }
    }

    private sealed class TestRuntimeContext : IRuntimeContext
    {
        public bool IsLinux => false;
        public bool IsWindows => true;
        public bool IsMacOS => false;
        public bool IsFlatpak => false;
        public string? SessionType => null;
    }

    private sealed class DummyInputSimulator : IInputSimulator
    {
        public string ProviderName => "dummy-sim";
        public bool IsSupported => true;
        public void Initialize(int screenWidth = 0, int screenHeight = 0) { }
        public void MoveAbsolute(int x, int y) { }
        public void MoveRelative(int dx, int dy) { }
        public void MouseButton(int button, bool pressed) { }
        public void Scroll(int delta, bool isHorizontal = false) { }
        public void KeyPress(int keyCode, bool pressed) { }
        public void Sync() { }
        public void Dispose() { }
    }

    private sealed class DummyKeyboardLayoutService : IKeyboardLayoutService
    {
        public string GetKeyName(int keyCode) => keyCode.ToString(System.Globalization.CultureInfo.InvariantCulture);

        public int GetKeyCode(string keyName) => int.TryParse(
            keyName,
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out var keyCode)
                ? keyCode
                : 0;

        public char? GetCharFromKeyCode(
            int keyCode,
            bool leftShift,
            bool rightShift,
            bool rightAlt,
            bool leftAlt,
            bool leftCtrl,
            bool capsLock) => null;

        public (int KeyCode, bool Shift, bool AltGr)? GetInputForChar(char c) => null;
    }

    private sealed class DummyInputCapture : IInputCapture
    {
        public string ProviderName => "dummy-cap";
        public bool IsSupported => true;
#pragma warning disable CS0067
        public event EventHandler<InputCaptureEventArgs>? InputReceived;
        public event EventHandler<string>? Error;
#pragma warning restore CS0067
        public void Configure(bool captureMouse, bool captureKeyboard) { }
        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
        public void Stop() { }
        public void Dispose() { }
    }

    private sealed class DummyMousePositionProvider : IMousePositionProvider
    {
        public string ProviderName => "dummy-pos";
        public bool IsSupported => true;

        public Task<(int X, int Y)?> GetAbsolutePositionAsync() =>
            Task.FromResult<(int X, int Y)?>(null);

        public Task<(int Width, int Height)?> GetScreenResolutionAsync() =>
            Task.FromResult<(int Width, int Height)?>(null);

        public void Dispose()
        {
        }
    }

    private sealed class DummyClipboardService : IClipboardService
    {
        public bool IsSupported => false;

        public Task SetTextAsync(string text, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<string?> GetTextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }

    private sealed class DummyImageClipboardService : IImageClipboardService
    {
        public bool IsSupported => false;

        public Task SetPngAsync(ReadOnlyMemory<byte> pngBytes, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
