namespace CrossMacro.Core.Models;

/// <summary>
/// Preserves the editor-authored boundary of a TextInput action inside a macro event stream.
/// </summary>
public sealed record class TextInputBoundary(int StartEventIndex, int EventCount, string Text);
