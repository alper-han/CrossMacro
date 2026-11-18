namespace CrossMacro.Mcp.Tests;

public sealed class CrossMacroMcpToolCatalogTests
{
    [Fact]
    public void ToolNames_AreStableAndOrdered()
    {
        var names = CrossMacroMcpToolCatalog.V1.Select(static definition => definition.Name).ToArray();

        Assert.Equal(
        [
            "status.get",
            "help.get",
            "setup.status",
            "daemon.status",
            "settings.get",
            "settings.set",
            "settings.list_keys",
            "settings.reset",
            "profile.list",
            "profile.current",
            "profile.create",
            "profile.switch",
            "profile.rename",
            "profile.delete",
            "text_expansion.list",
            "text_expansion.add",
            "text_expansion.remove",
            "text_expansion.enable",
            "text_expansion.disable",
            "text_expansion.test",
            "schedule.list",
            "schedule.run",
            "schedule.add",
            "schedule.edit",
            "schedule.remove",
            "schedule.enable",
            "schedule.disable",
            "schedule.next",
            "shortcut.list",
            "shortcut.run",
            "shortcut.add",
            "shortcut.edit",
            "shortcut.remove",
            "shortcut.enable",
            "shortcut.disable",
            "shortcut.bind",
            "trigger.list",
            "trigger.add",
            "trigger.edit",
            "trigger.remove",
            "trigger.enable",
            "trigger.disable",
            "command.execute",
            "automation.start",
            "automation.get",
            "automation.stop",
            "macro.list",
            "macro.inspect",
            "macro.validate",
            "clipboard.get_text",
            "clipboard.set_text",
            "clipboard.get_image",
            "clipboard.set_image",
            "window.query",
            "window.control",
            "screen.read",
            "cursor.position",
            "screen.find_image",
            "image.read",
            "screenshot.capture",
        ],
        names);
    }

    [Fact]
    public void Tools_AreDescribedAndUniquelyNamed()
    {
        Assert.Equal(
            CrossMacroMcpToolCatalog.V1.Count,
            CrossMacroMcpToolCatalog.V1.Select(static definition => definition.Name)
                .Distinct(StringComparer.Ordinal)
                .Count());

        Assert.All(CrossMacroMcpToolCatalog.V1, definition =>
        {
            Assert.False(string.IsNullOrWhiteSpace(definition.Name));
            Assert.False(string.IsNullOrWhiteSpace(definition.Title));
            Assert.False(string.IsNullOrWhiteSpace(definition.Description));
        });
    }

    [Fact]
    public void EffectfulTools_MatchTheExpectedSet()
    {
        var effectful = CrossMacroMcpToolCatalog.V1
            .Where(static definition => definition.Access is McpToolAccess.Effectful)
            .Select(static definition => definition.Name)
            .Order(StringComparer.Ordinal);

        Assert.Equal(
        [
            "automation.start",
            "automation.stop",
            "clipboard.set_image",
            "clipboard.set_text",
            "command.execute",
            "profile.create",
            "profile.delete",
            "profile.rename",
            "profile.switch",
            "schedule.add",
            "schedule.disable",
            "schedule.edit",
            "schedule.enable",
            "schedule.remove",
            "schedule.run",
            "screenshot.capture",
            "settings.reset",
            "settings.set",
            "shortcut.add",
            "shortcut.bind",
            "shortcut.disable",
            "shortcut.edit",
            "shortcut.enable",
            "shortcut.remove",
            "shortcut.run",
            "text_expansion.add",
            "text_expansion.disable",
            "text_expansion.enable",
            "text_expansion.remove",
            "trigger.add",
            "trigger.disable",
            "trigger.edit",
            "trigger.enable",
            "trigger.remove",
            "window.control",
        ],
        effectful,
        StringComparer.Ordinal);
    }

    [Fact]
    public void JsonContext_ShouldProvideGeneratedMetadataForTheToolContract()
    {
        Assert.NotNull(McpJsonContext.Default.GetTypeInfo(typeof(McpToolDefinition)));
        Assert.NotNull(McpJsonContext.Default.GetTypeInfo(typeof(McpToolDefinition[])));
        Assert.NotNull(McpJsonContext.Default.GetTypeInfo(typeof(McpSettingsResult)));
        Assert.NotNull(McpJsonContext.Default.GetTypeInfo(typeof(McpStatusResult)));
        Assert.NotNull(McpJsonContext.Default.GetTypeInfo(typeof(McpHelpResult)));
        Assert.NotNull(McpJsonContext.Default.GetTypeInfo(typeof(McpClipboardImageResult)));
        Assert.NotNull(McpJsonContext.Default.GetTypeInfo(typeof(McpClipboardSetImageResult)));
        Assert.NotNull(McpJsonContext.Default.GetTypeInfo(typeof(McpImageClipboardCapability)));
        Assert.NotNull(McpJsonContext.Default.GetTypeInfo(typeof(McpAutomationOperation)));
        Assert.NotNull(McpJsonContext.Default.GetTypeInfo(typeof(McpAutomationOperationStartResult)));
        Assert.NotNull(McpJsonContext.Default.GetTypeInfo(typeof(McpAutomationOperationStopResult)));
        Assert.NotNull(McpJsonContext.Default.GetTypeInfo(typeof(McpAutomationStartResult)));
        Assert.NotNull(McpJsonContext.Default.GetTypeInfo(typeof(McpAutomationGetResult)));
        Assert.NotNull(McpJsonContext.Default.GetTypeInfo(typeof(McpAutomationStopResult)));
        Assert.NotNull(McpJsonContext.Default.GetTypeInfo(typeof(McpSetupResult)));
        Assert.NotNull(McpJsonContext.Default.GetTypeInfo(typeof(McpDaemonResult)));
        Assert.NotNull(McpJsonContext.Default.GetTypeInfo(typeof(McpWindowControlResult)));
        Assert.NotNull(McpJsonContext.Default.GetTypeInfo(typeof(McpCursorPositionResult)));
        Assert.Equal("sampleName", McpJsonContext.Default.Options.PropertyNamingPolicy!.ConvertName("SampleName"));
    }

    [Fact]
    public void EveryTool_DeclaresAtLeastOneCapability()
    {
        Assert.All(CrossMacroMcpToolCatalog.V1, definition => Assert.NotEmpty(definition.Capabilities));
    }

    [Fact]
    public void AutomationStart_ShouldDeclareOperationSpecificCapabilityRequirements()
    {
        var definition = Assert.Single(
            CrossMacroMcpToolCatalog.V1,
            static tool => tool.Name is "automation.start");

        Assert.Equal(
            ["play", "run", "record"],
            definition.OperationCapabilities.Select(static item => item.Operation),
            StringComparer.Ordinal);
        Assert.Equal([McpCapability.MacroRead, McpCapability.InputAutomation], definition.OperationCapabilities[0].Capabilities);
        Assert.Equal([McpCapability.CommandExecute, McpCapability.InputAutomation], definition.OperationCapabilities[1].Capabilities);
        Assert.Equal([McpCapability.Recording, McpCapability.FileWrite], definition.OperationCapabilities[2].Capabilities);
    }

    [Fact]
    public void Catalog_ShouldMatchTheExplicitMcpToolRegistration()
    {
        var registeredNames = McpToolTypeCatalog.All
            .SelectMany(static toolType => toolType.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            .Select(static method => method.GetCustomAttribute<McpServerToolAttribute>())
            .Where(static attribute => attribute is not null)
            .Select(static attribute => attribute!.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var catalogNames = CrossMacroMcpToolCatalog.V1
            .Select(static definition => definition.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(catalogNames, registeredNames);
    }

    [Fact]
    public void CliCompatibilityCatalog_ShouldOnlyReferenceCommandsFromThePublicCliContract()
    {
        var cliTokens = CliCommandContractCatalog.RootCommands
            .SelectMany(static command => new[] { command.CommandToken }.Concat(command.Aliases))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(
            McpCliCommandCatalog.SupportedCommands.Count,
            McpCliCommandCatalog.SupportedCommands.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(
            McpCliCommandCatalog.SupportedCommands,
            command => Assert.Contains(command, cliTokens, StringComparer.OrdinalIgnoreCase));
    }
}
