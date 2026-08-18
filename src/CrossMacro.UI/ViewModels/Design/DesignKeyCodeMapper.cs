
namespace CrossMacro.UI.ViewModels.Design;

internal sealed class DesignKeyCodeMapper : IKeyCodeMapper
{
    public string GetKeyName(int keyCode) => $"Key{keyCode.ToString(CultureInfo.InvariantCulture)}";

    public int GetKeyCode(string keyName) => 0;

    public bool IsModifierKeyCode(int code) => false;

    public int GetKeyCodeForCharacter(char character) => character;

    public bool RequiresShift(char character) => char.IsUpper(character);

    public bool RequiresAltGr(char character) => false;

    public char? GetCharacterForKeyCode(int keyCode, bool withShift = false) => (char)keyCode;
}
