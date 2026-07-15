
namespace CrossMacro.UI.ViewModels;

public sealed record class EditorActionPickerGroup(string DisplayName, IReadOnlyList<EditorActionPickerChoice> Choices);
