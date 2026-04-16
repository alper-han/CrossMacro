namespace CrossMacro.Infrastructure.Tests.DependencyInjection;

using System;
using System.Collections.Generic;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Core.Services.Recording.Processors;
using CrossMacro.Core.Services.Recording.Strategies;
using CrossMacro.Core.Services.TextExpansion;
using CrossMacro.Infrastructure.DependencyInjection;
using CrossMacro.Infrastructure.Services;
using CrossMacro.Infrastructure.Services.TextExpansion;
using Microsoft.Extensions.DependencyInjection;

public class RuntimeServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCrossMacroSharedPostPlatformRuntimeServices_ThrowsForNullPoolResolver()
    {
        var services = new TestServiceCollection();

        Assert.Throws<ArgumentNullException>(() =>
            services.AddCrossMacroSharedPostPlatformRuntimeServices(null!));
    }

    [Fact]
    public void AddCrossMacroCommonRuntimeServices_RegistersExpectedContracts()
    {
        var services = new TestServiceCollection();

        services.AddCrossMacroCommonRuntimeServices();

        AssertImplementationRegistration<IRuntimeContext, RuntimeContext>(services, ServiceLifetime.Singleton);
        AssertImplementationRegistration<IHotkeyConfigurationService, HotkeyConfigurationService>(services, ServiceLifetime.Singleton);
        AssertImplementationRegistration<ISettingsService, SettingsService>(services, ServiceLifetime.Singleton);
        AssertFactoryRegistration<HotkeySettings>(services, ServiceLifetime.Singleton);
        AssertImplementationRegistration<ITimeProvider, SystemTimeProvider>(services, ServiceLifetime.Singleton);
        AssertImplementationRegistration<IMacroFileManager, MacroFileManager>(services, ServiceLifetime.Singleton);
        AssertFactoryRegistration<Func<ICoordinateStrategy, IInputEventProcessor>>(services, ServiceLifetime.Singleton);
        AssertFactoryRegistration<IMacroRecorder>(services, ServiceLifetime.Transient);
    }

    [Fact]
    public void AddCrossMacroSharedPostPlatformRuntimeServices_RegistersExpectedContracts()
    {
        var services = new TestServiceCollection();

        services.AddCrossMacroSharedPostPlatformRuntimeServices(_ => null);

        AssertImplementationRegistration<IKeyCodeMapper, KeyCodeMapper>(services, ServiceLifetime.Singleton);
        AssertImplementationRegistration<IMouseButtonMapper, MouseButtonMapper>(services, ServiceLifetime.Singleton);
        AssertImplementationRegistration<IModifierStateTracker, ModifierStateTracker>(services, ServiceLifetime.Singleton);
        AssertImplementationRegistration<IHotkeyParser, HotkeyParser>(services, ServiceLifetime.Singleton);
        AssertImplementationRegistration<IHotkeyStringBuilder, HotkeyStringBuilder>(services, ServiceLifetime.Singleton);
        AssertImplementationRegistration<IHotkeyMatcher, HotkeyMatcher>(services, ServiceLifetime.Singleton);
        AssertFactoryRegistration<IGlobalHotkeyService>(services, ServiceLifetime.Singleton);

        AssertImplementationRegistration<PlaybackValidator, PlaybackValidator>(services, ServiceLifetime.Transient);
        AssertFactoryRegistration<IMacroPlayer>(services, ServiceLifetime.Transient);
        AssertFactoryRegistration<Func<IMacroPlayer>>(services, ServiceLifetime.Singleton);

        AssertImplementationRegistration<IScheduledTaskRepository, JsonScheduledTaskRepository>(services, ServiceLifetime.Singleton);
        AssertImplementationRegistration<IScheduledTaskExecutor, MacroScheduledTaskExecutor>(services, ServiceLifetime.Singleton);
        AssertImplementationRegistration<ISchedulerService, SchedulerService>(services, ServiceLifetime.Singleton);
        AssertImplementationRegistration<IShortcutService, ShortcutService>(services, ServiceLifetime.Singleton);
        AssertImplementationRegistration<ITextExpansionStorageService, TextExpansionStorageService>(services, ServiceLifetime.Singleton);

        AssertImplementationRegistration<IInputProcessor, InputProcessor>(services, ServiceLifetime.Singleton);
        AssertImplementationRegistration<ITextBufferState, TextBufferState>(services, ServiceLifetime.Singleton);
        AssertImplementationRegistration<ITextExpansionExecutor, TextExpansionExecutor>(services, ServiceLifetime.Singleton);
        AssertImplementationRegistration<ITextExpansionService, TextExpansionService>(services, ServiceLifetime.Singleton);

        AssertImplementationRegistration<IEditorActionConverter, EditorActionConverter>(services, ServiceLifetime.Singleton);
        AssertImplementationRegistration<IEditorActionValidator, EditorActionValidator>(services, ServiceLifetime.Singleton);
        AssertFactoryRegistration<ICoordinateCaptureService>(services, ServiceLifetime.Singleton);
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

    private sealed class TestServiceCollection : List<ServiceDescriptor>, IServiceCollection
    {
    }
}
