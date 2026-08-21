namespace CrossMacro.Mcp.Tests;

public sealed class CrossMacroMcpToolCatalogTests
{
    [Fact]
    public void V1_ShouldExposeTheStableToolNamesInDeterministicOrder()
    {
        var names = CrossMacroMcpToolCatalog.V1.Select(static definition => definition.Name).ToArray();

        Assert.Equal(
        [
            "status.get",
            "help.get",
            "setup.status",
            "setup.run",
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
    public void V1_ShouldDescribeEveryToolAndDeclareItsAccessMode()
    {
        Assert.All(CrossMacroMcpToolCatalog.V1, definition =>
        {
            Assert.StartsWith("", definition.Name, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(definition.Title));
            Assert.False(string.IsNullOrWhiteSpace(definition.Description));
            Assert.True(Enum.IsDefined(definition.Access));
        });
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
    public void CapabilityMetadata_ShouldCoverEveryV1Tool()
    {
        Assert.All(CrossMacroMcpToolCatalog.V1, definition =>
        {
            Assert.NotEmpty(definition.Capabilities);
            Assert.All(definition.Capabilities, capability => Assert.True(Enum.IsDefined(capability)));
            Assert.True(Enum.IsDefined(definition.CapabilityRequirement));
        });
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
        Assert.Equal([McpCapability.CommandExecute], definition.OperationCapabilities[1].Capabilities);
        Assert.Equal([McpCapability.Recording, McpCapability.FileWrite], definition.OperationCapabilities[2].Capabilities);
    }

    [Fact]
    public void Catalog_ShouldMatchTheExplicitMcpToolRegistration()
    {
        var registeredNames = typeof(CrossMacroMcpTools)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
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

    [Fact]
    public void CliOptionMetadata_ShouldCoverEveryPublicCliOption()
    {
        var options = CliCommandContractCatalog.RootCommands
            .SelectMany(command => command.Options.Select(option => (command.CommandToken, option.Token)))
            .ToArray();

        Assert.Equal(options.Length, McpCommandCapabilityMetadataCatalog.All.Count);
        Assert.All(options, option =>
        {
            var metadata = McpCommandCapabilityMetadataCatalog.Get(option.CommandToken, option.Token);
            Assert.True(Enum.IsDefined(metadata.Capability));
            Assert.True(Enum.IsDefined(metadata.Access));
            Assert.True(Enum.IsDefined(metadata.Platform));
            Assert.True(metadata.MaximumDuration > TimeSpan.Zero);
            if (option.Token is "--macro" or "--output" or "-o" or "--file")
            {
                Assert.True(metadata.PathKind is not null, option.Token);
            }
        });
    }

    [Fact]
    public void CliCompatibilityCommands_ShouldHaveCanonicalParserReachability()
    {
        foreach (var command in McpCliCommandCatalog.SupportedCommands)
        {
            var result = CliCommandRouter.Parse([command, "--help"]);

            Assert.True(
                result.ShowHelp,
                $"MCP compatibility command '{command}' must remain invocable through the canonical CLI parser.");
        }
    }

    public static IEnumerable<object[]> RepresentativeCliCompatibilityInvocations()
    {
        yield return ["macro", new[] { "validate", "demo.macro", "--json" }];
        yield return ["play", new[] { "demo.macro", "--dry-run", "--json" }];
        yield return ["doctor", new[] { "--verbose", "--json" }];
        yield return ["record", new[] { "--output", "recorded.macro", "--duration", "0", "--json" }];
        yield return ["run", new[] { "--step", "delay 1ms", "--dry-run", "--json" }];
        yield return ["move", new[] { "abs", "1", "2", "--dry-run", "--json" }];
        yield return ["click", new[] { "left", "--dry-run", "--json" }];
        yield return ["down", new[] { "left", "--dry-run", "--json" }];
        yield return ["up", new[] { "left", "--dry-run", "--json" }];
        yield return ["scroll", new[] { "up", "1", "--dry-run", "--json" }];
        yield return ["key", new[] { "down", "A", "--dry-run", "--json" }];
        yield return ["tap", new[] { "CTRL+A", "--dry-run", "--json" }];
        yield return ["type", new[] { "hello", "--dry-run", "--json" }];
        yield return ["delay", new[] { "1ms", "--dry-run", "--json" }];
        yield return ["clipboard", new[] { "get", "--json" }];
        yield return ["window", new[] { "active", "--json" }];
        yield return ["screen", new[] { "pixel", "1", "2", "--json" }];
        yield return ["screenshot", new[] { "--clipboard", "--json" }];
    }

    [Theory]
    [MemberData(nameof(RepresentativeCliCompatibilityInvocations))]
    public void RepresentativeCompatibilityInvocation_ShouldParseExactlyLikeTheCli(string command, IReadOnlyList<string> arguments)
    {
        var cliResult = CliCommandRouter.Parse(arguments.Prepend(command).ToArray().AsMemory());

        Assert.True(cliResult.IsSuccess, $"{command}: {cliResult.ErrorMessage}");
        var options = Assert.IsAssignableFrom<CliCommandOptions>(cliResult.Options);
        Assert.True(options.JsonOutput);
    }
}
