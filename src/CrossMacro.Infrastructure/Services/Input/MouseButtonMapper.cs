
namespace CrossMacro.Infrastructure.Services.Input;

/// <summary>
/// Maps between Linux evdev BTN_* mouse button codes and their names. Buttons without a dedicated
/// name are called "MouseN" (N counts from BTN_LEFT = 1) and parse back to the same code.
/// </summary>
public static class MouseButtonMapper
{
    private const string GenericPrefix = "Mouse";
    private const int LastSupportedCode = InputEventCode.BTN_TASK + 10;

    private static readonly Dictionary<int, string> CodeToName = new()
    {
        { InputEventCode.BTN_LEFT, "Mouse Left" },
        { InputEventCode.BTN_RIGHT, "Mouse Right" },
        { InputEventCode.BTN_MIDDLE, "Mouse Middle" },
        { InputEventCode.BTN_SIDE, "Mouse Side" },
        { InputEventCode.BTN_EXTRA, "Mouse Extra" },
        { InputEventCode.BTN_FORWARD, "Mouse Forward" },
        { InputEventCode.BTN_BACK, "Mouse Back" },
        { InputEventCode.BTN_TASK, "Mouse Task" },
    };

    private static readonly Dictionary<string, int> NameToCode =
        CodeToName.ToDictionary(kvp => kvp.Value, kvp => kvp.Key, StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns the button name, or an empty string when the code is not a mouse button.</summary>
    public static string GetMouseButtonName(int buttonCode)
    {
        if (CodeToName.TryGetValue(buttonCode, out var name))
        {
            return name;
        }

        return buttonCode is >= InputEventCode.BTN_LEFT and <= LastSupportedCode
            ? GenericPrefix + (buttonCode - InputEventCode.BTN_LEFT + 1).ToString(CultureInfo.InvariantCulture)
            : string.Empty;
    }

    /// <summary>Returns the button code for a name produced by <see cref="GetMouseButtonName"/>, or -1.</summary>
    public static int GetButtonCode(string buttonName)
    {
        ArgumentNullException.ThrowIfNull(buttonName);

        if (NameToCode.TryGetValue(buttonName, out var code))
        {
            return code;
        }

        if (buttonName.StartsWith(GenericPrefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(
                buttonName.AsSpan(GenericPrefix.Length),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var number))
        {
            var generic = InputEventCode.BTN_LEFT + number - 1;
            if (generic is >= InputEventCode.BTN_LEFT and <= LastSupportedCode)
            {
                return generic;
            }
        }

        return -1;
    }
}
