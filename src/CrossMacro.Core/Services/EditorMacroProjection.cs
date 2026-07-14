using System;
using System.Collections.Generic;
using System.Linq;
using CrossMacro.Core.Models;

namespace CrossMacro.Core.Services;

/// <summary>
/// Editor-owned input to macro conversion. The runtime sequence remains the
/// canonical macro representation; this value carries editor choices only at
/// the conversion boundary.
/// </summary>
public sealed class EditorMacroProjection
{
    public EditorMacroProjection(
        IEnumerable<EditorAction> actions,
        string name,
        bool isAbsoluteCoordinates,
        bool skipInitialZeroZero = false)
    {
        ArgumentNullException.ThrowIfNull(actions);
        Actions = actions.ToArray();
        Name = name ?? throw new ArgumentNullException(nameof(name));
        IsAbsoluteCoordinates = isAbsoluteCoordinates;
        SkipInitialZeroZero = skipInitialZeroZero;
    }

    public IReadOnlyList<EditorAction> Actions { get; }

    public string Name { get; }

    public bool IsAbsoluteCoordinates { get; }

    public bool SkipInitialZeroZero { get; }
}
