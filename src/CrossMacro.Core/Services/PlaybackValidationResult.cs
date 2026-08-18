namespace CrossMacro.Core.Services;

public sealed class PlaybackValidationResult
{
    public bool IsValid => Errors.Count is 0;
    public IReadOnlyList<string> Warnings => _warnings;
    public IReadOnlyList<string> Errors => _errors;

    private readonly List<string> _warnings = [];
    private readonly List<string> _errors = [];

    public void AddWarning(string message) => _warnings.Add(message);
    public void AddError(string message) => _errors.Add(message);
}
