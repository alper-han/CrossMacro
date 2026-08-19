namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class RunScriptMousePositionRuntimeTests
{
    [Fact]
    public async Task ExecuteStepAsync_WhenPositionIsAvailable_StoresSignedInvariantCoordinates()
    {
        var provider = SupportedProvider((-1920, 345));
        var executor = new RunScriptMousePositionExecutor(provider);
        var variables = Variables();

        await executor.ExecuteStepAsync("mouse position mouse_x mouse_y", 3, variables, CancellationToken.None);

        _ = variables.Should().Contain("mouse_x", "-1920");
        _ = variables.Should().Contain("mouse_y", "345");
        _ = await provider.Received(1).GetAbsolutePositionAsync();
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenProviderIsMissing_ThrowsMeaningfulError()
    {
        var executor = new RunScriptMousePositionExecutor(mousePositionProvider: null);

        var act = async () => await executor.ExecuteStepAsync("mouse position x y", 2, Variables(), CancellationToken.None);

        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Step 2: *IMousePositionProvider*");
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenProviderIsUnsupported_DoesNotReadPosition()
    {
        var provider = Substitute.For<IMousePositionProvider>();
        _ = provider.IsSupported.Returns(returnThis: false);
        var executor = new RunScriptMousePositionExecutor(provider);

        var act = async () => await executor.ExecuteStepAsync("mouse position x y", 4, Variables(), CancellationToken.None);

        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Step 4: The current mouse position is unavailable in this session.");
        _ = await provider.DidNotReceive().GetAbsolutePositionAsync();
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenProviderHasNoLivePosition_DoesNotReadPosition()
    {
        var provider = Substitute.For<IMousePositionProvider, IMousePositionAvailability>();
        _ = provider.IsSupported.Returns(returnThis: true);
        _ = provider.SupportsAbsolutePosition.Returns(returnThis: true);
        _ = ((IMousePositionAvailability)provider).IsPositionAvailable.Returns(returnThis: false);
        var executor = new RunScriptMousePositionExecutor(provider);

        var act = async () => await executor.ExecuteStepAsync("mouse position x y", 4, Variables(), CancellationToken.None);

        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Step 4: The current mouse position is unavailable in this session.");
        _ = await provider.DidNotReceive().GetAbsolutePositionAsync();
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenProviderReturnsNoSample_ThrowsMeaningfulError()
    {
        var provider = SupportedProvider(position: null);
        var executor = new RunScriptMousePositionExecutor(provider);

        var act = async () => await executor.ExecuteStepAsync("mouse position x y", 5, Variables(), CancellationToken.None);

        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Step 5: The current mouse position is unavailable in this session.");
    }

    [Theory]
    [InlineData("mouse")]
    [InlineData("mouse position x")]
    [InlineData("mouse position 1x y")]
    [InlineData("mouse position x x")]
    public void Validate_WhenSyntaxIsInvalid_ReturnsMeaningfulError(string step)
    {
        var error = RunScriptMousePositionExecutor.Validate(step);

        _ = error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PlayAsync_WhenMousePositionOnlyScript_RunsWithoutInputSimulator()
    {
        var keyCodeMapper = Substitute.For<IKeyCodeMapper>();
        _ = keyCodeMapper.GetKeyCode(Arg.Any<string>()).Returns(-1);
        _ = keyCodeMapper.IsModifierKeyCode(Arg.Any<int>()).Returns(returnThis: false);
        _ = keyCodeMapper.GetKeyCodeForCharacter(Arg.Any<char>()).Returns(-1);
        var provider = SupportedProvider((12, -34));
        using var player = new MacroPlayer(
            new PlaybackValidator(keyCodeMapper, provider),
            CreateDependencies(provider, keyCodeMapper));
        var macro = new MacroSequence
        {
            ScriptSteps = { "mouse position x y" },
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        var variables = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables;
        _ = variables.Should().Contain("x", "12");
        _ = variables.Should().Contain("y", "-34");
    }

    private static Dictionary<string, string> Variables() => new(StringComparer.OrdinalIgnoreCase);

    private static IMousePositionProvider SupportedProvider((int X, int Y)? position)
    {
        var provider = Substitute.For<IMousePositionProvider>();
        _ = provider.ProviderName.Returns("test-position");
        _ = provider.IsSupported.Returns(returnThis: true);
        _ = provider.SupportsAbsolutePosition.Returns(returnThis: true);
        _ = provider.GetAbsolutePositionAsync().Returns(Task.FromResult(position));
        return provider;
    }

    private static MacroPlayerDependencies CreateDependencies(
        IMousePositionProvider positionProvider,
        IKeyCodeMapper keyCodeMapper)
    {
        return new MacroPlayerDependencies(
            positionProvider,
            new SystemPlaybackTimingService(),
            (_, _) => Task.CompletedTask,
            CreateElapsedMillisecondsProvider,
            () => new DefaultPlaybackCoordinator(positionProvider),
            () => new ButtonStateTracker(),
            () => new KeyStateTracker(),
            new DefaultPlaybackMouseButtonMapper(),
            inputSimulatorFactory: () => throw new InvalidOperationException("simulator should not be acquired"),
            simulatorPool: null,
            NullScreenPixelReader.Instance,
            keyCodeMapper,
            new NullWindowManager(),
            clipboardService: null,
            shellCommandRunner: null,
            screenshotCaptureService: null,
            new ImageClickMovementResolver(positionProvider),
            new ImageAssetCodec(),
            new PlaybackDelayResolver());
    }

    private static Func<double> CreateElapsedMillisecondsProvider()
    {
        var stopwatch = Stopwatch.StartNew();
        return () => stopwatch.Elapsed.TotalMilliseconds;
    }
}
