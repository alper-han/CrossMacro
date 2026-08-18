namespace CrossMacro.Platform.Abstractions;

/// <summary>
/// Exposes whether a mouse position provider currently has a usable logical
/// cursor position. This is separate from the provider's static capability
/// declaration because some providers receive their first position
/// asynchronously.
/// </summary>
public interface IMousePositionAvailability
{
    public bool IsPositionAvailable { get; }
}
