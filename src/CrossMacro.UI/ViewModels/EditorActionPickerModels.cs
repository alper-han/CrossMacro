using System.Collections.Generic;
using CrossMacro.Core.Models;

namespace CrossMacro.UI.ViewModels;

public sealed record EditorActionPickerChoice(EditorActionType ActionType, string DisplayName);

public sealed record EditorActionPickerGroup(string DisplayName, IReadOnlyList<EditorActionPickerChoice> Choices);

public sealed record ShellCommandModeOption(ShellCommandMode Value, string DisplayName);

public sealed record WindowCommandModeOption(WindowCommandMode Value, string DisplayName);
