namespace CrossMacro.Cli.Tests;

public sealed class CliRuntimeCompositionTests
{
    [Fact]
    public void AddCrossMacroCliRuntimeServices_WhenPlatformSuppliesNoRuntimeContext_UsesTheEnvironmentRuntimeContext()
    {
        var services = new ServiceCollection();

        _ = services.AddCrossMacroCliRuntimeServices(new NoPlatformRegistrar(), CliRuntimeProfile.OneShot);

        using var provider = services.BuildServiceProvider();
        var runtimeContext = Assert.IsType<RuntimeContext>(provider.GetRequiredService<IRuntimeContext>());
        Assert.Same(runtimeContext, provider.GetRequiredService<IDisplayEnvironmentDiagnostic>());
    }

    [Theory]
    [InlineData(CliRuntimeProfile.OneShot)]
    [InlineData(CliRuntimeProfile.Persistent)]
    public void AddCrossMacroCliRuntimeServices_WhenPlatformSuppliesRuntimeContext_KeepsThePlatformContext(CliRuntimeProfile profile)
    {
        var services = new ServiceCollection();

        _ = services.AddCrossMacroCliRuntimeServices(new TestPlatformServiceRegistrar(), profile);

        using var provider = services.BuildServiceProvider();
        Assert.IsNotType<RuntimeContext>(provider.GetRequiredService<IRuntimeContext>());
        Assert.Same(provider.GetRequiredService<IRuntimeContext>(), provider.GetRequiredService<IDisplayEnvironmentDiagnostic>());
    }

    [Fact]
    public void AddCrossMacroCliRuntimeServices_WhenRegistrarIsNull_Throws()
    {
        var services = new ServiceCollection();

        _ = Assert.Throws<ArgumentNullException>(() =>
            services.AddCrossMacroCliRuntimeServices(null!, CliRuntimeProfile.OneShot));
    }

    private sealed class NoPlatformRegistrar : IPlatformServiceRegistrar
    {
        public void RegisterPlatformServices(IServiceCollection services)
        {
            // Resolving the runtime context must not depend on platform registrations.
        }
    }
}
