
namespace CrossMacro.Commands.Tests;

public sealed class PreflightServiceTests
{
    [Fact]
    public async Task CheckAsync_WhenDisplaySessionUnsupported_ReturnsEnvironmentError()
    {
        var displaySession = new FakeDisplaySessionService(supported: false, reason: "unsupported");
        var inputSimulator = Substitute.For<IInputSimulator>();
        var inputCapture = Substitute.For<IInputCapture>();

        var service = CreateService(displaySession, inputSimulator, inputCapture, isLinux: false);
        var result = await service.CheckAsync(PreflightTarget.Play, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.EnvironmentError, result.Outcome);
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

        var service = CreateService(displaySession, inputSimulator, inputCapture, isLinux: false);
        var result = await service.CheckAsync(PreflightTarget.Play, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.EnvironmentError, result.Outcome);
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

        var service = CreateService(displaySession, inputSimulator, inputCapture, isLinux: false);
        var result = await service.CheckAsync(PreflightTarget.Record, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.EnvironmentError, result.Outcome);
        Assert.Contains("capture backend", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckAsync_WhenHeadlessAndDisplaySupported_ReturnsSuccess()
    {
        var displaySession = new FakeDisplaySessionService(supported: true, reason: string.Empty);
        var inputSimulator = Substitute.For<IInputSimulator>();
        var inputCapture = Substitute.For<IInputCapture>();

        var service = CreateService(displaySession, inputSimulator, inputCapture, isLinux: false);
        var result = await service.CheckAsync(PreflightTarget.Headless, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(ExecutionOutcomeCode.Success, result.Outcome);
    }

    [Fact]
    public async Task CheckAsync_WhenHeadlessLinuxDisplayVariablesAreMissing_ReturnsEnvironmentError()
    {
        var displaySession = new FakeDisplaySessionService(supported: true, reason: string.Empty);
        var inputSimulator = Substitute.For<IInputSimulator>();
        var inputCapture = Substitute.For<IInputCapture>();

        var service = CreateService(displaySession, inputSimulator, inputCapture, isLinux: true);

        var result = await service.CheckAsync(PreflightTarget.Headless, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.EnvironmentError, result.Outcome);
        Assert.Contains("no active Linux display session", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckAsync_WhenRunAndLinuxDisplayVariablesAreMissing_ReportsUnavailableInputBackend()
    {
        var displaySession = new FakeDisplaySessionService(supported: true, reason: string.Empty);
        var inputSimulator = Substitute.For<IInputSimulator>();
        var inputCapture = Substitute.For<IInputCapture>();

        var service = CreateService(displaySession, inputSimulator, inputCapture, isLinux: true);

        var result = await service.CheckAsync(PreflightTarget.Run, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.EnvironmentError, result.Outcome);
        Assert.Contains("input simulation backend is unavailable", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static PreflightService CreateService(
        IDisplaySessionService displaySession,
        IInputSimulator inputSimulator,
        IInputCapture inputCapture,
        bool isLinux) =>
        new(displaySession, () => inputSimulator, () => inputCapture, () => isLinux, static _ => null);
}
