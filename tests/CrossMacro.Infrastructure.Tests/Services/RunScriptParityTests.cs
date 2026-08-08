// Parity contract: the same script must produce identical observable outcomes through
// compile-time expansion (RunScriptCompiler) and the runtime executor (RunScriptRuntimeExecutor,
// forced by a leading screen-reading step).
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class RunScriptParityTests
{
    [Fact]
    public async Task PlayAsync_WhenRuntimeSetStoresArithmetic_RepeatObservesEvaluatedCount()
    {
        // Regression pin: runtime `set` used to store the raw text "5+3" while compile-time
        // expansion evaluated it to "8", so `repeat $x {` diverged between the two paths.
        var activity = new List<string>();
        using var player = CreatePlayer(activity);
        var macro = new MacroSequence
        {
            ScriptSteps =
            {
                "pixelcolor 1 2 sampled",
                "set x 5+3",
                "repeat $x {",
                "click left",
                "}",
            },
        };

        await player.PlayAsync(macro, cancellationToken: CancellationToken.None);

        _ = activity.Should().HaveCount(9);
        _ = activity[0].Should().Be("screen:pixelcolor:1,2");
        _ = activity.Skip(1).Should().OnlyContain(entry => entry == "input:click:left");
        var variables = ((IRunScriptRuntimeVariableSource)player).RuntimeVariables;
        _ = variables.Should().Contain("x", "8");
    }

    [Theory]
    [MemberData(nameof(ParityScripts))]
    public async Task Script_ProducesIdenticalOutcome_InCompileExpansionAndRuntimeExecution(IReadOnlyList<string> scriptSteps)
    {
        ArgumentNullException.ThrowIfNull(scriptSteps);

        // Path A: compile-time expansion, then the expanded events play through the same
        // recording input simulator.
        var compileActivity = new List<string>();
        var compiler = new RunScriptCompiler(CreateKeyCodeMapper());
        var compileResult = compiler.Compile(scriptSteps.Select(step => new RunScriptStep(step)).ToList());
        _ = compileResult.Success.Should().BeTrue($"compile expansion should succeed: {compileResult.ErrorMessage}");
        using (var compilePlayer = CreatePlayer(compileActivity))
        {
            await compilePlayer.PlayAsync(compileResult.Sequence!, cancellationToken: CancellationToken.None);
        }

        // Path B: a leading screen-reading step forces the same script down the runtime executor.
        var runtimeActivity = new List<string>();
        using (var runtimePlayer = CreatePlayer(runtimeActivity))
        {
            var runtimeMacro = new MacroSequence();
            runtimeMacro.ScriptSteps.Add("pixelcolor 1 2 sampled");
            foreach (var step in scriptSteps)
            {
                runtimeMacro.ScriptSteps.Add(step);
            }

            await runtimePlayer.PlayAsync(runtimeMacro, cancellationToken: CancellationToken.None);
        }

        var runtimeInputActivity = runtimeActivity
            .Where(entry => !entry.StartsWith("screen:", StringComparison.Ordinal))
            .ToList();
        _ = runtimeInputActivity.Should().Equal(compileActivity);
    }

    [Theory]
    [MemberData(nameof(MalformedScripts))]
    public async Task Script_WhenMalformed_RejectedByBothInterpreters(
        IReadOnlyList<string> scriptSteps,
        string compileMessagePart,
        string runtimeMessagePart)
    {
        ArgumentNullException.ThrowIfNull(scriptSteps);
        ArgumentNullException.ThrowIfNull(compileMessagePart);
        ArgumentNullException.ThrowIfNull(runtimeMessagePart);

        var compiler = new RunScriptCompiler(CreateKeyCodeMapper());
        var compileResult = compiler.Compile(scriptSteps.Select(step => new RunScriptStep(step)).ToList());
        _ = compileResult.Success.Should().BeFalse("malformed scripts must be rejected by compile expansion");
        _ = compileResult.ErrorMessage.Should().Contain(compileMessagePart);

        using var player = CreatePlayer([]);
        var macro = new MacroSequence();
        macro.ScriptSteps.Add("pixelcolor 1 2 sampled");
        foreach (var step in scriptSteps)
        {
            macro.ScriptSteps.Add(step);
        }

        var act = async () => await player.PlayAsync(macro, cancellationToken: CancellationToken.None);
        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{runtimeMessagePart}*");
    }

    public static IEnumerable<object[]> ParityScripts()
    {
        yield return new object[]
        {
            new List<string> { "set x 5+3", "set y $x * 2", "move rel $x $y", "click left" },
        };
        yield return new object[]
        {
            new List<string> { "set name $$foo", "if $name == $$foo {", "click left", "}" },
        };
        yield return new object[]
        {
            new List<string> { "set x 10", "inc x", "inc x 3", "set y 2", "dec x $y", "move rel $x $y" },
        };
        yield return new object[]
        {
            new List<string> { "set x 5", "mul x 2", "set y 4", "div x $y", "move rel $x 0" },
        };
        yield return new object[]
        {
            new List<string> { "set x 3", "mul x", "div x", "move rel $x 0" },
        };
        yield return new object[]
        {
            new List<string> { "set n 8", "while $n > 1 {", "div n 2", "}", "move rel $n 0", "click left" },
        };
        yield return new object[]
        {
            new List<string> { "repeat 3 {", "move rel 1 1", "}" },
        };
        yield return new object[]
        {
            new List<string> { "set x 5+3", "repeat $x {", "click left", "}" },
        };
        yield return new object[]
        {
            new List<string> { "for i from 1 to 6 step 2 {", "move rel $i 0", "}" },
        };
        yield return new object[]
        {
            new List<string> { "for i from 3 to 1 {", "move rel $i 0", "}" },
        };
        yield return new object[]
        {
            new List<string> { "set x 2", "if $x == 1 {", "move rel 1 0", "}", "else {", "move rel 2 0", "}" },
        };
        yield return new object[]
        {
            new List<string> { "set n 3", "while $n > 0 {", "click left", "dec n", "}" },
        };
        yield return new object[]
        {
            new List<string> { "set x 5", "set limit 10", "if $x < $limit {", "move rel 1 1", "}" },
        };
        yield return new object[]
        {
            new List<string> { "set a 7 % 3", "set s 10 - 4", "set d 10 / 3", "move rel $a $s", "move rel $d 0" },
        };
        yield return new object[]
        {
            // Binary arithmetic in the repeat count: a = 10, so the body runs 5 times.
            new List<string> { "set a 10", "repeat $a / 2 {", "click left", "}" },
        };
        yield return new object[]
        {
            // Regression pin for the closed divergence window: spaceless `repeat 5+3 {`
            // used to evaluate in the runtime executor but fail compile-time expansion.
            new List<string> { "repeat 5+3 {", "click left", "}" },
        };
        yield return new object[]
        {
            new List<string> { "set start 1", "set n 3", "for i from $start to $n * 2 {", "move rel $i 0", "}" },
        };
        yield return new object[]
        {
            new List<string> { "set s 1", "for i from 1 to 10 step $s + 1 {", "move rel $i 0", "}" },
        };
        yield return new object[]
        {
            // Keyword-shaped variable names stay operands thanks to the $ sigil.
            new List<string> { "set from 2", "set to 3", "for i from $from to $to {", "move rel $i 0", "}" },
        };
        yield return new object[]
        {
            new List<string> { "set x 5", "if $x + 1 > 5 {", "click left", "}" },
        };
        yield return new object[]
        {
            new List<string> { "set n 3", "while $n - 1 > 0 {", "click left", "dec n", "}" },
        };
        yield return new object[]
        {
            // Text operands containing spaces keep the ==/!= path with no arithmetic.
            new List<string> { "set msg hello world", "if $msg == hello world {", "click left", "}" },
        };
        yield return new object[]
        {
            new List<string> { "set c FF0000", "if $c == FF0000 {", "click left", "}" },
        };
        yield return new object[]
        {
            new List<string> { "set flag true", "if $flag == true {", "click left", "}" },
        };
        yield return new object[]
        {
            new List<string>
            {
                "set counter 0",
                "set total 2 * 3",
                "repeat $total {",
                "inc counter",
                "}",
                "for i from $counter to 6 {",
                "move rel $i 0",
                "}",
                "if $counter == 6 {",
                "click left",
                "}",
                "while $counter > 4 {",
                "dec counter",
                "}",
                "move rel $counter $total",
            },
        };
    }

    public static IEnumerable<object[]> MalformedScripts()
    {
        yield return new object[]
        {
            new List<string> { "repeat abc {", "click left", "}" },
            "Invalid repeat count 'abc'. Expected integer.",
            "Repeat count must be an integer >= 0.",
        };
        yield return new object[]
        {
            new List<string> { "repeat -1 {", "click left", "}" },
            "repeat count must be >= 0.",
            "Repeat count must be an integer >= 0.",
        };
        yield return new object[]
        {
            new List<string> { "for i from 1 to 3 step 0 {", "click left", "}" },
            "for step cannot be 0.",
            "For step cannot be 0.",
        };
        yield return new object[]
        {
            new List<string> { "for i 1 to 3 {", "click left", "}" },
            "Invalid for syntax. Expected: for <var> from <start> to <end> [step <n>] {",
            "Invalid for syntax. Expected: for <var> from <start> to <end> [step <n>] {",
        };
        yield return new object[]
        {
            new List<string> { "for i from abc to 3 {", "click left", "}" },
            "Invalid for start 'abc'. Expected integer.",
            "Invalid for start 'abc'. Expected integer.",
        };
        yield return new object[]
        {
            new List<string> { "for 1bad from 1 to 3 {", "click left", "}" },
            "Invalid loop variable name '1bad'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*",
            "Invalid loop variable name '1bad'. Allowed pattern: [A-Za-z_][A-Za-z0-9_]*",
        };
        yield return new object[]
        {
            new List<string> { "repeat 5{", "click left", "}" },
            "unsupported block syntax",
            "unsupported block syntax",
        };
        yield return new object[]
        {
            new List<string> { "set x 5", "div x 0", "click left" },
            "Division by zero is not allowed in mul/div.",
            "Division by zero is not allowed in mul/div.",
        };
        yield return new object[]
        {
            new List<string> { "mul missing 2", "click left" },
            "variable 'missing' must exist and be an integer for mul/div.",
            "variable 'missing' must exist and be an integer for mul/div.",
        };
        yield return new object[]
        {
            new List<string> { "set name foo", "div name 2", "click left" },
            "variable 'name' must exist and be an integer for mul/div.",
            "variable 'name' must exist and be an integer for mul/div.",
        };
        yield return new object[]
        {
            new List<string> { "set x 5", "mul x abc", "click left" },
            "Invalid mul/div amount 'abc'. Expected integer.",
            "Invalid mul/div amount 'abc'. Expected integer.",
        };
        yield return new object[]
        {
            new List<string> { "set x 5", "div x $missing", "click left" },
            "Unknown variable '$missing'.",
            "Unknown variable '$missing'.",
        };
        yield return new object[]
        {
            // Mirrors inc/dec: playback validation routes the 3-token form through the
            // static single-step emit path, which reports it as an unsupported step.
            new List<string> { "set x 5", "mul x 1 1", "click left" },
            "Invalid mul syntax. Expected: mul <name> [amount].",
            "unsupported step syntax 'mul x 1 1'",
        };
        yield return new object[]
        {
            new List<string> { "set x 2000000000", "mul x 3", "click left" },
            "Result is out of range for mul/div.",
            "Result is out of range for mul/div.",
        };
        yield return new object[]
        {
            // Dangling operator in a block argument: loud, canonical, context-labeled.
            new List<string> { "repeat $a / {", "click left", "}" },
            "'$a /' is not a valid numeric expression for repeat count.",
            "'$a /' is not a valid numeric expression for repeat count.",
        };
        yield return new object[]
        {
            // Chained operators stay out of scope and fail loudly on both paths.
            new List<string> { "repeat 1 + 2 + 3 {", "click left", "}" },
            "is not a valid numeric expression for repeat count.",
            "is not a valid numeric expression for repeat count.",
        };
        yield return new object[]
        {
            // A negative expression result keeps the historical per-interpreter wording.
            new List<string> { "repeat 2 - 5 {", "click left", "}" },
            "repeat count must be >= 0.",
            "Repeat count must be an integer >= 0.",
        };
        yield return new object[]
        {
            new List<string> { "repeat $missing + 1 {", "click left", "}" },
            "Unknown variable '$missing'.",
            "Unknown variable '$missing'.",
        };
        yield return new object[]
        {
            new List<string> { "repeat 4 / 0 {", "click left", "}" },
            "Division by zero is not allowed in repeat count.",
            "Division by zero is not allowed in repeat count.",
        };
        yield return new object[]
        {
            new List<string> { "for i from 1 to $n * 2 {", "click left", "}" },
            "Unknown variable '$n'.",
            "Unknown variable '$n'.",
        };
        yield return new object[]
        {
            // The step expression evaluates to 0; the zero-step guards stay verbatim.
            new List<string> { "set a 1", "for i from $a to 3 step $a - 1 {", "click left", "}" },
            "for step cannot be 0.",
            "For step cannot be 0.",
        };
        yield return new object[]
        {
            new List<string> { "for i from 1 to 10 step $s + {", "click left", "}" },
            "is not a valid numeric expression for for step.",
            "is not a valid numeric expression for for step.",
        };
        yield return new object[]
        {
            // Chained arithmetic in a condition operand fails loudly and identically.
            new List<string> { "set x 5", "if 2 + 2 + 2 > 3 {", "click left", "}" },
            "Operator '>' requires numeric operands. Got '2 + 2 + 2' and '3'.",
            "Operator '>' requires numeric operands. Got '2 + 2 + 2' and '3'.",
        };
        yield return new object[]
        {
            new List<string> { "set x 5", "if $x + 1 > $missing {", "click left", "}" },
            "Unknown variable '$missing'.",
            "Unknown variable '$missing'.",
        };
        yield return new object[]
        {
            new List<string> { "set n 5", "while $n - $missing > 0 {", "click left", "}" },
            "Unknown variable '$missing'.",
            "Unknown variable '$missing'.",
        };
    }

    private static MacroPlayer CreatePlayer(List<string> activity)
    {
        var keyCodeMapper = CreateKeyCodeMapper();
        var positionProvider = CreatePositionProvider();
        return new MacroPlayer(
            new PlaybackValidator(keyCodeMapper, positionProvider),
            CreateDependencies(positionProvider, keyCodeMapper, activity));
    }

    private static MacroPlayerDependencies CreateDependencies(
        IMousePositionProvider positionProvider,
        IKeyCodeMapper keyCodeMapper,
        List<string> activity)
    {
        return new MacroPlayerDependencies(positionProvider, new PlaybackTimingService(), (_, _) => Task.CompletedTask,
            CreateElapsedMillisecondsProvider, () => new DefaultPlaybackCoordinator(positionProvider), () => new ButtonStateTracker(),
            () => new KeyStateTracker(), new DefaultPlaybackMouseButtonMapper(), () => new RecordingInputSimulator(activity), simulatorPool: null,
            new RecordingScreenPixelReader(activity), keyCodeMapper, new NullWindowManager(), clipboardService: null, shellCommandRunner: null,
            screenshotCaptureService: null, new ImageClickMovementResolver(positionProvider), new ImageAssetCodec(), new PlaybackDelayResolver());
    }

    private static Func<double> CreateElapsedMillisecondsProvider()
    {
        var stopwatch = Stopwatch.StartNew();
        return () => stopwatch.Elapsed.TotalMilliseconds;
    }

    private static IKeyCodeMapper CreateKeyCodeMapper()
    {
        var keyCodeMapper = Substitute.For<IKeyCodeMapper>();
        _ = keyCodeMapper.GetKeyCode(Arg.Any<string>()).Returns(-1);
        _ = keyCodeMapper.IsModifierKeyCode(Arg.Any<int>()).Returns(returnThis: false);
        _ = keyCodeMapper.GetKeyCodeForCharacter(Arg.Any<char>()).Returns(-1);
        return keyCodeMapper;
    }

    private static IMousePositionProvider CreatePositionProvider()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.ProviderName.Returns("fake-position");
        _ = positionProvider.IsSupported.Returns(returnThis: true);
        _ = positionProvider.GetAbsolutePositionAsync().Returns(Task.FromResult<(int X, int Y)?>((0, 0)));
        _ = positionProvider.GetScreenResolutionAsync().Returns(Task.FromResult<(int Width, int Height)?>((1920, 1080)));
        return positionProvider;
    }

    private sealed class RecordingScreenPixelReader(List<string> activity) : IScreenPixelReader
    {
        private readonly List<string> _activity = activity;

        public string ProviderName => "recording-screen-reader";

        public bool IsSupported => true;

        public Task<ScreenReadResult<ScreenPixelColor>> GetPixelAsync(ScreenPoint point, ScreenReadOptions options)
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            _activity.Add(string.Concat(
                "screen:pixelcolor:",
                point.X.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ",",
                point.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            return Task.FromResult(ScreenReadResultFactory.Success<ScreenPixelColor>(new ScreenPixelColor(0x12, 0x34, 0x56)));
        }

        public Task<ScreenReadResult<ScreenPixelColor>> WaitForPixelAsync(
            ScreenPoint point,
            ScreenPixelColor expected,
            ScreenReadOptions options)
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            _activity.Add(string.Concat(
                "screen:waitcolor:",
                point.X.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ",",
                point.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            return Task.FromResult(ScreenReadResultFactory.Success<ScreenPixelColor>(expected));
        }

        public Task<ScreenReadResult<ScreenPixelSearchMatch>> SearchPixelAsync(
            ScreenRect region,
            ScreenPixelColor expected,
            int tolerance,
            ScreenReadOptions options)
        {
            options.CancellationToken.ThrowIfCancellationRequested();
            _activity.Add(string.Concat(
                "screen:pixelsearch:",
                region.X.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ",",
                region.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            return Task.FromResult(ScreenReadResultFactory.Success<ScreenPixelSearchMatch>(new ScreenPixelSearchMatch(new ScreenPoint(region.X, region.Y), expected)));
        }

        public void Dispose()
        {
        }
    }

    private sealed class RecordingInputSimulator(List<string> activity) : IInputSimulator, IInputSimulatorCapabilities
    {
        private readonly List<string> _activity = activity;

        public string ProviderName => "recording-input-simulator";

        public bool IsSupported => true;

        public bool SupportsAbsoluteCoordinates { get; init; } = true;

        public void Initialize(int screenWidth = 0, int screenHeight = 0)
        {
        }

        public Task InitializeAsync(int screenWidth = 0, int screenHeight = 0, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public void MoveAbsolute(int x, int y)
        {
            _activity.Add(string.Concat(
                "input:move-abs:",
                x.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ",",
                y.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        public void MoveRelative(int dx, int dy)
        {
            _activity.Add(string.Concat(
                "input:move:",
                dx.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ",",
                dy.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        public void MouseButton(int button, bool pressed)
        {
            if (pressed)
            {
                var name = button switch
                {
                    MouseButtonCode.Right => "right",
                    MouseButtonCode.Middle => "middle",
                    _ => "left",
                };
                _activity.Add($"input:click:{name}");
            }
        }

        public void Scroll(int delta, bool isHorizontal = false)
        {
        }

        public void KeyPress(int keyCode, bool pressed)
        {
            _activity.Add(string.Concat(
                "input:key:",
                keyCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ":",
                pressed ? "down" : "up"));
        }

        public void Sync()
        {
        }

        public void Dispose()
        {
        }
    }
}
