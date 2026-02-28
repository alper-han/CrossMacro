using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Cli;
using CrossMacro.Cli.Commands;
using CrossMacro.Cli.Services;
using NSubstitute;
using Xunit;

namespace CrossMacro.Cli.Tests;

public class MacroInfoCommandHandlerTests
{
    private readonly IMacroExecutionService _executionService;
    private readonly MacroInfoCommandHandler _handler;

    public MacroInfoCommandHandlerTests()
    {
        _executionService = Substitute.For<IMacroExecutionService>();
        _handler = new MacroInfoCommandHandler(_executionService);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInfoFails_ReturnsErrorCode()
    {
        var options = new MacroInfoCliOptions("/tmp/missing.macro");
        _executionService.GetInfoAsync(options.MacroFilePath, Arg.Any<CancellationToken>())
            .Returns(new MacroExecutionResult
            {
                Success = false,
                ExitCode = CliExitCode.FileError,
                Message = "Macro file not found.",
                Errors = ["File does not exist"]
            });

        var result = await _handler.ExecuteAsync(options, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.FileError, result.ExitCode);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInfoSucceeds_ReturnsSuccess()
    {
        var options = new MacroInfoCliOptions("/tmp/ok.macro", JsonOutput: true);
        _executionService.GetInfoAsync(options.MacroFilePath, Arg.Any<CancellationToken>())
            .Returns(new MacroExecutionResult
            {
                Success = true,
                ExitCode = CliExitCode.Success,
                Message = "Macro info loaded."
            });

        var result = await _handler.ExecuteAsync(options, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
    }
}
