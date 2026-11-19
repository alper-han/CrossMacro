using System.Globalization;

namespace CrossMacro.Infrastructure.Tests.Services.Playback;

/// <summary>
/// A <c>$$</c> in a script is a literal dollar sign. It must mean the same thing whether the script is
/// expanded ahead of time or executed step by step, and variable values that happen to contain a dollar
/// sign must never be substituted a second time.
/// </summary>
public sealed class RunScriptEscapeParityTests
{
    public static TheoryData<string[]> TypedScripts => new()
    {
        { ["type cost $$5"] },
        { ["type $$"] },
        { ["set x 7", "type $$x"] },
        { ["set x 7", "type $$$x"] },
        { ["set x 7", "type $x$$"] },
        { ["set price $$5", "type $price"] },
        { ["set name $$x", "set x 9", "type $name"] },
    };

    [Theory]
    [MemberData(nameof(TypedScripts))]
    public async Task Type_ProducesTheSameKeysWhenExpandedAndWhenExecuted(string[] steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        var compiled = new RunScriptCompiler(CreateMapper()).Compile([.. steps.Select(step => new RunScriptStep(step))]);
        _ = compiled.Success.Should().BeTrue(compiled.ErrorMessage);
        var staticActivity = new List<string>();
        using (var player = CreatePlayer(staticActivity))
        {
            await player.PlayAsync(compiled.Sequence!, cancellationToken: TestContext.Current.CancellationToken);
        }

        var runtimeActivity = new List<string>();
        using (var player = CreatePlayer(runtimeActivity))
        {
            var macro = new MacroSequence();
            macro.ScriptSteps.Add("pixelcolor 1 2 sampled");
            foreach (var step in steps)
            {
                macro.ScriptSteps.Add(step);
            }

            await player.PlayAsync(macro, cancellationToken: TestContext.Current.CancellationToken);
        }

        var runtimeKeys = runtimeActivity.Where(entry => entry.StartsWith("input:key:", StringComparison.Ordinal)).ToList();
        _ = staticActivity.Should().NotBeEmpty();
        _ = runtimeKeys.Should().Equal(staticActivity.Where(entry => entry.StartsWith("input:key:", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Type_WhenTextEscapesADollar_TypesTheLiteralDollar()
    {
        var activity = new List<string>();
        using var player = CreatePlayer(activity);
        var macro = new MacroSequence();
        macro.ScriptSteps.Add("pixelcolor 1 2 sampled");
        macro.ScriptSteps.Add("set x 7");
        macro.ScriptSteps.Add("type $$x=$x");

        await player.PlayAsync(macro, cancellationToken: TestContext.Current.CancellationToken);

        var typed = activity
            .Where(entry => entry.EndsWith(":down", StringComparison.Ordinal) && entry.StartsWith("input:key:", StringComparison.Ordinal))
            .Select(entry => (char)int.Parse(entry.Split(':')[2], CultureInfo.InvariantCulture))
            .ToArray();
        _ = new string(typed).Should().Be("$x=7");
    }

    [Fact]
    public async Task Clipboard_WhenTextEscapesADollar_SetsTheLiteralDollar()
    {
        var clipboard = Substitute.For<IClipboardService>();
        _ = clipboard.IsSupported.Returns(returnThis: true);
        string? set = null;
        _ = clipboard.SetTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            set = call.ArgAt<string>(0);
            return Task.CompletedTask;
        });
        using var player = CreatePlayer([], clipboard: clipboard);
        var macro = new MacroSequence();
        macro.ScriptSteps.Add("set x 7");
        macro.ScriptSteps.Add("clipboard set cost $$5 x=$x");

        await player.PlayAsync(macro, cancellationToken: TestContext.Current.CancellationToken);

        _ = set.Should().Be("cost $5 x=7");
    }

    [Fact]
    public async Task Shell_WhenCommandEscapesADollar_RunsWithTheLiteralDollar()
    {
        var runner = Substitute.For<IShellCommandRunner>();
        ShellCommandRequest? request = null;
        _ = runner.RunAsync(Arg.Any<ShellCommandRequest>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            request = call.Arg<ShellCommandRequest>();
            return Task.FromResult(new ShellCommandResult(ExitCode: 0, StandardOutput: string.Empty, StandardError: string.Empty));
        });
        using var player = CreatePlayer([], shellRunner: runner);
        var macro = new MacroSequence();
        macro.ScriptSteps.Add("set x 7");
        macro.ScriptSteps.Add("shell \"echo $$HOME $x\"");

        await player.PlayAsync(macro, cancellationToken: TestContext.Current.CancellationToken);

        _ = request.Should().NotBeNull();
        _ = request.Command.Should().Be("echo $HOME 7");
    }

    private static IKeyCodeMapper CreateMapper()
    {
        var mapper = Substitute.For<IKeyCodeMapper>();
        _ = mapper.GetKeyCode(Arg.Any<string>()).Returns(-1);
        _ = mapper.IsModifierKeyCode(Arg.Any<int>()).Returns(returnThis: false);
        _ = mapper.GetKeyCodeForCharacter(Arg.Any<char>()).Returns(call => (int)call.Arg<char>());
        _ = mapper.RequiresShift(Arg.Any<char>()).Returns(returnThis: false);
        _ = mapper.RequiresAltGr(Arg.Any<char>()).Returns(returnThis: false);
        return mapper;
    }

    private static MacroPlayer CreatePlayer(List<string> activity, IClipboardService? clipboard = null, IShellCommandRunner? shellRunner = null)
    {
        var mapper = CreateMapper();
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.ProviderName.Returns("fake-position");
        _ = positionProvider.IsSupported.Returns(returnThis: true);
        _ = positionProvider.GetAbsolutePositionAsync().Returns(Task.FromResult<(int X, int Y)?>((0, 0)));
        _ = positionProvider.GetScreenResolutionAsync().Returns(Task.FromResult<(int Width, int Height)?>((1920, 1080)));
        return new MacroPlayer(
            new PlaybackValidator(mapper, positionProvider),
            MacroPlayerTestFactory.CreateDependencies(
                positionProvider,
                mapper,
                () => new ActivityRecordingInputSimulator(activity),
                screenPixelReader: new ActivityRecordingScreenPixelReader(activity),
                clipboardService: clipboard,
                shellCommandRunner: shellRunner));
    }
}
