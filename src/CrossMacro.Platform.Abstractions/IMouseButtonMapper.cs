namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// Maps between mouse button codes and their names.
/// </summary>
public interface IMouseButtonMapper
{
    /// <summary>
    /// Gets the human-readable name for a mouse button code.
    /// </summary>
    /// <param name="buttonCode">The button code (e.g., 272 for left click)</param>
    /// <returns>The button name (e.g., "Mouse Left"), or empty if unknown</returns>
    string GetMouseButtonName(int buttonCode);

    /// <summary>
    /// Gets the button code for a given mouse button name.
    /// </summary>
    /// <param name="buttonName">The button name (e.g., "Mouse Left")</param>
    /// <returns>The button code, or -1 if not found</returns>
    int GetButtonCode(string buttonName);

    /// <summary>
    /// Determines if the given code is a mouse button.
    /// </summary>
    /// <param name="code">The code to check</param>
    /// <returns>True if the code represents a mouse button</returns>
    bool IsMouseButton(int code);
}
