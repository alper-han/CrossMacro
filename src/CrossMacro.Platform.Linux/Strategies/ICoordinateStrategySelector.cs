using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.Strategies;

public interface ICoordinateStrategySelector
{
    /// <summary>
    /// Priority of this selector. Higher values are checked first.
    /// </summary>
    int Priority { get; }
    
    /// <summary>
    /// Determines if this selector can handle the given context.
    /// </summary>
    bool CanHandle(StrategyContext context);
    
    /// <summary>
    /// Creates the coordinate strategy for the given context.
    /// </summary>
    ICoordinateStrategy Create(StrategyContext context);
}
