using CrossMacro.Core.Services;
using CrossMacro.Cli;
using CrossMacro.Cli.Services;
using NSubstitute;

namespace CrossMacro.Cli.Tests;

public class CliPreflightServiceTests
{
    [Fact]
    public async Task CheckAsync_WhenDisplaySessionUnsupported_ReturnsEnvironmentError()
    {
        var displaySession = Substitute.For<IDisplaySessionService>();
        var inputSimulator = Substitute.For<IInputSimulator>();
        var inputCapture = Substitute.For<IInputCapture>();
        displaySession.IsSessionSupported(out Arg.Any<string>()).Returns(callInfo =>
        {
            callInfo[0] = "unsupported";
            return false;
        });

        var service = new CliPreflightService(displaySession, inputSimulator, inputCapture, isLinux: () => false);
        var result = await service.CheckAsync(CliPreflightTarget.Play, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CliExitCode.EnvironmentError, result.ExitCode);
        Assert.Contains("display session", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckAsync_WhenPlayAndSimulatorUnsupported_ReturnsEnvironmentError()
    {
        var displaySession = Substitute.For<IDisplaySessionService>();
        var inputSimulator = Substitute.For<IInputSimulator>();
        var inputCapture = Substitute.For<IInputCapture>();
        displaySession.IsSessionSupported(out Arg.Any<string>()).Returns(callInfo =>
        {
            callInfo[0] = string.Empty;
            return true;
        });
        inputSimulator.IsSupported.Returns(false);
        inputSimulator.ProviderName.Returns("MockSimulator");

        var service = new CliPreflightService(displaySession, inputSimulator, inputCapture, isLinux: () => false);
        var result = await service.CheckAsync(CliPreflightTarget.Play, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CliExitCode.EnvironmentError, result.ExitCode);
        Assert.Contains("simulation backend", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckAsync_WhenRecordAndCaptureUnsupported_ReturnsEnvironmentError()
    {
        var displaySession = Substitute.For<IDisplaySessionService>();
        var inputSimulator = Substitute.For<IInputSimulator>();
        var inputCapture = Substitute.For<IInputCapture>();
        displaySession.IsSessionSupported(out Arg.Any<string>()).Returns(callInfo =>
        {
            callInfo[0] = string.Empty;
            return true;
        });
        inputCapture.IsSupported.Returns(false);
        inputCapture.ProviderName.Returns("MockCapture");

        var service = new CliPreflightService(displaySession, inputSimulator, inputCapture, isLinux: () => false);
        var result = await service.CheckAsync(CliPreflightTarget.Record, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CliExitCode.EnvironmentError, result.ExitCode);
        Assert.Contains("capture backend", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckAsync_WhenHeadlessAndDisplaySupported_ReturnsSuccess()
    {
        var displaySession = Substitute.For<IDisplaySessionService>();
        var inputSimulator = Substitute.For<IInputSimulator>();
        var inputCapture = Substitute.For<IInputCapture>();
        displaySession.IsSessionSupported(out Arg.Any<string>()).Returns(callInfo =>
        {
            callInfo[0] = string.Empty;
            return true;
        });

        var service = new CliPreflightService(displaySession, inputSimulator, inputCapture, isLinux: () => false);
        var result = await service.CheckAsync(CliPreflightTarget.Headless, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(CliExitCode.Success, result.ExitCode);
    }

    [Fact]
    public async Task CheckAsync_WhenLinuxAndDisplayVariablesMissing_ReturnsEnvironmentError()
    {
        var displaySession = Substitute.For<IDisplaySessionService>();
        var inputSimulator = Substitute.For<IInputSimulator>();
        var inputCapture = Substitute.For<IInputCapture>();
        displaySession.IsSessionSupported(out Arg.Any<string>()).Returns(callInfo =>
        {
            callInfo[0] = string.Empty;
            return true;
        });

        var service = new CliPreflightService(
            displaySession,
            inputSimulator,
            inputCapture,
            isLinux: () => true,
            getEnvironmentVariable: _ => null);

        var result = await service.CheckAsync(CliPreflightTarget.Run, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(CliExitCode.EnvironmentError, result.ExitCode);
        Assert.Contains("no active Linux display session", result.Message, StringComparison.OrdinalIgnoreCase);
    }
}
