namespace CrossMacro.Infrastructure.Tests.Services.Playback;

public sealed class ImageClickMovementResolverTests
{
    [Fact]
    public async Task ResolveAsync_WhenAbsoluteInputIsAvailable_UsesTargetWithoutReadingCursor()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        var simulator = Substitute.For<IInputSimulator, IInputSimulatorCapabilities>();
        _ = ((IInputSimulatorCapabilities)simulator).SupportsAbsoluteCoordinates.Returns(returnThis: true);
        var resolver = new ImageClickMovementResolver(positionProvider);

        var result = await resolver.ResolveAsync(simulator, new ScreenPoint(420, 240), CancellationToken.None);

        _ = result.Should().Be(ImageClickMovementResolution.Absolute(new ScreenPoint(420, 240)));
        _ = positionProvider.DidNotReceive().GetAbsolutePositionAsync();
    }

    [Fact]
    public async Task ResolveAsync_WhenOnlyRelativeInputIsAvailable_UsesCurrentCursorDelta()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.IsSupported.Returns(returnThis: true);
        _ = positionProvider.GetAbsolutePositionAsync().Returns(Task.FromResult<(int X, int Y)?>((100, 75)));
        var simulator = Substitute.For<IInputSimulator, IInputSimulatorCapabilities>();
        _ = ((IInputSimulatorCapabilities)simulator).SupportsAbsoluteCoordinates.Returns(returnThis: false);
        var resolver = new ImageClickMovementResolver(positionProvider);

        var result = await resolver.ResolveAsync(simulator, new ScreenPoint(125, 50), CancellationToken.None);

        _ = result.Should().Be(ImageClickMovementResolution.Relative(25, -25));
    }

    [Fact]
    public async Task ResolveAsync_WhenRelativeInputHasNoCursorSample_FailsWithoutMovement()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.IsSupported.Returns(returnThis: true);
        _ = positionProvider.GetAbsolutePositionAsync().Returns(Task.FromResult<(int X, int Y)?>(null));
        var simulator = Substitute.For<IInputSimulator, IInputSimulatorCapabilities>();
        _ = ((IInputSimulatorCapabilities)simulator).SupportsAbsoluteCoordinates.Returns(returnThis: false);
        var resolver = new ImageClickMovementResolver(positionProvider);

        var result = await resolver.ResolveAsync(simulator, new ScreenPoint(125, 50), CancellationToken.None);

        _ = result.IsSuccess.Should().BeFalse();
        _ = result.ErrorMessage.Should().Be("The current mouse position is unavailable for relative movement.");
        simulator.DidNotReceive().MoveAbsolute(Arg.Any<int>(), Arg.Any<int>());
        simulator.DidNotReceive().MoveRelative(Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public async Task ResolveAsync_WhenRelativeDeltaOverflows_FailsWithoutMovement()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.IsSupported.Returns(returnThis: true);
        _ = positionProvider.GetAbsolutePositionAsync().Returns(Task.FromResult<(int X, int Y)?>((int.MinValue, 0)));
        var simulator = Substitute.For<IInputSimulator, IInputSimulatorCapabilities>();
        _ = ((IInputSimulatorCapabilities)simulator).SupportsAbsoluteCoordinates.Returns(returnThis: false);
        var resolver = new ImageClickMovementResolver(positionProvider);

        var result = await resolver.ResolveAsync(simulator, new ScreenPoint(int.MaxValue, 0), CancellationToken.None);

        _ = result.IsSuccess.Should().BeFalse();
        _ = result.ErrorMessage.Should().Be("The target and current mouse positions cannot be represented as a relative movement.");
        simulator.DidNotReceive().MoveRelative(Arg.Any<int>(), Arg.Any<int>());
    }
}
