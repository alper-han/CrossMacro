
namespace CrossMacro.Platform.Linux.Services.Factories.Selectors;

public sealed class CosmicPositionProviderSelector : IPositionProviderSelector
{
    public int Priority => 10;

    public bool CanHandle(CompositorType compositor)
    {
        return compositor is CompositorType.COSMIC;
    }

    public IMousePositionProvider Create()
    {
        return new CosmicPositionProvider();
    }
}
