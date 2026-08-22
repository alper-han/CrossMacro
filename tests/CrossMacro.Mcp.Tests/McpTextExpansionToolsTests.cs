namespace CrossMacro.Mcp.Tests;

public sealed class McpTextExpansionToolsTests
{
    [Fact]
    public async Task TextExpansionTools_ShouldMapStructuredResultsAndForwardAddOptions()
    {
        var service = new TestTextExpansionCliService
        {
            ListResult = CliCommandExecutionResult.Ok(
                "1 text expansion(s).",
                new TextExpansionListData(
                [new TextExpansionData(":mail", "me@example.com", true, "CtrlShiftV", "Paste", "FastBatch")],
                "work",
                1)),
        };
        var tools = McpToolTestFactory.CreateTextExpansionTools(textExpansionCliService: service);

        var list = await tools.ListTextExpansionsAsync("work", CancellationToken.None);
        var add = await tools.AddTextExpansionAsync(":sig", "Regards", "CtrlShiftV", "DirectTyping", "CompatibleKeyByKey", "work", CancellationToken.None);

        Assert.True(list.Outcome.Success);
        Assert.Equal("work", list.ProfileId);
        Assert.Equal("me@example.com", Assert.Single(list.Expansions).Replacement);
        Assert.True(add.Outcome.Success);
        Assert.Equal(":sig", service.LastTrigger);
        Assert.Equal("Regards", service.LastReplacement);
        Assert.Equal(PasteMethod.CtrlShiftV, service.LastMethod);
        Assert.Equal(TextInsertionMode.DirectTyping, service.LastInsertionMode);
        Assert.Equal(DirectTypingMethod.CompatibleKeyByKey, service.LastDirectTypingMethod);
    }

    [Fact]
    public async Task TextExpansionMutation_ShouldRequireWriteCapability()
    {
        var policy = new McpCapabilityPolicy(new TestSettingsService(new AppSettings()));
        policy.SetRestricted(true);
        var tools = McpToolTestFactory.CreateTextExpansionTools(capabilityPolicy: policy);

        var result = await tools.RemoveTextExpansionAsync(":mail", cancellationToken: CancellationToken.None);

        Assert.False(result.Outcome.Success);
        Assert.Equal("capability_denied", Assert.Single(result.Outcome.Errors).Code);
    }
}
