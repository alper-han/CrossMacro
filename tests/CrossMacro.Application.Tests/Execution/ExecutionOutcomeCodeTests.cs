using CrossMacro.Application.Execution;

namespace CrossMacro.Application.Tests.Execution;

/// <summary>
/// The numbers are a public contract: they are the CLI process exit codes and the codes the MCP server reports.
/// </summary>
public sealed class ExecutionOutcomeCodeTests
{
    public static TheoryData<ExecutionOutcomeCode, int> PublishedCodes => new()
    {
        { ExecutionOutcomeCode.Success, 0 },
        { ExecutionOutcomeCode.InvalidArguments, 2 },
        { ExecutionOutcomeCode.FileError, 3 },
        { ExecutionOutcomeCode.ValidationError, 4 },
        { ExecutionOutcomeCode.EnvironmentError, 5 },
        { ExecutionOutcomeCode.RuntimeError, 6 },
        { ExecutionOutcomeCode.Cancelled, 130 },
    };

    [Theory]
    [MemberData(nameof(PublishedCodes))]
    public void Outcome_KeepsItsPublishedNumber(ExecutionOutcomeCode outcome, int expected) =>
        Assert.Equal(expected, (int)outcome);

    [Fact]
    public void Outcomes_AreAllPublished() =>
        Assert.Equal(
            PublishedCodes.Select(row => (int)row.Data.Item1).Order(),
            Enum.GetValues<ExecutionOutcomeCode>().Cast<int>().Order());
}
