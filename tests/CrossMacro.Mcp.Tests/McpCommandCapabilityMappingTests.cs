using CrossMacro.Application.Automation;
using CrossMacro.Mcp.Contracts.Automation;

namespace CrossMacro.Mcp.Tests;

public sealed class McpCommandCapabilityMappingTests
{
    [Fact]
    public void UnknownCommandOptions_AreDeniedEvenWhenCapabilitiesAreEnabled()
    {
        var authorization = CreateAuthorization();
        var result = authorization.RequireCommand(new UnclassifiedCommandOptions());
        Assert.NotNull(result);
        Assert.False(result.Success);
    }

    [Fact]
    public void Doctor_IsAnExplicitDiagnosticException()
    {
        Assert.Null(CreateAuthorization().RequireCommand(new DoctorOptions()));
    }

    [Theory]
    [InlineData(McpAutomationOperationKind.Play)]
    [InlineData(McpAutomationOperationKind.Run)]
    [InlineData(McpAutomationOperationKind.Record)]
    public void AutomationStartAndCommandExecute_ShouldRequireTheSameCapabilities(McpAutomationOperationKind kind)
    {
        CommandOptions options = kind switch
        {
            McpAutomationOperationKind.Play => new PlayOptions("macro.macro"),
            McpAutomationOperationKind.Run => new RunOptions(["click left"]),
            McpAutomationOperationKind.Record => new RecordOptions("out.macro"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, message: null),
        };
        var operation = kind switch
        {
            McpAutomationOperationKind.Play => "play",
            McpAutomationOperationKind.Run => "run",
            McpAutomationOperationKind.Record => "record",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, message: null),
        };

        foreach (var denied in Enum.GetValues<McpCapability>())
        {
            var authorization = CreateAuthorization(new DenyOneCapabilityPolicy(denied));
            var startDenied = authorization.RequireAutomation(operation) is not null;
            var commandDenied = authorization.RequireCommand(options) is not null;

            Assert.Equal(McpAutomationCapabilities.For(kind).Contains(denied), startDenied);
            Assert.Equal(startDenied, commandDenied);
        }
    }

    [Fact]
    public void RunAutomation_ShouldRequireInputAutomation()
    {
        var authorization = CreateAuthorization(new DenyOneCapabilityPolicy(McpCapability.InputAutomation));

        Assert.NotNull(authorization.RequireAutomation("run"));
        Assert.NotNull(authorization.RequireCommand(new RunOptions(["click left"])));
    }

    private static McpToolAuthorization CreateAuthorization(IMcpCapabilityPolicy? policy = null) => new(
        policy ?? new AllowAllMcpCapabilityPolicy(), new McpPathAuthorizer(new AllowAllMcpPathPolicy()),
        new TestScheduleCommands(), new TestShortcutCommands(),
        new TestTriggerCommands(), new AutomationTaskAuthorization());

    private sealed record UnclassifiedCommandOptions() : CommandOptions(JsonOutput: false);

    private sealed class DenyOneCapabilityPolicy(McpCapability denied) : IMcpCapabilityPolicy
    {
        public bool IsRestricted => false;

        public bool IsAllowed(McpCapability capability) => capability != denied;

        public bool IsAnyAllowed(params McpCapability[] capabilities) => capabilities.Any(IsAllowed);

        public McpToolOutcome Require(McpCapability capability) =>
            IsAllowed(capability)
                ? McpToolOutcomeMapper.Success(string.Empty)
                : McpToolOutcomeMapper.Denied("MCP capability is not enabled for this runtime.");

        public void SetRestricted(bool restricted)
        {
        }
    }
}
