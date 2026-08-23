
namespace CrossMacro.Platform.Linux.Strategies.Selectors;

public class ForceRelativeStrategySelector(IMousePositionProvider positionProvider) : ICoordinateStrategySelector
{
    private readonly IMousePositionProvider _positionProvider = positionProvider;
    public int Priority => 100;

    public bool CanHandle(StrategyContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.ForceRelative;
    }

    public ICoordinateStrategy Create(StrategyContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.UseLogicalRelativeCoordinates)
        {
            if (!_positionProvider.HasUsableAbsolutePosition())
            {
                throw new InvalidOperationException("Logical relative recording requires access to the global cursor position.");
            }

            if (context.IsWayland)
            {
                return new CompositorCoordinateStrategy(
                    _positionProvider,
                    emitRelativeCoordinates: true,
                    allowRawRelativeFallback: false);
            }

            if (context.Compositor is CompositorType.X11)
            {
                return new X11LogicalRelativeCoordinateStrategy(_positionProvider);
            }

            throw new InvalidOperationException(
                "Logical relative recording is not supported by the active input backend.");
        }

        return new RelativeCoordinateStrategy();
    }
}
