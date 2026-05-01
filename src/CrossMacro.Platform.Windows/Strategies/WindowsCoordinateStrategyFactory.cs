using System;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services.Recording.Strategies;
using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Windows.Strategies;

public class WindowsCoordinateStrategyFactory : ICoordinateStrategyFactory
{
    private readonly IMousePositionProvider _positionProvider;
    private readonly Func<IInputSimulator>? _inputSimulatorFactory;

    public WindowsCoordinateStrategyFactory(
        IMousePositionProvider positionProvider,
        Func<IInputSimulator>? inputSimulatorFactory = null)
    {
        _positionProvider = positionProvider;
        _inputSimulatorFactory = inputSimulatorFactory;
    }

    public ICoordinateStrategy Create(bool useAbsoluteCoordinates, bool forceRelative, bool skipInitialZero)
    {
        if (forceRelative)
        {
            return new RelativeCoordinateStrategy();
        }

        if (useAbsoluteCoordinates)
        {
            return new WindowsAbsoluteCoordinateStrategy(_positionProvider);
        }
        else
        {
            return new RelativeCoordinateStrategy();
        }
    }
}
