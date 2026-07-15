
namespace CrossMacro.Platform.Linux.Services.Factories.Selectors;

public sealed class NiriPositionProviderSelector : IPositionProviderSelector
{
    public int Priority => 10;

    public bool CanHandle(CompositorType compositor)
    {
        return compositor is CompositorType.NIRI;
    }

    public IMousePositionProvider Create()
    {
        return new NiriPositionProvider();
    }
}
