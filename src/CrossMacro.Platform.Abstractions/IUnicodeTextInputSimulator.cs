namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// Optional capability for simulators that can inject arbitrary Unicode text
/// without relying on the active keyboard layout.
/// </summary>
public interface IUnicodeTextInputSimulator
{
    public bool SupportsUnicodeTextInput { get; }

    public void TypeText(string text);
}
