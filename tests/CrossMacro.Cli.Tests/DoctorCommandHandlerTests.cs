
using CrossMacro.Cli.Serialization;

namespace CrossMacro.Cli.Tests;

public sealed class DoctorCommandHandlerTests
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
        _ = _doctorService.RunAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new DoctorReport
            {
                Checks =
                [
                    new DoctorCheck { Name = "display-session", Status = DoctorCheckStatus.Fail, Message = "unsupported" },
                ],
            });

        var result = await _handler.ExecuteAsync(new DoctorCliOptions(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.EnvironmentError, result.ExitCode);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOnlyWarnings_ReturnsSuccess()
    {
        _ = _doctorService.RunAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new DoctorReport
            {
                Checks =
                [
                    new DoctorCheck { Name = "linux-uinput", Status = DoctorCheckStatus.Warn, Message = "warn" },
                ],
            });

        var result = await _handler.ExecuteAsync(new DoctorCliOptions(Verbose: true), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
    }

    [Fact]
    public async Task ExecuteAsync_PreservesReportOutputOrderAndDetails()
    {
        var details = new JsonObject { ["provider"] = "test" };
        _ = _doctorService.RunAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new DoctorReport
            {
                Checks =
                [
                    new DoctorCheck { Name = "first", Status = DoctorCheckStatus.Pass, Message = "first message", Details = details },
                    new DoctorCheck { Name = "second", Status = DoctorCheckStatus.Warn, Message = "second message" },
                ],
            });

        var result = await _handler.ExecuteAsync(new DoctorCliOptions(Verbose: true), CancellationToken.None);
        var data = Assert.IsType<DoctorCommandData>(result.Data);

        Assert.Collection(
            data.Checks,
            check =>
            {
                Assert.Equal("first", check.Name);
                Assert.Equal("pass", check.Status);
                Assert.Equal("first message", check.Message);
                Assert.Same(details, check.Details);
            },
            check =>
            {
                Assert.Equal("second", check.Name);
                Assert.Equal("warn", check.Status);
                Assert.Equal("second message", check.Message);
                Assert.Null(check.Details);
            });
        _ = await _doctorService.Received(1).RunAsync(verbose: true, CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDoctorIsCancelled_PropagatesCancellation()
    {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        _ = _doctorService.RunAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromCanceled<DoctorReport>(cancellationSource.Token));

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _handler.ExecuteAsync(new DoctorCliOptions(), cancellationSource.Token));
    }
}
