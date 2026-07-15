
namespace CrossMacro.Platform.Linux.Services.Factories.Selectors;

public class WayfirePositionProviderSelector : IPositionProviderSelector
{
    public int Priority => 10;

    public bool CanHandle(CompositorType compositor)
    {
        return compositor is CompositorType.WAYFIRE;
    }

    public IMousePositionProvider Create()
    {
        return new WayfirePositionProvider();
    }
}
