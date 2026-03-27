using System;
using System.Collections.Generic;
using CrossMacro.Core.Models;

namespace CrossMacro.Core.Services;

/// <summary>
/// Warning produced while restoring script steps into editor actions.
/// </summary>
public sealed record EditorActionRestoreWarning(int StepIndex, string Step, string Message);

/// <summary>
/// Result of restoring editor actions from a macro sequence.
/// </summary>
public sealed class EditorActionRestoreResult
{
    public EditorActionRestoreResult(
        IReadOnlyList<EditorAction> actions,
        IReadOnlyList<EditorActionRestoreWarning> warnings,
        bool restoredFromScriptSteps)
    {
        Actions = actions ?? throw new ArgumentNullException(nameof(actions));
        Warnings = warnings ?? throw new ArgumentNullException(nameof(warnings));
        RestoredFromScriptSteps = restoredFromScriptSteps;
    }

    public IReadOnlyList<EditorAction> Actions { get; }

    public IReadOnlyList<EditorActionRestoreWarning> Warnings { get; }

    public bool RestoredFromScriptSteps { get; }

    public bool HasWarnings => Warnings.Count > 0;
}
