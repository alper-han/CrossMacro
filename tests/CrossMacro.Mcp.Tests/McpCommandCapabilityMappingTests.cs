using CrossMacro.Application.Automation;

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
        Assert.Null(CreateAuthorization().RequireCommand(new DoctorCliOptions()));
    }

    [Theory]
    [InlineData(McpAutomationOperationKind.Play)]
    [InlineData(McpAutomationOperationKind.Run)]
    [InlineData(McpAutomationOperationKind.Record)]
    public void AutomationStartAndCommandExecute_ShouldRequireTheSameCapabilities(McpAutomationOperationKind kind)
    {
        CliCommandOptions options = kind switch
        {
            McpAutomationOperationKind.Play => new PlayCliOptions("macro.macro"),
            McpAutomationOperationKind.Run => new RunCliOptions(["click left"]),
            _ => new RecordCliOptions("out.macro"),
        };
        var operation = kind.ToString().ToLowerInvariant();

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
        Assert.NotNull(authorization.RequireCommand(new RunCliOptions(["click left"])));
    }

    private static McpToolAuthorization CreateAuthorization(IMcpCapabilityPolicy? policy = null) => new(
        policy ?? new AllowAllMcpCapabilityPolicy(), new McpPathAuthorizer(new AllowAllMcpPathPolicy()),
        new TestScheduleCommands(), new TestShortcutCommands(),
        new TestTriggerCommands(), new AutomationTaskAuthorization());

    private sealed record UnclassifiedCommandOptions() : CliCommandOptions(JsonOutput: false);

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
