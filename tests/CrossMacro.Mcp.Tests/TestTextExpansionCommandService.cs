namespace CrossMacro.Mcp.Tests;

internal sealed class TestTextExpansionCommandService : ITextExpansionCommandService
{
    public OperationResult? ListResult { get; init; }
    public string? LastTrigger { get; private set; }
    public string? LastReplacement { get; private set; }
    public PasteMethod LastMethod { get; private set; }
    public TextInsertionMode LastInsertionMode { get; private set; }
    public DirectTypingMethod LastDirectTypingMethod { get; private set; }

    public Task<OperationResult> ListAsync(string? profileIdentifier, CancellationToken cancellationToken) =>
        Task.FromResult(ListResult ?? OperationResult.Ok("0 text expansion(s).", new TextExpansionListData([], profileIdentifier ?? string.Empty, 0)));

    public Task<OperationResult> AddAsync(string trigger, string replacement, PasteMethod method, TextInsertionMode insertionMode, DirectTypingMethod directTypingMethod, string? profileIdentifier, CancellationToken cancellationToken)
    {
        LastTrigger = trigger;
        LastReplacement = replacement;
        LastMethod = method;
        LastInsertionMode = insertionMode;
        LastDirectTypingMethod = directTypingMethod;
        return Task.FromResult(OperationResult.Ok("Text expansion added.", new TextExpansionData(
            Trigger: trigger,
            Replacement: replacement,
            IsEnabled: true,
            Method: method.ToString(),
            InsertionMode: insertionMode.ToString(),
            DirectTypingMethod: directTypingMethod.ToString())));
    }

    public Task<OperationResult> RemoveAsync(string trigger, string? profileIdentifier, CancellationToken cancellationToken) => Task.FromResult(OperationResult.Ok("Text expansion removed."));
    public Task<OperationResult> EnableAsync(string trigger, string? profileIdentifier, CancellationToken cancellationToken) => Task.FromResult(OperationResult.Ok("Text expansion enabled."));
    public Task<OperationResult> DisableAsync(string trigger, string? profileIdentifier, CancellationToken cancellationToken) => Task.FromResult(OperationResult.Ok("Text expansion disabled."));
    public Task<OperationResult> TestAsync(string trigger, string? profileIdentifier, CancellationToken cancellationToken) => Task.FromResult(OperationResult.Ok("Text expansion tested.", new TextExpansionTestData(
        Found: true,
        Expansion: new TextExpansionData(
            Trigger: trigger,
            Replacement: "replacement",
            IsEnabled: true,
            Method: "CtrlV",
            InsertionMode: "Paste",
            DirectTypingMethod: "FastBatch"))));
}
