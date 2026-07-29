
namespace CrossMacro.UI.Tests.DependencyInjection;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCrossMacroCliRuntimeServices_DoesNotRegisterGuiOnlyServices()
    {
        var services = new ServiceCollection();

        _ = services.AddCrossMacroCliRuntimeServices(new NoOpPlatformServiceRegistrar());

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

        _ = services.AddCrossMacroCliRuntimeServices(new NoOpPlatformServiceRegistrar());
        _ = services.AddCliServices();

        using var provider = services.BuildServiceProvider();

        _ = Assert.IsType<ScreenshotCliService>(provider.GetRequiredService<IScreenshotCliService>());
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
        _ = Assert.IsType<MacroFileManager>(provider.GetRequiredService<IMacroFileManager>());
    }

    [Fact]
    public void AddCrossMacroServices_ResolvesTextExpansionViewModelThroughManagedPort()
    {
        var services = new ServiceCollection();
        ComposeGuiServices(services, new PoolAwarePlatformServiceRegistrar());
        _ = services.AddSingleton<IClipboardService>(_ => new DummyClipboardService());
        _ = services.AddSingleton<IImageClipboardService>(_ => new DummyImageClipboardService());
        _ = services.AddSingleton<IEnvironmentInfoProvider>(Substitute.For<IEnvironmentInfoProvider>());

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
        _ = services.AddCrossMacroCliRuntimeServices(new WindowsLikePlatformServiceRegistrar());

        using var provider = services.BuildServiceProvider();
        _ = Assert.IsType<DummyClipboardService>(provider.GetRequiredService<IClipboardService>());
        _ = Assert.IsType<ShellCommandRunner>(provider.GetRequiredService<IShellCommandRunner>());
    }

    [Fact]
    public void AddCrossMacroGuiRuntimeServices_LeavesClipboardCompositionToExecutableRoot()
    {
        var services = new ServiceCollection();
        ComposeGuiServices(services, new WindowsLikePlatformServiceRegistrar());

        using var provider = services.BuildServiceProvider();
        _ = Assert.IsType<DummyClipboardService>(provider.GetRequiredService<IClipboardService>());
        _ = Assert.IsType<ShellCommandRunner>(provider.GetRequiredService<IShellCommandRunner>());
    }

    [Fact]
    public void AddCrossMacroCliRuntimeServices_DoesNotRegisterLinuxClipboardCompatibilityBinding()
    {
        var services = new ServiceCollection();
        _ = services.AddCrossMacroCliRuntimeServices(new LinuxLikePlatformServiceRegistrar());

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

        using var provider = services.BuildServiceProvider();
        _ = Assert.IsType<ScreenshotCaptureService>(provider.GetRequiredService<IScreenshotCaptureService>());
        Assert.Null(provider.GetService<IImageClipboardService>());
    }

    [Fact]
    public void AddCliServices_ResolvesPreflightService_WhenPlatformRegistersFactoryBasedInput()
    {
        var services = new ServiceCollection();
        _ = services.AddCrossMacroCliRuntimeServices(new FactoryInputPlatformServiceRegistrar());
        _ = services.AddCliServices();

        using var provider = services.BuildServiceProvider();
        var preflight = provider.GetRequiredService<ICliPreflightService>();

        Assert.NotNull(preflight);
    }

    [Fact]
    public async Task AddCrossMacroCliRuntimeServices_OneShot_DoesNotInjectPoolIntoMacroPlayer()
    {
        var simulatorPool = new TrackingInputSimulatorPool();
        var services = new ServiceCollection();
        _ = services.AddCrossMacroCliRuntimeServices(
            new PoolAwarePlatformServiceRegistrar(simulatorPool),
            CliRuntimeProfile.OneShot);

        using var provider = services.BuildServiceProvider();
        var player = provider.GetRequiredService<IMacroPlayer>();

        await player.PlayAsync(CreatePlayableMacro(), cancellationToken: CancellationToken.None);

        Assert.Equal(0, simulatorPool.AcquireCount);
        Assert.Equal(0, simulatorPool.ReleaseCount);
    }

    [Fact]
    public async Task AddCrossMacroCliRuntimeServices_Persistent_InjectsPoolIntoMacroPlayer()
    {
        var simulatorPool = new TrackingInputSimulatorPool();
        var services = new ServiceCollection();
        _ = services.AddCrossMacroCliRuntimeServices(
            new PoolAwarePlatformServiceRegistrar(simulatorPool),
            CliRuntimeProfile.Persistent);

        using var provider = services.BuildServiceProvider();
        var player = provider.GetRequiredService<IMacroPlayer>();

        await player.PlayAsync(CreatePlayableMacro(), cancellationToken: CancellationToken.None);

        Assert.Equal(1, simulatorPool.AcquireCount);
        Assert.Equal(1, simulatorPool.ReleaseCount);
        Assert.NotNull(simulatorPool.AcquiredSimulator);
        Assert.Same(simulatorPool.AcquiredSimulator, simulatorPool.ReleasedSimulator);
    }

    private static MacroSequence CreatePlayableMacro()
    {
        return new MacroSequence
        {
            Events = { new MacroEvent { Type = EventType.MouseMove, X = 1, Y = 1 } },
        };
    }

    private static void ComposeGuiServices(IServiceCollection services, IPlatformServiceRegistrar registrar)
    {
        registrar.RegisterPlatformServices(services);
        _ = services.AddSingleton<IRuntimeLogLevelService, RuntimeLogLevelService>();
        _ = services.AddCrossMacroCommonRuntimeServices();
        _ = services.AddCrossMacroSharedPostPlatformRuntimeServices(sp => sp.GetService<IInputSimulatorPool>());
        _ = services.AddSingleton<IUpdateService, GitHubUpdateService>();
        _ = services.AddCrossMacroServices();
    }

    private sealed class NoOpPlatformServiceRegistrar : IPlatformServiceRegistrar
    {

        public void RegisterPlatformServices(IServiceCollection services)
        {
            _ = services.AddSingleton<IRuntimeContext>(_ => new TestRuntimeContext());
        }
    }

    private sealed class WindowsLikePlatformServiceRegistrar : IPlatformServiceRegistrar
    {

        public void RegisterPlatformServices(IServiceCollection services)
        {
            _ = services.AddSingleton<IRuntimeContext>(_ => new TestRuntimeContext());
            _ = services.AddSingleton<IClipboardService>(_ => new DummyClipboardService());
            _ = services.AddSingleton<IImageClipboardService>(_ => new DummyImageClipboardService());
        }
    }

    private sealed class LinuxLikePlatformServiceRegistrar : IPlatformServiceRegistrar
    {

        public void RegisterPlatformServices(IServiceCollection services)
        {
            _ = services.AddSingleton<IRuntimeContext>(_ => new TestRuntimeContext());
        }
    }

    private sealed class FactoryInputPlatformServiceRegistrar : IPlatformServiceRegistrar
    {

        public void RegisterPlatformServices(IServiceCollection services)
        {
            _ = services.AddSingleton<IRuntimeContext>(_ => new TestRuntimeContext());
            _ = services.AddSingleton<IDisplaySessionService, GenericDisplaySessionService>();
            _ = services.AddTransient<Func<IInputSimulator>>(_ => () => new DummyInputSimulator());
            _ = services.AddTransient<Func<IInputCapture>>(_ => () => new DummyInputCapture());
        }
    }

    private sealed class PoolAwarePlatformServiceRegistrar(IInputSimulatorPool? inputSimulatorPool = null) : IPlatformServiceRegistrar
    {
        public void RegisterPlatformServices(IServiceCollection services)
        {
            _ = services.AddSingleton<IRuntimeContext>(_ => new TestRuntimeContext());
            _ = services.AddSingleton<IKeyboardLayoutService>(_ => new DummyKeyboardLayoutService());
            _ = services.AddSingleton<IDisplaySessionService, GenericDisplaySessionService>();
            _ = services.AddSingleton<IMousePositionProvider>(_ => new DummyMousePositionProvider());
            _ = services.AddTransient<Func<IInputSimulator>>(_ => () => new DummyInputSimulator());
            _ = services.AddTransient<Func<IInputCapture>>(_ => () => new DummyInputCapture());
            _ = services.AddSingleton<IInputSimulatorPool>(sp => inputSimulatorPool ?? new CrossMacro.Platform.Linux.Services.InputSimulatorPool(sp.GetRequiredService<Func<IInputSimulator>>()));
        }
    }

    private sealed class TrackingInputSimulatorPool : IInputSimulatorPool
    {
        public int AcquireCount { get; private set; }
        public int ReleaseCount { get; private set; }
        public IInputSimulator? AcquiredSimulator { get; private set; }
        public IInputSimulator? ReleasedSimulator { get; private set; }
        public bool HasWarmDevice => false;
        public Task Completion => Task.CompletedTask;

        public Task WarmUpAsync(int screenWidth = 0, int screenHeight = 0) => Task.CompletedTask;

        public Task<IInputSimulator> AcquireAsync(
            int screenWidth,
            int screenHeight,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Acquire(screenWidth, screenHeight));
        }

        public IInputSimulator Acquire(int screenWidth, int screenHeight)
        {
            AcquireCount++;
            AcquiredSimulator = new DummyInputSimulator();
            return AcquiredSimulator;
        }

        public void Release(IInputSimulator device, int screenWidth = 0, int screenHeight = 0)
        {
            ReleaseCount++;
            ReleasedSimulator = device;
        }

        public void Dispose()
        {
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
        public Task InitializeAsync(int screenWidth = 0, int screenHeight = 0, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Initialize(screenWidth, screenHeight);
            return Task.CompletedTask;
        }
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
        public event EventHandler<CapturedInputEventArgs>? InputReceived { add { } remove { } }
        public event EventHandler<InputCaptureErrorEventArgs>? CaptureError { add { } remove { } }
        public void Configure(bool captureMouse, bool captureKeyboard) { }
        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
        public void StopCapture() { }
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
