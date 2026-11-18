namespace CrossMacro.Mcp.Tests;

/// <summary>
/// Every registered MCP tool must refuse to act when the capability policy denies everything,
/// reached through the same protocol path a client uses.
/// </summary>
public sealed class McpToolCapabilityGateTests
{
    private static readonly Dictionary<string, object?> AutomationStartArguments = new(StringComparer.Ordinal) { ["kind"] = "run" };
    private static readonly Dictionary<string, object?> CommandExecuteArguments = new(StringComparer.Ordinal) { ["command"] = "play" };

    /// <summary>
    /// StatusRead is always allowed by the policy, so only status.get consults it (and reports an empty status);
    /// the remaining status tools expose no gated behavior.
    /// </summary>
    private static readonly HashSet<string> StatusTools = new(["status.get", "help.get", "setup.status", "daemon.status"], StringComparer.Ordinal);

    public static TheoryData<string> ToolNames()
    {
        var names = new TheoryData<string>();
        foreach (var definition in CrossMacroMcpToolCatalog.V1.Where(static definition => !StatusTools.Contains(definition.Name)))
        {
            names.Add(definition.Name);
        }

        return names;
    }

    [Fact]
    public async Task StatusGet_WhenEveryCapabilityIsDenied_ReportsAnEmptyStatus()
    {
        var tools = McpToolTestFactory.CreateRuntimeTools(capabilityPolicy: new DenyAllCapabilityPolicy());

        var status = await tools.GetStatusAsync(CancellationToken.None);

        Assert.Empty(status.AvailableTools);
        Assert.Empty(status.EnabledCapabilities);
        Assert.Null(status.ActiveOperation);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_WhenEveryCapabilityIsDenied_RefusesWithoutTouchingTheSystem(string toolName)
    {
        var scheduleCommands = new TestScheduleCommands();
        var shortcutCommands = new TestShortcutCommands();
        var triggerCommands = new TestTriggerCommands();
        var policy = new DenyAllCapabilityPolicy();
        var macroExecution = new TestMacroExecutionService();
        var runScript = new TestRunScriptExecutionService();
        var screenshot = new TestScreenshotCaptureService();
        var screenReader = new FakeScreenReader();
        var clipboard = new TestClipboardCommandService();
        var window = new TestWindowCommandService();
        var coordinator = McpToolTestFactory.CreateOperationCoordinator();
        await using var server = await InProcessMcpServer.StartAsync(builder => builder.WithCrossMacroTools(
            McpToolTestFactory.CreateRuntimeTools(operationCoordinator: coordinator, capabilityPolicy: policy, scheduleCommands: scheduleCommands, shortcutCommands: shortcutCommands, triggerCommands: triggerCommands),
            McpToolTestFactory.CreateSettingsTools(capabilityPolicy: policy, scheduleCommands: scheduleCommands, shortcutCommands: shortcutCommands, triggerCommands: triggerCommands),
            McpToolTestFactory.CreateProfileTools(capabilityPolicy: policy, scheduleCommands: scheduleCommands, shortcutCommands: shortcutCommands, triggerCommands: triggerCommands),
            McpToolTestFactory.CreateTextExpansionTools(capabilityPolicy: policy, scheduleCommands: scheduleCommands, shortcutCommands: shortcutCommands, triggerCommands: triggerCommands),
            McpToolTestFactory.CreateTaskTools(scheduleCommands, shortcutCommands, triggerCommands, policy),
            McpToolTestFactory.CreateAutomationTools(macroExecution, coordinator, runScript, capabilityPolicy: policy, scheduleCommands: scheduleCommands, shortcutCommands: shortcutCommands, triggerCommands: triggerCommands),
            McpToolTestFactory.CreateCommandTools(macroExecution, coordinator, runScript, capabilityPolicy: policy, scheduleCommands: scheduleCommands, shortcutCommands: shortcutCommands, triggerCommands: triggerCommands),
            McpToolTestFactory.CreateMacroTools(macroExecution, policy, scheduleCommands: scheduleCommands, shortcutCommands: shortcutCommands, triggerCommands: triggerCommands),
            McpToolTestFactory.CreateClipboardTools(clipboard, capabilityPolicy: policy, scheduleCommands: scheduleCommands, shortcutCommands: shortcutCommands, triggerCommands: triggerCommands),
            McpToolTestFactory.CreateWindowTools(window, policy, scheduleCommands: scheduleCommands, shortcutCommands: shortcutCommands, triggerCommands: triggerCommands),
            McpToolTestFactory.CreateScreenTools(screenReader, screenshot, capabilityPolicy: policy, scheduleCommands: scheduleCommands, shortcutCommands: shortcutCommands, triggerCommands: triggerCommands)));
        var tool = Assert.Single(await server.Client.ListToolsAsync(cancellationToken: server.Token), candidate => string.Equals(candidate.Name, toolName, StringComparison.Ordinal));

        var result = await server.Client.CallToolAsync(toolName, CreateArguments(tool), cancellationToken: server.Token);

        var structured = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal("capability_denied", FirstErrorCode(structured));
        Assert.Equal(0, macroExecution.GetInfoCallCount);
        Assert.Null(macroExecution.LastExecutionRequest);
        Assert.Null(runScript.LastRequest);
        Assert.Equal(0, screenshot.CallCount);
        Assert.Equal(0, screenReader.CallCount);
        Assert.Equal(0, scheduleCommands.ExecuteCallCount + scheduleCommands.RunCallCount);
        Assert.Equal(0, shortcutCommands.ExecuteCallCount + shortcutCommands.RunCallCount);
        Assert.Equal(0, triggerCommands.ExecuteCallCount);
        Assert.Equal(0, clipboard.GetCallCount + clipboard.SetCallCount);
        Assert.Equal(0, window.CallCount);
    }

    private static string? FirstErrorCode(JsonElement structured)
    {
        var outcome = structured.TryGetProperty("outcome", out var nested) ? nested : structured;
        return outcome.GetProperty("errors")[0].GetProperty("code").GetString();
    }

    private static Dictionary<string, object?> CreateArguments(McpClientTool tool)
    {
        if (tool.Name is "automation.start")
        {
            return AutomationStartArguments;
        }

        if (tool.Name is "command.execute")
        {
            return CommandExecuteArguments;
        }

        var arguments = new Dictionary<string, object?>(StringComparer.Ordinal);
        var schema = tool.ProtocolTool.InputSchema;
        if (!schema.TryGetProperty("required", out var required))
        {
            return arguments;
        }

        var properties = schema.GetProperty("properties");
        foreach (var name in required.EnumerateArray().Select(static item => item.GetString()!))
        {
            arguments[name] = CreateValue(properties.GetProperty(name));
        }

        return arguments;
    }

    private static object? CreateValue(JsonElement property)
    {
        var type = property.TryGetProperty("type", out var typeElement) ? typeElement : default;
        var typeName = type.ValueKind is JsonValueKind.Array
            ? type.EnumerateArray().Select(static item => item.GetString()).First(static item => item is not "null")
            : type.GetString();
        return typeName switch
        {
            "integer" or "number" => 0,
            "boolean" => false,
            "array" => Array.Empty<string>(),
            "object" => new Dictionary<string, object?>(StringComparer.Ordinal),
            _ => "x",
        };
    }

    private sealed class DenyAllCapabilityPolicy : IMcpCapabilityPolicy
    {
        public bool IsRestricted => false;

        public bool IsAllowed(McpCapability capability) => false;

        public bool IsAnyAllowed(params McpCapability[] capabilities) => false;

        public McpToolOutcome Require(McpCapability capability) =>
            McpToolOutcomeMapper.Denied("MCP capability is not enabled for this runtime.");

        public void SetRestricted(bool restricted)
        {
        }
    }
}
