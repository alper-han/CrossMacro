using System;
using CrossMacro.Core.Models;

namespace CrossMacro.UI.ViewModels;

public sealed class EditorMacroCreatedEventArgs : EventArgs
{
    public EditorMacroCreatedEventArgs(MacroSequence macro, string sourcePath)
    {
        Macro = macro ?? throw new ArgumentNullException(nameof(macro));
        SourcePath = string.IsNullOrWhiteSpace(sourcePath)
            ? throw new ArgumentException("Source path cannot be null or whitespace.", nameof(sourcePath))
            : sourcePath;
    }

    public MacroSequence Macro { get; }

    public string SourcePath { get; }
}
