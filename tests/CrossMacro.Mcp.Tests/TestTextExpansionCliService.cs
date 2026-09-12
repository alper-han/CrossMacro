namespace CrossMacro.Mcp.Tests;

internal sealed class TestTextExpansionCliService : ITextExpansionCliService
{
    public CliCommandExecutionResult? ListResult { get; init; }
    public string? LastTrigger { get; private set; }
    public string? LastReplacement { get; private set; }
    public PasteMethod LastMethod { get; private set; }
    public TextInsertionMode LastInsertionMode { get; private set; }
    public DirectTypingMethod LastDirectTypingMethod { get; private set; }

    public Task<CliCommandExecutionResult> ListAsync(string? profileIdentifier, CancellationToken cancellationToken) =>
        Task.FromResult(ListResult ?? CliCommandExecutionResult.Ok("0 text expansion(s).", new TextExpansionListData([], profileIdentifier ?? string.Empty, 0)));

    public Task<CliCommandExecutionResult> AddAsync(string trigger, string replacement, PasteMethod method, TextInsertionMode insertionMode, DirectTypingMethod directTypingMethod, string? profileIdentifier, CancellationToken cancellationToken)
    {
        LastTrigger = trigger;
        LastReplacement = replacement;
        LastMethod = method;
        LastInsertionMode = insertionMode;
        LastDirectTypingMethod = directTypingMethod;
        return Task.FromResult(CliCommandExecutionResult.Ok("Text expansion added.", new TextExpansionData(
            Trigger: trigger,
            Replacement: replacement,
            IsEnabled: true,
            Method: method.ToString(),
            InsertionMode: insertionMode.ToString(),
            DirectTypingMethod: directTypingMethod.ToString())));
    }

    public Task<CliCommandExecutionResult> RemoveAsync(string trigger, string? profileIdentifier, CancellationToken cancellationToken) => Task.FromResult(CliCommandExecutionResult.Ok("Text expansion removed."));
    public Task<CliCommandExecutionResult> EnableAsync(string trigger, string? profileIdentifier, CancellationToken cancellationToken) => Task.FromResult(CliCommandExecutionResult.Ok("Text expansion enabled."));
    public Task<CliCommandExecutionResult> DisableAsync(string trigger, string? profileIdentifier, CancellationToken cancellationToken) => Task.FromResult(CliCommandExecutionResult.Ok("Text expansion disabled."));
    public Task<CliCommandExecutionResult> TestAsync(string trigger, string? profileIdentifier, CancellationToken cancellationToken) => Task.FromResult(CliCommandExecutionResult.Ok("Text expansion tested.", new TextExpansionTestData(
        Found: true,
        Expansion: new TextExpansionData(
            Trigger: trigger,
            Replacement: "replacement",
            IsEnabled: true,
            Method: "CtrlV",
            InsertionMode: "Paste",
            DirectTypingMethod: "FastBatch"))));
}
