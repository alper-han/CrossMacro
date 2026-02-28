using CrossMacro.Cli;
using CrossMacro.Cli.Commands;
using CrossMacro.Cli.Services;
using NSubstitute;

namespace CrossMacro.Cli.Tests;

public class MacroValidateCommandHandlerTests
{
    private readonly IMacroExecutionService _executionService;
    private readonly MacroValidateCommandHandler _handler;

    public MacroValidateCommandHandlerTests()
    {
        _executionService = Substitute.For<IMacroExecutionService>();
        _handler = new MacroValidateCommandHandler(_executionService);
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidationFails_ReturnsErrorCode()
    {
        var options = new MacroValidateCliOptions("/tmp/missing.macro");
        _executionService.ValidateAsync(options.MacroFilePath, Arg.Any<CancellationToken>())
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
    public async Task ExecuteAsync_WhenValidationPasses_ReturnsSuccess()
    {
        var options = new MacroValidateCliOptions("/tmp/ok.macro", JsonOutput: true);
        _executionService.ValidateAsync(options.MacroFilePath, Arg.Any<CancellationToken>())
            .Returns(new MacroExecutionResult
            {
                Success = true,
                ExitCode = CliExitCode.Success,
                Message = "Macro is valid."
            });

        var result = await _handler.ExecuteAsync(options, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
    }
}
