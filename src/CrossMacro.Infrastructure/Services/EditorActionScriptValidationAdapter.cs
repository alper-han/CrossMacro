
namespace CrossMacro.Infrastructure.Services;

internal sealed class EditorActionScriptValidationAdapter(IEditorActionConverter converter, IScriptValidationService? service)
{
    private readonly IScriptValidationService? _service = service;
    private readonly IEditorActionConverter _converter = converter ?? throw new ArgumentNullException(nameof(converter));

    public (bool IsValid, string? Error) ValidateAction(EditorAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (string.IsNullOrWhiteSpace(action.Text))
        {
            return (false, "Raw script step cannot be empty.");
        }

        if (_service is not null)
        {
            var diagnostics = _service.Validate([new RunScriptStep(action.Text, SourceIndex: 0)]);
            var diagnostic = diagnostics.Count > 0 ? diagnostics[0] : null;
            return diagnostic is null ? (true, null) : (false, diagnostic.Message);
        }

        try
        {
            _ = _converter.ToMacroSequence([action], "Validation", isAbsolute: false);
            return (true, null);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return (false, exception.Message);
        }
    }

    public IReadOnlyList<string> Validate(IEnumerable<EditorAction> actions)
    {
        if (_service is null)
        {
            return [];
        }

        var steps = actions
            .Where(action => action.Type is EditorActionType.RawScriptStep && !string.IsNullOrWhiteSpace(action.Text))
            .Select((action, index) => new RunScriptStep(action.Text, SourceIndex: index))
            .ToList();
        return _service.Validate(steps)
            .Select(diagnostic => $"Script: {diagnostic.Message}")
            .ToArray();
    }
}
