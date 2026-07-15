
namespace CrossMacro.Platform.Linux.Strategies;

public interface ICoordinateStrategySelector
{
    /// <summary>
    /// Priority of this selector. Higher values are checked first.
    /// </summary>
    public int Priority { get; }

    /// <summary>
    /// Determines if this selector can handle the given context.
    /// </summary>
    public bool CanHandle(StrategyContext context);

    /// <summary>
    /// Creates the coordinate strategy for the given context.
    /// </summary>
    public ICoordinateStrategy Create(StrategyContext context);
}
