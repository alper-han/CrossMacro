using System.Globalization;

namespace CrossMacro.Infrastructure.Tests.TestDoubles;

/// <summary>
/// Layout whose key names are the numeric key codes. With <paramref name="mapsCharacters"/> every key code is also
/// its own character and characters map to the upper-case key; without it no character maps to a key.
/// </summary>
internal sealed class TestKeyboardLayoutService(bool mapsCharacters = true) : IKeyboardLayoutService
{
    public string GetKeyName(int keyCode)
    {
        return keyCode.ToString(CultureInfo.InvariantCulture);
    }

    public int GetKeyCode(string keyName)
    {
        return -1;
    }

    public char? GetCharFromKeyCode(
        int keyCode,
        bool leftShift,
        bool rightShift,
        bool rightAlt,
        bool leftAlt,
        bool leftCtrl,
        bool capsLock)
    {
        return mapsCharacters && keyCode is >= char.MinValue and <= char.MaxValue ? (char)keyCode : null;
    }

    public (int KeyCode, bool Shift, bool AltGr)? GetInputForChar(char c)
    {
        return mapsCharacters ? (char.ToUpperInvariant(c), char.IsUpper(c), false) : null;
    }
}
