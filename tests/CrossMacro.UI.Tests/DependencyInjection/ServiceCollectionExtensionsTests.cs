using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Cli.DependencyInjection;
using CrossMacro.Cli.Services;
using CrossMacro.Cli;
using CrossMacro.UI.DependencyInjection;
using CrossMacro.UI.Services;
using Microsoft.Extensions.DependencyInjection;

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
        Assert.DoesNotContain(services, sd => sd.ServiceType == typeof(IUpdateService));
        Assert.DoesNotContain(services, sd => sd.ServiceType == typeof(IExternalUrlOpener));
        Assert.DoesNotContain(services, sd => sd.ServiceType == typeof(IThemeService));
    }

    [Fact]
    public void AddCrossMacroGuiRuntimeServices_RegistersGuiOnlyServices()
    {
        var services = new ServiceCollection();

        services.AddCrossMacroGuiRuntimeServices(new NoOpPlatformServiceRegistrar());

        Assert.Contains(services, sd => sd.ServiceType == typeof(ITrayIconService));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IDialogService));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IUpdateService));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IExternalUrlOpener));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IThemeService));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IDesktopStartupCoordinator));
    }

    [Fact]
    public void AddCrossMacroCliRuntimeServices_UsesNonAvaloniaClipboardBinding()
    {
        var services = new ServiceCollection();
        services.AddCrossMacroCliRuntimeServices(new NoOpPlatformServiceRegistrar());

        using var provider = services.BuildServiceProvider();
        var clipboard = provider.GetRequiredService<IClipboardService>();

        Assert.NotNull(clipboard);
        Assert.IsNotType<AvaloniaClipboardService>(clipboard);
        Assert.IsNotType<CompositeClipboardService>(clipboard);

        if (OperatingSystem.IsLinux())
        {
            Assert.IsType<LinuxShellClipboardService>(clipboard);
        }
        else
        {
            Assert.False(clipboard.IsSupported);
        }
    }

    [Fact]
    public void AddCrossMacroGuiRuntimeServices_UsesGuiClipboardBinding()
    {
        var services = new ServiceCollection();
        services.AddCrossMacroGuiRuntimeServices(new NoOpPlatformServiceRegistrar());

        using var provider = services.BuildServiceProvider();
        var clipboard = provider.GetRequiredService<IClipboardService>();

        Assert.NotNull(clipboard);
        if (OperatingSystem.IsLinux())
        {
            Assert.IsType<CompositeClipboardService>(clipboard);
        }
        else
        {
            Assert.IsType<AvaloniaClipboardService>(clipboard);
        }
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

    private sealed class NoOpPlatformServiceRegistrar : IPlatformServiceRegistrar
    {
        public void RegisterPlatformServices(IServiceCollection services)
        {
        }
    }

    private sealed class FactoryInputPlatformServiceRegistrar : IPlatformServiceRegistrar
    {
        public void RegisterPlatformServices(IServiceCollection services)
        {
            services.AddSingleton<IDisplaySessionService, GenericDisplaySessionService>();
            services.AddTransient<Func<IInputSimulator>>(_ => () => new DummyInputSimulator());
            services.AddTransient<Func<IInputCapture>>(_ => () => new DummyInputCapture());
        }
    }

    private sealed class PoolAwarePlatformServiceRegistrar : IPlatformServiceRegistrar
    {
        public void RegisterPlatformServices(IServiceCollection services)
        {
            services.AddSingleton<IDisplaySessionService, GenericDisplaySessionService>();
            services.AddSingleton<IMousePositionProvider, DummyMousePositionProvider>();
            services.AddTransient<Func<IInputSimulator>>(_ => () => new DummyInputSimulator());
            services.AddTransient<Func<IInputCapture>>(_ => () => new DummyInputCapture());
            services.AddSingleton(sp => new InputSimulatorPool(sp.GetRequiredService<Func<IInputSimulator>>()));
        }
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
}
