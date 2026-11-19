
namespace CrossMacro.Cli.Tests;

public sealed class MacroValidateCommandHandlerTests
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
        var options = new MacroValidateOptions("/tmp/missing.macro");
        _ = _executionService.ValidateAsync(options.MacroFilePath, Arg.Any<CancellationToken>())
            .Returns(new OperationResult
            {
                Success = false,
                Outcome = ExecutionOutcomeCode.FileError,
                Message = "Macro file not found.",
                Errors = ["File does not exist"],
            });

        var result = await _handler.ExecuteAsync(options, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ExecutionOutcomeCode.FileError, result.Outcome);
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidationPasses_ReturnsSuccess()
    {
        var options = new MacroValidateOptions("/tmp/ok.macro", JsonOutput: true);
        _ = _executionService.ValidateAsync(options.MacroFilePath, Arg.Any<CancellationToken>())
            .Returns(new OperationResult
            {
                Success = true,
                Outcome = ExecutionOutcomeCode.Success,
                Message = "Macro is valid.",
            });

        var result = await _handler.ExecuteAsync(options, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(ExecutionOutcomeCode.Success, result.Outcome);
    }
}
