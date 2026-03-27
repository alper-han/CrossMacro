using System;
using CrossMacro.Core.Models;

namespace CrossMacro.UI.ViewModels;

/// <summary>
/// UI-only projection for rendering editor action list rows.
/// </summary>
public sealed class EditorActionListItem
{
    public EditorActionListItem(EditorAction action, int index, int indentLevel, string displayName)
    {
        Action = action ?? throw new ArgumentNullException(nameof(action));
        Index = index;
        IndentLevel = indentLevel;
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
    }

    public EditorAction Action { get; }

    public int Index { get; }

    public int IndentLevel { get; }

    public string DisplayName { get; }
}
