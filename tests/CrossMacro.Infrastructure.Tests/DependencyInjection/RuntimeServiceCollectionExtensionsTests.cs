using CrossMacro.Infrastructure.DependencyInjection;
using CrossMacro.Platform.Abstractions.Recording.Strategies;
using CrossMacro.Platform.Abstractions.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace CrossMacro.Infrastructure.Tests.DependencyInjection;

public sealed class RuntimeServiceCollectionExtensionsTests : IDisposable
{
    private readonly string _configDirectory = Directory.CreateTempSubdirectory("crossmacro-di-").FullName;

    public void Dispose()
    {
        Directory.Delete(_configDirectory, recursive: true);
    }

    [Fact]
    public void CommonRuntime_PreservesHostTimeProvider()
    {
        var services = new ServiceCollection();
        var clock = new FakeTimeProvider();
        _ = services.AddSingleton<TimeProvider>(clock);
        _ = services.AddSingleton<IRuntimeContext>(FakeRuntimeContext.Linux(sessionType: "x11"));
        _ = services.AddSingleton(new ApplicationPaths(_configDirectory));

        _ = services.AddCrossMacroCommonRuntimeServices();

        using var provider = services.BuildServiceProvider();
        Assert.Same(clock, provider.GetRequiredService<TimeProvider>());
    }

    [Fact]
    public void AddCrossMacroSharedPostPlatformRuntimeServices_ThrowsForNullPoolResolver()
    {
        var services = new ServiceCollection();

        _ = Assert.Throws<ArgumentNullException>(() =>
            services.AddCrossMacroSharedPostPlatformRuntimeServices(null!));
    }

    [Theory]
    [InlineData(false, typeof(ShellCommandRunner))]
    [InlineData(true, typeof(FlatpakSandboxShellCommandRunner))]
    public void AddCrossMacroCommonRuntimeServices_SelectsShellCommandRunnerByRuntimeContext(bool isFlatpak, Type expectedRunner)
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IRuntimeContext>(FakeRuntimeContext.Linux(isFlatpak, sessionType: "x11"));
        _ = services.AddSingleton(new ApplicationPaths(_configDirectory));

        _ = services.AddCrossMacroCommonRuntimeServices();

        using var provider = services.BuildServiceProvider();
        Assert.IsType(expectedRunner, provider.GetRequiredService<IShellCommandRunner>());
    }

    [Fact]
    public async Task SharedRuntimeServices_ComposeIntoAFullyResolvableGraph()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IRuntimeContext>(FakeRuntimeContext.Linux(sessionType: "x11"));
        _ = services.AddSingleton(new ApplicationPaths(_configDirectory));
        AddPlatformStubs(services);
        _ = services.AddCrossMacroCommonRuntimeServices();
        _ = services.AddCrossMacroSharedPostPlatformRuntimeServices(_ => null);

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        var registeredServices = services
            .Select(static descriptor => descriptor.ServiceType)
            .Where(static type => !type.IsGenericTypeDefinition)
            .Distinct()
            .ToArray();
        Assert.NotEmpty(registeredServices);
        foreach (var serviceType in registeredServices)
        {
            Assert.NotNull(provider.GetRequiredService(serviceType));
        }
    }

    private static void AddPlatformStubs(IServiceCollection services)
    {
        _ = services.AddSingleton<Func<IInputSimulator>>(() => Substitute.For<IInputSimulator>());
        _ = services.AddSingleton(Substitute.For<IClipboardService>());
        _ = services.AddSingleton(Substitute.For<ICoordinateStrategyFactory>());
        _ = services.AddSingleton(Substitute.For<IMousePositionProvider>());
        _ = services.AddSingleton(Substitute.For<IKeyboardLayoutService>());
        _ = services.AddSingleton<Func<IInputCapture>>(() => Substitute.For<IInputCapture>());
    }
}
