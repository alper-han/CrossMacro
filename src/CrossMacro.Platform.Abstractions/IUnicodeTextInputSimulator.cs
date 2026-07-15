namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// Optional capability for simulators that can inject arbitrary Unicode text
/// without relying on the active keyboard layout.
/// </summary>
public interface IUnicodeTextInputSimulator
{
    bool SupportsUnicodeTextInput { get; }

    void TypeText(string text);
}
