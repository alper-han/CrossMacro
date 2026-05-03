using System;
using CrossMacro.Core.Models;

namespace CrossMacro.UI.ViewModels;

/// <summary>
/// UI-only projection for rendering editor action list rows.
/// </summary>
public sealed class EditorActionListItem
{
    public EditorActionListItem(
        EditorAction action,
        int index,
        int underlyingIndex,
        int indentLevel,
        string displayName,
        string condensedHint,
        EditorActionVisualKind visualKind,
        bool isImportant,
        bool isCleanupEligible,
        int condensedHiddenCount,
        bool representsSourceAction,
        bool isNoise = false)
    {
        Action = action ?? throw new ArgumentNullException(nameof(action));
        Index = index;
        UnderlyingIndex = underlyingIndex;
        IndentLevel = indentLevel;
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        CondensedHint = condensedHint ?? throw new ArgumentNullException(nameof(condensedHint));
        DisplayTooltip = string.IsNullOrWhiteSpace(CondensedHint) ? DisplayName : CondensedHint;
        VisualKind = visualKind;
        IsImportant = isImportant;
        IsCleanupEligible = isCleanupEligible;
        CondensedHiddenCount = condensedHiddenCount;
        RepresentsSourceAction = representsSourceAction;
        IsNoise = isNoise;
    }

    public EditorAction Action { get; }

    public int Index { get; }

    public int UnderlyingIndex { get; }

    public int IndentLevel { get; }

    public string DisplayName { get; }

    public string CondensedHint { get; }

    public string DisplayTooltip { get; }

    public EditorActionVisualKind VisualKind { get; }

    public bool IsImportant { get; }

    public bool IsCleanupEligible { get; }

    public int CondensedHiddenCount { get; }

    public bool RepresentsSourceAction { get; }

    public bool IsNoise { get; }
}
