
namespace CrossMacro.Cli.Tests;

public sealed class CliPreflightServiceTests
{
    [Fact]
    public async Task CheckAsync_WhenDisplaySessionUnsupported_ReturnsEnvironmentError()
    {
        var displaySession = new FakeDisplaySessionService(supported: false, reason: "unsupported");
        var inputSimulator = Substitute.For<IInputSimulator>();
        var inputCapture = Substitute.For<IInputCapture>();

        var service = new CliPreflightService(displaySession, inputSimulator, inputCapture, isLinux: () => false);
        var result = await service.CheckAsync(CliPreflightTarget.Play, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CliExitCode.EnvironmentError, result.ExitCode);
        Assert.Contains("display session", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckAsync_WhenPlayAndSimulatorUnsupported_ReturnsEnvironmentError()
    {
        var displaySession = new FakeDisplaySessionService(supported: true, reason: string.Empty);
        var inputSimulator = Substitute.For<IInputSimulator>();
        var inputCapture = Substitute.For<IInputCapture>();
        _ = inputSimulator.IsSupported.Returns(returnThis: false);
        _ = inputSimulator.ProviderName.Returns("MockSimulator");

        var service = new CliPreflightService(displaySession, inputSimulator, inputCapture, isLinux: () => false);
        var result = await service.CheckAsync(CliPreflightTarget.Play, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CliExitCode.EnvironmentError, result.ExitCode);
        Assert.Contains("simulation backend", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckAsync_WhenRecordAndCaptureUnsupported_ReturnsEnvironmentError()
    {
        var displaySession = new FakeDisplaySessionService(supported: true, reason: string.Empty);
        var inputSimulator = Substitute.For<IInputSimulator>();
        var inputCapture = Substitute.For<IInputCapture>();
        _ = inputCapture.IsSupported.Returns(returnThis: false);
        _ = inputCapture.ProviderName.Returns("MockCapture");

        var service = new CliPreflightService(displaySession, inputSimulator, inputCapture, isLinux: () => false);
        var result = await service.CheckAsync(CliPreflightTarget.Record, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CliExitCode.EnvironmentError, result.ExitCode);
        Assert.Contains("capture backend", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckAsync_WhenHeadlessAndDisplaySupported_ReturnsSuccess()
    {
        var displaySession = new FakeDisplaySessionService(supported: true, reason: string.Empty);
        var inputSimulator = Substitute.For<IInputSimulator>();
        var inputCapture = Substitute.For<IInputCapture>();

        var service = new CliPreflightService(displaySession, inputSimulator, inputCapture, isLinux: () => false);
        var result = await service.CheckAsync(CliPreflightTarget.Headless, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(CliExitCode.Success, result.ExitCode);
    }

    [Fact]
    public async Task CheckAsync_WhenHeadlessLinuxDisplayVariablesAreMissing_ReturnsEnvironmentError()
    {
        var displaySession = new FakeDisplaySessionService(supported: true, reason: string.Empty);
        var inputSimulator = Substitute.For<IInputSimulator>();
        var inputCapture = Substitute.For<IInputCapture>();

        var service = new CliPreflightService(
            displaySession,
            inputSimulator,
            inputCapture,
            isLinux: () => true,
            getEnvironmentVariable: _ => null);

        var result = await service.CheckAsync(CliPreflightTarget.Headless, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CliExitCode.EnvironmentError, result.ExitCode);
        Assert.Contains("no active Linux display session", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckAsync_WhenDisplayVariablesMissing_DelegatesToDisplayAndInputServices()
    {
        var displaySession = new FakeDisplaySessionService(supported: true, reason: string.Empty);
        var inputSimulator = Substitute.For<IInputSimulator>();
        var inputCapture = Substitute.For<IInputCapture>();

        var service = new CliPreflightService(
            displaySession,
            inputSimulator,
            inputCapture,
            isLinux: () => true,
            getEnvironmentVariable: _ => null);

        var result = await service.CheckAsync(CliPreflightTarget.Run, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CliExitCode.EnvironmentError, result.ExitCode);
        Assert.Contains("input simulation backend is unavailable", result.Message, StringComparison.OrdinalIgnoreCase);
    }
}
