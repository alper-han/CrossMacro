using CrossMacro.Cli;
using CrossMacro.Cli.Commands;
using CrossMacro.Cli.Services;
using NSubstitute;

namespace CrossMacro.Cli.Tests;

public class DoctorCommandHandlerTests
{
    private readonly IDoctorService _doctorService;
    private readonly DoctorCommandHandler _handler;

    public DoctorCommandHandlerTests()
    {
        _doctorService = Substitute.For<IDoctorService>();
        _handler = new DoctorCommandHandler(_doctorService);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAnyCheckFails_ReturnsEnvironmentError()
    {
        _doctorService.RunAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new DoctorReport
            {
                Checks =
                [
                    new DoctorCheck { Name = "display-session", Status = DoctorCheckStatus.Fail, Message = "unsupported" }
                ]
            });

        var result = await _handler.ExecuteAsync(new DoctorCliOptions(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.EnvironmentError, result.ExitCode);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOnlyWarnings_ReturnsSuccess()
    {
        _doctorService.RunAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new DoctorReport
            {
                Checks =
                [
                    new DoctorCheck { Name = "linux-uinput", Status = DoctorCheckStatus.Warn, Message = "warn" }
                ]
            });

        var result = await _handler.ExecuteAsync(new DoctorCliOptions(Verbose: true), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
    }
}
