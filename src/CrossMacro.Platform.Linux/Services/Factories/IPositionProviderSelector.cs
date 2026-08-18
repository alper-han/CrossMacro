
namespace CrossMacro.Platform.Linux.Services.Factories;

public interface IPositionProviderSelector
{
    /// <summary>
    /// Priority of this selector. Higher values are checked first.
    /// </summary>
    public int Priority { get; }

    /// <summary>
    /// Determines if this selector can handle the given compositor type.
    /// </summary>
    public bool CanHandle(CompositorType compositor);

    /// <summary>
    /// Creates the mouse position provider.
    /// </summary>
    public IMousePositionProvider Create();
}
