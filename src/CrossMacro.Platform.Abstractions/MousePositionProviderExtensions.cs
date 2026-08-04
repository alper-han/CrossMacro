namespace CrossMacro.Platform.Abstractions;

public static class MousePositionProviderExtensions
{
    /// <summary>
    /// Returns whether the provider can supply a position right now.
    /// Providers that do not expose a separate runtime availability contract
    /// retain the historical capability-based behavior.
    /// </summary>
    public static bool HasUsableAbsolutePosition(this IMousePositionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return provider is IMousePositionAvailability availability
            ? availability.IsPositionAvailable
            : provider.SupportsAbsolutePosition;
    }
}
