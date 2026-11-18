namespace CrossMacro.UI.ViewModels.Editor;

/// <summary>Shared re-entrancy guard: applying one suggestion must not trigger another.</summary>
internal sealed class VariableSuggestionScope
{
    internal bool IsApplying { get; set; }
}
