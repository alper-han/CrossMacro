
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class RunScriptClipboardRuntimeTests
{
    [Fact]
    public async Task ExecuteStepAsync_WhenSetPayloadContainsSpaces_PreservesRawPayload()
    {
        var clipboard = SupportedClipboard();
        var executor = new RunScriptClipboardExecutor(clipboard);
        var variables = Vars();

        await executor.ExecuteStepAsync("clipboard set \"hello   spaced   world\"", 1, variables, CancellationToken.None);

        await clipboard.Received(1).SetTextAsync("hello   spaced   world", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenSetPayloadEscapesDollar_PreservesLiteralDollar()
    {
        var clipboard = SupportedClipboard();
        var executor = new RunScriptClipboardExecutor(clipboard);

        await executor.ExecuteStepAsync("clipboard set literal $$clipText", 1, Vars(), CancellationToken.None);

        await clipboard.Received(1).SetTextAsync("literal $clipText", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenGetDestinationUsesDollar_DoesNotResolveDestinationAsVariable()
    {
        var clipboard = SupportedClipboard();
        _ = clipboard.GetTextAsync(Arg.Any<CancellationToken>()).Returns("clipboard text");
        var executor = new RunScriptClipboardExecutor(clipboard);
        var variables = Vars();

        await executor.ExecuteStepAsync("clipboard get $dest", 1, variables, CancellationToken.None);

        _ = variables.Should().Contain("dest", "clipboard text");
    }

    [Theory]
    [InlineData("clipboard capture ctrl+c destination")]
    [InlineData("clipboard capture CTRL+SHIFT+C $destination")]
    public async Task ExecuteStepAsync_WhenCaptureReadsClipboard_StoresTextInDestinationVariable(string step)
    {
        var clipboard = SupportedClipboard();
        _ = clipboard.GetTextAsync(Arg.Any<CancellationToken>()).Returns("captured text");
        var variables = Vars();
        var executor = new RunScriptClipboardExecutor(clipboard);

        await executor.ExecuteStepAsync(step, 1, variables, CancellationToken.None);

        _ = variables.Should().Contain("destination", "captured text");
    }

    [Theory]
    [InlineData("clipboard capture ctrl+v destination")]
    [InlineData("clipboard capture ctrl+c 1destination")]
    [InlineData("clipboard capture ctrl+c destination extra")]
    public void Validate_WhenCaptureSyntaxIsInvalid_ReturnsSyntaxError(string step)
    {
        var result = RunScriptClipboardExecutor.Validate(step);

        _ = result.Should().Contain("Syntax: clipboard capture <ctrl+c|ctrl+shift+c> <var>");
    }

    [Fact]
    public void Validate_WhenClipboardCommandIsMissingSubcommand_ListsCaptureSyntax()
    {
        var result = RunScriptClipboardExecutor.Validate("clipboard");

        _ = result.Should().Be("Syntax: clipboard get <var> | clipboard set <text> | clipboard capture <ctrl+c|ctrl+shift+c> <var>");
    }

    [Fact]
    public void Validate_WhenGetDestinationIsInvalid_ReturnsVariableNameError()
    {
        var result = RunScriptClipboardExecutor.Validate("clipboard get 1bad");

        _ = result.Should().Contain("Invalid variable name");
    }

    [Fact]
    public void Validate_WhenGetDestinationHasExtraTokens_ReturnsSyntaxError()
    {
        var result = RunScriptClipboardExecutor.Validate("clipboard get dest extra");

        _ = result.Should().Contain("Syntax: clipboard get <var>");
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenClipboardServiceIsUnsupported_ThrowsMeaningfulError()
    {
        var clipboard = Substitute.For<IClipboardService>();
        _ = clipboard.IsSupported.Returns(returnThis: false);
        var executor = new RunScriptClipboardExecutor(clipboard);

        var act = async () => await executor.ExecuteStepAsync("clipboard set value", 1, Vars(), CancellationToken.None);

        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Step 1: Clipboard script steps require a supported IClipboardService runtime service.");
        await clipboard.DidNotReceive().SetTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenSetTextAsyncThrows_ThrowsWithStepContext()
    {
        var clipboard = SupportedClipboard();
        var failure = new InvalidOperationException("backend failed");
        _ = clipboard.SetTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(failure));
        var executor = new RunScriptClipboardExecutor(clipboard);

        var act = async () => await executor.ExecuteStepAsync("clipboard set value", 7, Vars(), CancellationToken.None);

        var exception = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Step 7: Failed to set clipboard text.");
        _ = exception.Which.InnerException.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenSetTextAsyncIsCanceled_PropagatesCancellation()
    {
        var clipboard = SupportedClipboard();
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        _ = clipboard.SetTextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromCanceled(cts.Token));
        var executor = new RunScriptClipboardExecutor(clipboard);

        var act = async () => await executor.ExecuteStepAsync("clipboard set value", 1, Vars(), cts.Token);

        _ = await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenClipboardServiceIsMissing_ThrowsMeaningfulError()
    {
        var executor = new RunScriptClipboardExecutor(clipboardService: null);

        var act = async () => await executor.ExecuteStepAsync("clipboard set value", 1, Vars(), CancellationToken.None);

        _ = await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*IClipboardService*");
    }

    private static Dictionary<string, string> Vars() =>
        new(StringComparer.OrdinalIgnoreCase);

    private static IClipboardService SupportedClipboard()
    {
        var clipboard = Substitute.For<IClipboardService>();
        _ = clipboard.IsSupported.Returns(returnThis: true);
        return clipboard;
    }
}
