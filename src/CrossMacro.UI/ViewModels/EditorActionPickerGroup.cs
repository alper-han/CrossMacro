using System.Collections.Generic;
using CrossMacro.Core.Models;

namespace CrossMacro.UI.ViewModels;

public sealed record EditorActionPickerGroup(string DisplayName, IReadOnlyList<EditorActionPickerChoice> Choices);
