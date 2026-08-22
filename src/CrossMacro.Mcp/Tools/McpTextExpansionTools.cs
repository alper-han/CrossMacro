namespace CrossMacro.Mcp.Tools;

public sealed class McpTextExpansionTools(ITextExpansionCliService textExpansionCliService, McpToolAuthorization authorization)
{
    private readonly ITextExpansionCliService _textExpansionCliService = textExpansionCliService;
    private readonly McpToolAuthorization _authorization = authorization;

    [McpServerTool(Name = "text_expansion.list", Title = "List text expansions", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpTextExpansionsResult))]
    public async Task<McpTextExpansionsResult> ListTextExpansionsAsync(string? profile = null, CancellationToken cancellationToken = default) =>
        await ExecuteAsync("list", McpCapability.TextExpansionRead, token => _textExpansionCliService.ListAsync(profile, token), cancellationToken).ConfigureAwait(false);

    [McpServerTool(Name = "text_expansion.add", Title = "Add a text expansion", ReadOnly = false, Destructive = true, Idempotent = false, UseStructuredContent = true, OutputSchemaType = typeof(McpTextExpansionsResult))]
    public async Task<McpTextExpansionsResult> AddTextExpansionAsync(string trigger, string replacement, string? method = null, string? insertionMode = null, string? directTypingMethod = null, string? profile = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        ArgumentNullException.ThrowIfNull(replacement);
        var capability = _authorization.Require(McpCapability.TextExpansionWrite);
        if (capability is not null)
        {
            return Create("add", capability);
        }

        if (!Enum.TryParse(method ?? nameof(PasteMethod.CtrlV), ignoreCase: true, out PasteMethod pasteMethod)
            || !Enum.TryParse(insertionMode ?? nameof(TextInsertionMode.Paste), ignoreCase: true, out TextInsertionMode insertion)
            || !Enum.TryParse(directTypingMethod ?? nameof(DirectTypingMethod.FastBatch), ignoreCase: true, out DirectTypingMethod directTyping))
        {
            return Create("add", McpToolOutcomeMapper.InvalidArguments("Text expansion method options are invalid."));
        }

        var result = await _textExpansionCliService.AddAsync(trigger, replacement, pasteMethod, insertion, directTyping, profile, cancellationToken).ConfigureAwait(false);
        return Create("add", result);
    }

    [McpServerTool(Name = "text_expansion.remove", Title = "Remove a text expansion", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpTextExpansionsResult))]
    public async Task<McpTextExpansionsResult> RemoveTextExpansionAsync(string trigger, string? profile = null, CancellationToken cancellationToken = default) =>
        await ExecuteAsync("remove", McpCapability.TextExpansionWrite, token => _textExpansionCliService.RemoveAsync(trigger, profile, token), cancellationToken).ConfigureAwait(false);

    [McpServerTool(Name = "text_expansion.enable", Title = "Enable a text expansion", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpTextExpansionsResult))]
    public async Task<McpTextExpansionsResult> EnableTextExpansionAsync(string trigger, string? profile = null, CancellationToken cancellationToken = default) =>
        await ExecuteAsync("enable", McpCapability.TextExpansionWrite, token => _textExpansionCliService.EnableAsync(trigger, profile, token), cancellationToken).ConfigureAwait(false);

    [McpServerTool(Name = "text_expansion.disable", Title = "Disable a text expansion", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpTextExpansionsResult))]
    public async Task<McpTextExpansionsResult> DisableTextExpansionAsync(string trigger, string? profile = null, CancellationToken cancellationToken = default) =>
        await ExecuteAsync("disable", McpCapability.TextExpansionWrite, token => _textExpansionCliService.DisableAsync(trigger, profile, token), cancellationToken).ConfigureAwait(false);

    [McpServerTool(Name = "text_expansion.test", Title = "Test a text expansion", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpTextExpansionsResult))]
    public async Task<McpTextExpansionsResult> TestTextExpansionAsync(string trigger, string? profile = null, CancellationToken cancellationToken = default) =>
        await ExecuteAsync("test", McpCapability.TextExpansionRead, token => _textExpansionCliService.TestAsync(trigger, profile, token), cancellationToken).ConfigureAwait(false);

    private async Task<McpTextExpansionsResult> ExecuteAsync(string action, McpCapability requiredCapability, Func<CancellationToken, Task<CliCommandExecutionResult>> executeAsync, CancellationToken cancellationToken)
    {
        var capability = _authorization.Require(requiredCapability);
        if (capability is not null)
        {
            return Create(action, capability);
        }

        return Create(action, await executeAsync(cancellationToken).ConfigureAwait(false));
    }

    private static McpTextExpansionsResult Create(string action, McpToolOutcome outcome) =>
        new(action, outcome, [], ProfileId: null, Found: false);

    private static McpTextExpansionsResult Create(string action, CliCommandExecutionResult result)
    {
        var expansions = new List<McpTextExpansion>();
        string? profileId = null;
        var found = false;
        if (result.Data is TextExpansionListData list)
        {
            profileId = list.ProfileId;
            expansions.AddRange(list.Expansions.Select(ToTextExpansion));
        }
        else if (result.Data is TextExpansionData expansion)
        {
            found = true;
            expansions.Add(ToTextExpansion(expansion));
        }
        else if (result.Data is TextExpansionTestData test)
        {
            found = test.Found;
            if (test.Expansion is not null)
            {
                expansions.Add(ToTextExpansion(test.Expansion));
            }
        }

        var outcome = McpToolOutcomeMapper.FromCliResultRedactingErrorDetails(result);
        if (result.Data is TextExpansionTestData { Found: true } && outcome.Success)
        {
            outcome = outcome with { Message = "Text expansion resolved." };
        }

        return new(action, outcome, expansions, profileId, found);
    }

    private static McpTextExpansion ToTextExpansion(TextExpansionData expansion) =>
        new(expansion.Trigger, expansion.Replacement, expansion.IsEnabled, expansion.Method, expansion.InsertionMode, expansion.DirectTypingMethod);
}
