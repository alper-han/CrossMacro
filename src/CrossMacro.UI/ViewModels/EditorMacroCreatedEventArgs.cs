
namespace CrossMacro.UI.ViewModels;

public sealed class EditorMacroCreatedEventArgs(MacroSequence macro, string sourcePath) : EventArgs
{
    public MacroSequence Macro { get; } = macro ?? throw new ArgumentNullException(nameof(macro));

    public string SourcePath { get; } = string.IsNullOrWhiteSpace(sourcePath)
            ? throw new ArgumentException("Source path cannot be null or whitespace.", nameof(sourcePath))
            : sourcePath;
}
