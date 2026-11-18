
namespace CrossMacro.Cli.Tests;

public sealed class MacroInfoCommandHandlerTests
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
        var options = new MacroInfoOptions("/tmp/missing.macro");
        _ = _executionService.GetInfoAsync(options.MacroFilePath, Arg.Any<CancellationToken>())
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
    public async Task ExecuteAsync_WhenInfoSucceeds_ReturnsSuccess()
    {
        var options = new MacroInfoOptions("/tmp/ok.macro", JsonOutput: true);
        _ = _executionService.GetInfoAsync(options.MacroFilePath, Arg.Any<CancellationToken>())
            .Returns(new OperationResult
            {
                Success = true,
                Outcome = ExecutionOutcomeCode.Success,
                Message = "Macro info loaded.",
            });

        var result = await _handler.ExecuteAsync(options, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(ExecutionOutcomeCode.Success, result.Outcome);
    }
}
