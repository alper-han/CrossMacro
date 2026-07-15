
namespace CrossMacro.Platform.Linux.Services.Factories.Selectors;

public class HyprlandPositionProviderSelector : IPositionProviderSelector
{
    public int Priority => 10;

    public bool CanHandle(CompositorType compositor)
    {
        return compositor is CompositorType.HYPRLAND;
    }

    public IMousePositionProvider Create()
    {
        return new HyprlandPositionProvider();
    }
}
