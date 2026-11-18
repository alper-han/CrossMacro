using System.Text.Json.Nodes;
using CrossMacro.Cli.DependencyInjection;
using CrossMacro.Core.Services.Clipboard;
using CrossMacro.Platform.Abstractions.Input.Keyboard;
using CrossMacro.UI.Hosting;

namespace CrossMacro.Cli.Tests;

public sealed class CliHostTests : IDisposable
{
    private readonly TempDirectory _configDirectory = new(create: false);

    public void Dispose() => _configDirectory.Dispose();

    private ApplicationPaths Paths => new(_configDirectory.Path);

    [Fact]
    public async Task RunAsync_WhenSettingsGetWithJson_ReturnsSuccess()
    {
        using var console = new ConsoleCapture();
        var host = new CliHost(ConfigureTestRuntime(new TestPlatformServiceRegistrar()));

        var exitCode = await host.RunAsync(new SettingsGetOptions(JsonOutput: true));

        Assert.Equal((int)ExecutionOutcomeCode.Success, exitCode);
        Assert.Equal("ok", (string?)JsonNode.Parse(console.StandardOutput)!["status"]);
        Assert.Equal(string.Empty, console.StandardError);
    }

    [Fact]
    public async Task RunAsync_WhenSettingsGetWithJson_AndNoPlatformRegistrations_ReturnsSuccess()
    {
        using var console = new ConsoleCapture();
        var host = new CliHost(ConfigureTestRuntime(new EmptyPlatformServiceRegistrar()));

        var exitCode = await host.RunAsync(new SettingsGetOptions(JsonOutput: true));

        Assert.Equal((int)ExecutionOutcomeCode.Success, exitCode);
        Assert.Equal("ok", (string?)JsonNode.Parse(console.StandardOutput)!["status"]);
        Assert.Equal(string.Empty, console.StandardError);
    }

    [Fact]
    public async Task RunAsync_WhenSettingsGet_InitializesProfilesOnlyUnderTheSuppliedPaths()
    {
        using var console = new ConsoleCapture();
        var host = new CliHost(ConfigureTestRuntime(new TestPlatformServiceRegistrar()));

        _ = await host.RunAsync(new SettingsGetOptions(JsonOutput: true));

        Assert.True(Directory.Exists(Paths.GetProfilesDirectory()));
    }

    [Fact]
    public async Task RunAsync_WhenRuntimeExceptionOccurs_ReturnsRuntimeErrorAsJson()
    {
        using var console = new ConsoleCapture();
        var host = new CliHost((services, profile) =>
        {
            _ = services.AddSingleton(Paths);
            _ = services.AddCrossMacroCliRuntimeServices(new ThrowingPlatformServiceRegistrar(new InvalidOperationException("simulated registration failure")), profile);
        });

        var exitCode = await host.RunAsync(new DoctorOptions(JsonOutput: true));

        Assert.Equal((int)ExecutionOutcomeCode.RuntimeError, exitCode);
        var envelope = JsonNode.Parse(console.StandardOutput)!;
        Assert.Equal("error", (string?)envelope["status"]);
        Assert.Equal((int)ExecutionOutcomeCode.RuntimeError, (int?)envelope["code"]);
        Assert.Equal(string.Empty, console.StandardError);
    }

    [Fact]
    public async Task RunAsync_WhenCancelledDuringBootstrap_ReturnsCancelledAsJson()
    {
        using var console = new ConsoleCapture();
        var host = new CliHost((services, profile) =>
        {
            _ = services.AddSingleton(Paths);
            _ = services.AddCrossMacroCliRuntimeServices(new ThrowingPlatformServiceRegistrar(new OperationCanceledException("simulated cancellation")), profile);
        });

        var exitCode = await host.RunAsync(new DoctorOptions(JsonOutput: true));

        Assert.Equal((int)ExecutionOutcomeCode.Cancelled, exitCode);
        var envelope = JsonNode.Parse(console.StandardOutput)!;
        Assert.Equal("error", (string?)envelope["status"]);
        Assert.Equal((int)ExecutionOutcomeCode.Cancelled, (int?)envelope["code"]);
        Assert.Equal(string.Empty, console.StandardError);
    }

    [Fact]
    public void AddCliServices_RegistersCommandHandlerResolverAndExecutor()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(Paths);
        _ = services.AddCrossMacroCliRuntimeServices(new TestPlatformServiceRegistrar(), CliRuntimeProfile.OneShot);
        _ = services.AddCliServices();

        using var provider = services.BuildServiceProvider();

        _ = Assert.IsType<CliCommandHandlerResolver>(provider.GetRequiredService<ICliCommandHandlerResolver>());
        Assert.NotNull(provider.GetRequiredService<CliCommandExecutor>());
    }

    private Action<IServiceCollection, CliRuntimeProfile> ConfigureTestRuntime(IPlatformServiceRegistrar registrar)
    {
        return (services, profile) =>
        {
            _ = services.AddSingleton(Substitute.For<IClipboardService>());
            _ = services.AddSingleton(Substitute.For<IImageClipboardService>());
            _ = services.AddSingleton(Substitute.For<IKeyboardLayoutService>());
            _ = services.AddSingleton(Paths);
            _ = services.AddCrossMacroCliRuntimeServices(registrar, profile);
        };
    }

    private sealed class ThrowingPlatformServiceRegistrar(Exception exception) : IPlatformServiceRegistrar
    {
        public void RegisterPlatformServices(IServiceCollection services) => throw exception;
    }

    private sealed class EmptyPlatformServiceRegistrar : IPlatformServiceRegistrar
    {
        // Settings commands must not force resolution of platform services.
        public void RegisterPlatformServices(IServiceCollection services)
        {
        }
    }
}
