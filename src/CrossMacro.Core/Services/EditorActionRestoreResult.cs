
namespace CrossMacro.Core.Services;

/// <summary>
/// Result of restoring editor actions from a macro sequence.
/// </summary>
public sealed class EditorActionRestoreResult(
    IReadOnlyList<EditorAction> actions,
    IReadOnlyList<EditorActionRestoreWarning> warnings,
    bool restoredFromScriptSteps)
{
    public IReadOnlyList<EditorAction> Actions { get; } = actions ?? throw new ArgumentNullException(nameof(actions));

    public IReadOnlyList<EditorActionRestoreWarning> Warnings { get; } = warnings ?? throw new ArgumentNullException(nameof(warnings));

    public bool RestoredFromScriptSteps { get; } = restoredFromScriptSteps;

    public bool HasWarnings => Warnings.Count > 0;
}
