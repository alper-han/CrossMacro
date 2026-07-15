
namespace CrossMacro.Infrastructure.Services;

internal sealed class EditorActionScriptValidationAdapter
{
    private readonly IScriptValidationService? _service;
    private readonly IEditorActionConverter _converter;

    public EditorActionScriptValidationAdapter(IEditorActionConverter converter, IScriptValidationService? service)
    {
        _converter = converter ?? throw new ArgumentNullException(nameof(converter));
        _service = service;
    }

    public (bool IsValid, string? Error) ValidateAction(EditorAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (string.IsNullOrWhiteSpace(action.Text))
        {
            return (false, "Raw script step cannot be empty.");
        }

        if (_service is not null)
        {
            var diagnostic = _service.Validate([new RunScriptStep(action.Text, SourceIndex: 0)]).FirstOrDefault();
            return diagnostic is null ? (true, null) : (false, diagnostic.Message);
        }

        try
        {
            _converter.ToMacroSequence([action], "Validation", isAbsolute: false);
            return (true, null);
        }
        catch (Exception exception)
        {
            return (false, exception.Message);
        }
    }

    public IReadOnlyList<string> Validate(IEnumerable<EditorAction> actions)
    {
        if (_service is null)
        {
            return Array.Empty<string>();
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
