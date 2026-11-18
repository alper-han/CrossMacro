namespace CrossMacro.Cli.Tests;

/// <summary>
/// Process exit codes are a public contract for scripts and for the MCP server, which reuses the same numbers.
/// </summary>
public sealed class ExitCodeContractTests
{
    public static TheoryData<CliExitCode, int> PublishedExitCodes => new()
    {
        { CliExitCode.Success, 0 },
        { CliExitCode.InvalidArguments, 2 },
        { CliExitCode.FileError, 3 },
        { CliExitCode.ValidationError, 4 },
        { CliExitCode.EnvironmentError, 5 },
        { CliExitCode.RuntimeError, 6 },
        { CliExitCode.Cancelled, 130 },
    };

    [Theory]
    [MemberData(nameof(PublishedExitCodes))]
    public void CliExitCode_KeepsItsPublishedNumber(CliExitCode code, int expected) =>
        Assert.Equal(expected, (int)code);

    [Fact]
    public void ExecutionOutcomeCode_MapsToTheCliExitCodeWithTheSameNumber()
    {
        foreach (var outcome in Enum.GetValues<ExecutionOutcomeCode>())
        {
            Assert.Equal((int)outcome, (int)outcome.ToCliExitCode());
        }
    }

    [Fact]
    public void ExecutionOutcomeCode_WhenUndefined_MapsToRuntimeError() =>
        Assert.Equal(CliExitCode.RuntimeError, ((ExecutionOutcomeCode)99).ToCliExitCode());

    [Fact]
    public void CliExitCode_IsCoveredByTheExecutionOutcomeCodes() =>
        Assert.Equal(
            Enum.GetValues<CliExitCode>().Cast<int>().Order(),
            Enum.GetValues<ExecutionOutcomeCode>().Cast<int>().Order());
}
