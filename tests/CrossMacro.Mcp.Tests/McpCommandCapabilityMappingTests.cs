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

    private static McpToolAuthorization CreateAuthorization() => new(
        new AllowAllMcpCapabilityPolicy(), new McpPathAuthorizer(new AllowAllMcpPathPolicy()),
        new TestScheduleCommands(), new TestShortcutCommands(),
        new TestTriggerCommands(), new AutomationTaskAuthorization());

    private sealed record UnclassifiedCommandOptions() : CliCommandOptions(JsonOutput: false);
}
