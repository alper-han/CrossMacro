
namespace CrossMacro.Platform.Linux.Services.Factories;

public interface IPositionProviderSelector
{
    /// <summary>
    /// Priority of this selector. Higher values are checked first.
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Determines if this selector can handle the given compositor type.
    /// </summary>
    bool CanHandle(CompositorType compositor);

    /// <summary>
    /// Creates the mouse position provider.
    /// </summary>
    IMousePositionProvider Create();
}
