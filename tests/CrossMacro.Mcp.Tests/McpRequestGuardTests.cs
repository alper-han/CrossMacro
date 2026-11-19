using CrossMacro.Mcp.Services;

namespace CrossMacro.Mcp.Tests;

public sealed class McpRequestGuardTests
{
    public static TheoryData<string> EffectfulTools()
    {
        var names = new TheoryData<string>();
        foreach (var tool in CrossMacroMcpToolCatalog.V1.Where(static tool => tool.Access is McpToolAccess.Effectful && tool.OperationCapabilities.Count is 0))
        {
            names.Add(tool.Name);
        }

        return names;
    }

    public static TheoryData<string> AutomationKinds() =>
        new(CrossMacroMcpToolCatalog.V1.Single(static tool => tool.Name is "automation.start").OperationCapabilities.Select(static operation => operation.Operation));

    [Theory]
    [MemberData(nameof(EffectfulTools))]
    public async Task EffectfulTool_WhenNoCapabilityIsAllowed_IsRejectedBeforeItRuns(string toolName)
    {
        var next = new CountingNext();

        var result = await new McpRequestGuard(new SelectiveCapabilityPolicy()).InvokeAsync(toolName, next.InvokeAsync);

        Assert.True(result.IsError);
        Assert.Equal(0, next.CallCount);
    }

    [Theory]
    [MemberData(nameof(EffectfulTools))]
    public async Task EffectfulTool_WhenItsRequiredCapabilitiesAreAllowed_Runs(string toolName)
    {
        var definition = CrossMacroMcpToolCatalog.V1.Single(tool => string.Equals(tool.Name, toolName, StringComparison.Ordinal));
        var allowed = definition.CapabilityRequirement is McpCapabilityRequirement.Any
            ? definition.Capabilities.Take(1)
            : definition.Capabilities;
        var next = new CountingNext();

        var result = await new McpRequestGuard(new SelectiveCapabilityPolicy([.. allowed])).InvokeAsync(toolName, next.InvokeAsync);

        Assert.NotEqual(true, result.IsError);
        Assert.Equal(1, next.CallCount);
    }

    [Theory]
    [MemberData(nameof(EffectfulTools))]
    public async Task EffectfulTool_WhenTheFirstCapabilityIsMissing_RunsOnlyIfAnotherOneSuffices(string toolName)
    {
        var definition = CrossMacroMcpToolCatalog.V1.Single(tool => string.Equals(tool.Name, toolName, StringComparison.Ordinal));
        var anotherSuffices = definition.CapabilityRequirement is McpCapabilityRequirement.Any && definition.Capabilities.Count > 1;
        var next = new CountingNext();

        var result = await new McpRequestGuard(new SelectiveCapabilityPolicy([.. definition.Capabilities.Skip(1)])).InvokeAsync(toolName, next.InvokeAsync);

        Assert.Equal(anotherSuffices ? 1 : 0, next.CallCount);
        Assert.Equal(!anotherSuffices, result.IsError is true);
    }

    [Theory]
    [MemberData(nameof(AutomationKinds))]
    public async Task AutomationStart_RequiresTheCapabilitiesOfTheRequestedKind(string kind)
    {
        var definition = CrossMacroMcpToolCatalog.V1.Single(static tool => tool.Name is "automation.start");
        var required = definition.OperationCapabilities.Single(operation => string.Equals(operation.Operation, kind, StringComparison.Ordinal)).Capabilities;
        var arguments = new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["kind"] = JsonSerializer.SerializeToElement(kind) };

        var denied = new CountingNext();
        var deniedResult = await new McpRequestGuard(new SelectiveCapabilityPolicy([.. required.Skip(1)])).InvokeAsync("automation.start", denied.InvokeAsync, arguments);
        var allowed = new CountingNext();
        var allowedResult = await new McpRequestGuard(new SelectiveCapabilityPolicy([.. required])).InvokeAsync("automation.start", allowed.InvokeAsync, arguments);

        Assert.True(deniedResult.IsError);
        Assert.Equal(0, denied.CallCount);
        Assert.NotEqual(true, allowedResult.IsError);
        Assert.Equal(1, allowed.CallCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("unknown")]
    public async Task AutomationStart_WithoutARecognisedKind_IsNotRejectedByTheGuardAndLeavesValidationToTheTool(string? kind)
    {
        var arguments = kind is null
            ? null
            : new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["kind"] = JsonSerializer.SerializeToElement(kind) };
        var next = new CountingNext();

        _ = await new McpRequestGuard(new SelectiveCapabilityPolicy()).InvokeAsync("automation.start", next.InvokeAsync, arguments);

        Assert.Equal(1, next.CallCount);
    }

    [Theory]
    [InlineData("status.get")]
    [InlineData("macro.list")]
    [InlineData("settings.get")]
    public async Task ReadOnlyTool_IsLeftToTheToolToAuthorize(string toolName)
    {
        var next = new CountingNext();

        _ = await new McpRequestGuard(new SelectiveCapabilityPolicy()).InvokeAsync(toolName, next.InvokeAsync);

        Assert.Equal(1, next.CallCount);
    }

    private sealed class CountingNext
    {
        public int CallCount { get; private set; }

        public ValueTask<CallToolResult> InvokeAsync()
        {
            CallCount++;
            return ValueTask.FromResult(new CallToolResult());
        }
    }

    private sealed class SelectiveCapabilityPolicy(params McpCapability[] allowed) : IMcpCapabilityPolicy
    {
        public bool IsRestricted => false;

        public bool IsAllowed(McpCapability capability) => allowed.Contains(capability);

        public bool IsAnyAllowed(params McpCapability[] capabilities) => capabilities.Any(IsAllowed);

        public McpToolOutcome Require(McpCapability capability) =>
            IsAllowed(capability) ? McpToolOutcomeMapper.Success(string.Empty) : McpToolOutcomeMapper.Denied("denied");

        public void SetRestricted(bool restricted)
        {
        }
    }
}
