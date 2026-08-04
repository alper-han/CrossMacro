namespace CrossMacro.Infrastructure.Tests.DependencyInjection;


public sealed class RuntimeServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCrossMacroSharedPostPlatformRuntimeServices_ThrowsForNullPoolResolver()
    {
        var services = new TestServiceCollection();

        _ = Assert.Throws<ArgumentNullException>(() =>
            services.AddCrossMacroSharedPostPlatformRuntimeServices(null!));
    }

    [Fact]
    public void AddCrossMacroCommonRuntimeServices_RegistersExpectedContracts()
    {
        var services = new TestServiceCollection();

        _ = services.AddCrossMacroCommonRuntimeServices();

        AssertImplementationRegistration<IRuntimeLogLevelService, RuntimeLogLevelService>(services, ServiceLifetime.Singleton);
        AssertFactoryRegistration<IShellCommandRunner>(services, ServiceLifetime.Singleton);
        AssertImplementationRegistration<IHotkeyConfigurationService, HotkeyConfigurationService>(services, ServiceLifetime.Singleton);
        AssertImplementationRegistration<ISettingsService, SettingsService>(services, ServiceLifetime.Singleton);
        AssertFactoryRegistration<HotkeySettings>(services, ServiceLifetime.Singleton);
        _ = services.Should().Contain(descriptor => descriptor.ServiceType == typeof(TimeProvider)
            && descriptor.ImplementationInstance == TimeProvider.System
            && descriptor.Lifetime == ServiceLifetime.Singleton);
        AssertFactoryRegistration<Func<ICoordinateStrategy, IInputEventProcessor>>(services, ServiceLifetime.Singleton);
        AssertFactoryRegistration<IMacroRecorder>(services, ServiceLifetime.Transient);
    }

    [Fact]
    public void AddCrossMacroCommonRuntimeServices_ResolvesShellCommandRunnerOutsideFlatpak()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IRuntimeContext>(new TestRuntimeContext(isFlatpak: false));

        _ = services.AddCrossMacroCommonRuntimeServices();

        var runner = ResolveShellRunnerFromDescriptor(services, new TestRuntimeContext(isFlatpak: false));

        _ = Assert.IsType<ShellCommandRunner>(runner);
    }

    [Fact]
    public void AddCrossMacroCommonRuntimeServices_DisablesShellCommandRunnerInFlatpak()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IRuntimeContext>(new TestRuntimeContext(isFlatpak: true));

        _ = services.AddCrossMacroCommonRuntimeServices();

        var runner = ResolveShellRunnerFromDescriptor(services, new TestRuntimeContext(isFlatpak: true));

        _ = Assert.IsType<FlatpakDisabledShellCommandRunner>(runner);
    }

    [Fact]
    public void AddCrossMacroSharedPostPlatformRuntimeServices_RegistersExpectedContracts()
    {
        var services = new TestServiceCollection();

        _ = services.AddCrossMacroSharedPostPlatformRuntimeServices(_ => null);

        AssertImplementationRegistration<IKeyCodeMapper, KeyCodeMapper>(services, ServiceLifetime.Singleton);
        AssertImplementationRegistration<IMacroFileManager, MacroFileManager>(services, ServiceLifetime.Singleton);
        AssertImplementationRegistration<IMouseButtonMapper, MouseButtonMapper>(services, ServiceLifetime.Singleton);
        AssertImplementationRegistration<IModifierStateTracker, ModifierStateTracker>(services, ServiceLifetime.Singleton);
        AssertImplementationRegistration<IHotkeyParser, HotkeyParser>(services, ServiceLifetime.Singleton);
        AssertImplementationRegistration<IHotkeyStringBuilder, HotkeyStringBuilder>(services, ServiceLifetime.Singleton);
        AssertImplementationRegistration<IHotkeyMatcher, HotkeyMatcher>(services, ServiceLifetime.Singleton);
        AssertFactoryRegistration<IGlobalHotkeyService>(services, ServiceLifetime.Singleton);
        AssertFactoryRegistration<IScreenshotCaptureService>(services, ServiceLifetime.Singleton);
        AssertFactoryRegistration<CrossMacro.Platform.Abstractions.IScreenReadingWarmupService>(services, ServiceLifetime.Singleton);

        AssertImplementationRegistration<IPlaybackValidator, PlaybackValidator>(services, ServiceLifetime.Transient);
        AssertFactoryRegistration<IMacroPlayer>(services, ServiceLifetime.Transient);
        AssertFactoryRegistration<Func<IMacroPlayer>>(services, ServiceLifetime.Singleton);

        AssertImplementationRegistration<IScheduledTaskRepository, JsonScheduledTaskRepository>(services, ServiceLifetime.Singleton);
        AssertImplementationRegistration<IScheduledTaskExecutor, MacroScheduledTaskExecutor>(services, ServiceLifetime.Singleton);
        AssertImplementationRegistration<ISchedulerService, SchedulerService>(services, ServiceLifetime.Singleton);
        AssertImplementationRegistration<IShortcutService, ShortcutService>(services, ServiceLifetime.Singleton);
        AssertImplementationRegistration<ProfileSwitchRequestBridge, ProfileSwitchRequestBridge>(services, ServiceLifetime.Singleton);
        AssertFactoryRegistration<IProfileSwitchRequests>(services, ServiceLifetime.Singleton);
        AssertFactoryRegistration<ITriggerService>(services, ServiceLifetime.Singleton);
        AssertFactoryRegistration<IProfileCatalog>(services, ServiceLifetime.Singleton);
        AssertFactoryRegistration<IProfileManager>(services, ServiceLifetime.Singleton);
        AssertImplementationRegistration<ITextExpansionStorageService, TextExpansionStorageService>(services, ServiceLifetime.Singleton);

        AssertImplementationRegistration<IInputProcessor, InputProcessor>(services, ServiceLifetime.Singleton);
        AssertImplementationRegistration<ITextBufferState, TextBufferState>(services, ServiceLifetime.Singleton);
        AssertImplementationRegistration<ITextExpansionExecutor, TextExpansionExecutor>(services, ServiceLifetime.Singleton);
        AssertImplementationRegistration<ITextExpansionService, TextExpansionService>(services, ServiceLifetime.Singleton);

        AssertImplementationRegistration<IEditorActionConverter, EditorActionConverter>(services, ServiceLifetime.Singleton);
        AssertImplementationRegistration<IEditorActionValidator, EditorActionValidator>(services, ServiceLifetime.Singleton);
        AssertFactoryRegistration<ICoordinateCaptureService>(services, ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddCrossMacroSharedPostPlatformRuntimeServices_ResolvesScreenshotCaptureWithoutImageClipboardService()
    {
        var services = new ServiceCollection();

        _ = services.AddCrossMacroSharedPostPlatformRuntimeServices(_ => null);

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IScreenshotCaptureService));
        Assert.NotNull(descriptor.ImplementationFactory);

        var screenshotCaptureService = descriptor.ImplementationFactory(new TestServiceProvider(new TestRuntimeContext(isFlatpak: false)));

        _ = Assert.IsType<ScreenshotCaptureService>(screenshotCaptureService);
    }

    private static void AssertImplementationRegistration<TService, TImplementation>(
        IServiceCollection services,
        ServiceLifetime lifetime)
    {
        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(TService));
        Assert.Equal(lifetime, descriptor.Lifetime);
        Assert.Equal(typeof(TImplementation), descriptor.ImplementationType);
    }

    private static void AssertFactoryRegistration<TService>(
        IServiceCollection services,
        ServiceLifetime lifetime)
    {
        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(TService));
        Assert.Equal(lifetime, descriptor.Lifetime);
        Assert.Null(descriptor.ImplementationType);
        Assert.NotNull(descriptor.ImplementationFactory);
    }

    private static IShellCommandRunner ResolveShellRunnerFromDescriptor(
        IServiceCollection services,
        IRuntimeContext runtimeContext)
    {
        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IShellCommandRunner));
        Assert.NotNull(descriptor.ImplementationFactory);
        return Assert.IsAssignableFrom<IShellCommandRunner>(
            descriptor.ImplementationFactory(new TestServiceProvider(runtimeContext)));
    }

    private sealed class TestServiceCollection : List<ServiceDescriptor>, IServiceCollection;

    private sealed class TestRuntimeContext(bool isFlatpak) : IRuntimeContext
    {
        public bool IsLinux => true;
        public bool IsWindows => false;
        public bool IsMacOS => false;
        public bool IsFlatpak { get; } = isFlatpak;
        public string? SessionType => "wayland";
    }

    private sealed class TestServiceProvider(IRuntimeContext runtimeContext) : IServiceProvider
    {
        private readonly IRuntimeContext _runtimeContext = runtimeContext;

        public object? GetService(Type serviceType) =>
            serviceType == typeof(IRuntimeContext)
                ? _runtimeContext
                : serviceType == typeof(IImageAssetCodec)
                    ? new ImageAssetCodec()
                    : null;
    }
}
