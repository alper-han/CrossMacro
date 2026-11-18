namespace CrossMacro.Core.Tests.Services;

public sealed class RunScriptInputSyntaxTests
{
    [Theory]
    [InlineData("click left")]
    [InlineData("delay")]
    [InlineData("move rel 1 2 3")]
    [InlineData("key down")]
    [InlineData("scroll")]
    [InlineData("tap")]
    [InlineData("type")]
    public void Parsers_IgnoreStepsThatAreNotTheirCommand(string step)
    {
        _ = RunScriptInputSyntax.TryParseDelay(step, out _, out _, out _, out _, out var delayError).Should().BeFalse();
        _ = delayError.Should().BeNull();
        _ = RunScriptInputSyntax.TryParseMove(step, out _, out _, out _, out _, out var moveError).Should().BeFalse();
        _ = moveError.Should().BeNull();
    }

    [Theory]
    [InlineData("delay 250", false, 250_000L, 0, 0)]
    [InlineData("DELAY 2.5ms", false, 2_500L, 0, 0)]
    [InlineData("delay 300us", false, 300L, 0, 0)]
    [InlineData("delay random 10 20", true, 0L, 10, 20)]
    [InlineData("delay random 10..20", true, 0L, 10, 20)]
    [InlineData("delay random 5 5", true, 0L, 5, 5)]
    public void TryParseDelay_ParsesFixedAndRandomDelays(
        string step,
        bool expectedRandom,
        long expectedFixedMicroseconds,
        int expectedMin,
        int expectedMax)
    {
        var recognized = RunScriptInputSyntax.TryParseDelay(step, out var random, out var fixedMicroseconds, out var min, out var max, out var error);

        _ = recognized.Should().BeTrue();
        _ = error.Should().BeNull();
        _ = random.Should().Be(expectedRandom);
        _ = fixedMicroseconds.Should().Be(expectedFixedMicroseconds);
        _ = min.Should().Be(expectedMin);
        _ = max.Should().Be(expectedMax);
    }

    [Theory]
    [InlineData("delay abc")]
    [InlineData("delay -5")]
    [InlineData("delay random 10")]
    [InlineData("delay random a b")]
    [InlineData("delay random 1..x")]
    [InlineData("delay random 20 10")]
    [InlineData("delay random -1 5")]
    public void TryParseDelay_WhenRecognizedButInvalid_ReportsAnError(string step)
    {
        var recognized = RunScriptInputSyntax.TryParseDelay(step, out var random, out _, out _, out _, out var error);

        _ = recognized.Should().BeTrue();
        _ = error.Should().NotBeNullOrEmpty();
        _ = random.Should().BeFalse();
    }

    [Theory]
    [InlineData("move abs 10 20", MouseCoordinateMode.Absolute, 10, 20)]
    [InlineData("move rel -5 7", MouseCoordinateMode.Relative, -5, 7)]
    [InlineData("MOVE relative 0 0", MouseCoordinateMode.Relative, 0, 0)]
    public void TryParseMove_ParsesModeAndCoordinates(string step, MouseCoordinateMode expectedMode, int expectedX, int expectedY)
    {
        var recognized = RunScriptInputSyntax.TryParseMove(step, out var mode, out _, out var x, out var y, out var error);

        _ = recognized.Should().BeTrue();
        _ = error.Should().BeNull();
        _ = mode.Should().Be(expectedMode);
        _ = x.Should().Be(expectedX);
        _ = y.Should().Be(expectedY);
    }

    [Theory]
    [InlineData("move sideways 1 2")]
    [InlineData("move abs one 2")]
    [InlineData("move abs 1 2.5")]
    public void TryParseMove_WhenRecognizedButInvalid_ReportsAnError(string step)
    {
        _ = RunScriptInputSyntax.TryParseMove(step, out _, out _, out _, out _, out var error).Should().BeTrue();
        _ = error.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("click left", MacroMouseButton.Left, false)]
    [InlineData("click r", MacroMouseButton.Right, false)]
    [InlineData("click current middle", MacroMouseButton.Middle, true)]
    [InlineData("click CURRENT back", MacroMouseButton.Side1, true)]
    public void TryParseButton_ParsesButtonAndCurrentPositionMarker(string step, MacroMouseButton expectedButton, bool expectedCurrentPosition)
    {
        var recognized = RunScriptInputSyntax.TryParseButton(step, "click", out var button, out var useCurrentPosition, out var error);

        _ = recognized.Should().BeTrue();
        _ = error.Should().BeNull();
        _ = button.Should().Be(expectedButton);
        _ = useCurrentPosition.Should().Be(expectedCurrentPosition);
    }

    [Theory]
    [InlineData("click turbo")]
    [InlineData("click current turbo")]
    [InlineData("click left right extra")]
    [InlineData("click nowhere left")]
    public void TryParseButton_WhenRecognizedButInvalid_ReportsAnError(string step)
    {
        _ = RunScriptInputSyntax.TryParseButton(step, "click", out _, out _, out var error).Should().BeTrue();
        _ = error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void TryParseButton_OnlyMatchesTheRequestedCommand()
    {
        _ = RunScriptInputSyntax.TryParseButton("down left", "click", out _, out _, out _).Should().BeFalse();
        _ = RunScriptInputSyntax.TryParseButton("down left", "down", out var button, out _, out _).Should().BeTrue();
        _ = button.Should().Be(MacroMouseButton.Left);
    }

    [Theory]
    [InlineData("key down ctrl", true, "ctrl")]
    [InlineData("KEY UP a", false, "a")]
    public void TryParseKey_ParsesDirectionAndKey(string step, bool expectedDown, string expectedKey)
    {
        _ = RunScriptInputSyntax.TryParseKey(step, out var isDown, out var key, out var error).Should().BeTrue();
        _ = error.Should().BeNull();
        _ = isDown.Should().Be(expectedDown);
        _ = key.Should().Be(expectedKey);
    }

    [Fact]
    public void TryParseKey_WhenDirectionIsUnknown_ReportsAnError()
    {
        _ = RunScriptInputSyntax.TryParseKey("key hold a", out _, out var key, out var error).Should().BeTrue();
        _ = error.Should().NotBeNullOrEmpty();
        _ = key.Should().BeEmpty();
    }

    [Theory]
    [InlineData("tap ctrl+c", "ctrl+c")]
    [InlineData("TAP  enter ", "enter")]
    public void TryParseTap_ReturnsTheTrimmedCombo(string step, string expectedCombo)
    {
        _ = RunScriptInputSyntax.TryParseTap(step, out var combo).Should().BeTrue();
        _ = combo.Should().Be(expectedCombo);
    }

    [Fact]
    public void TryParseType_KeepsTheTextVerbatim()
    {
        _ = RunScriptInputSyntax.TryParseType("type  hello world ", out var text).Should().BeTrue();
        _ = text.Should().Be(" hello world ");
        _ = RunScriptInputSyntax.TryParseType("typewriter", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("scroll up", MacroMouseButton.ScrollUp, 1)]
    [InlineData("scroll DOWN 3", MacroMouseButton.ScrollDown, 3)]
    [InlineData("scroll left 2", MacroMouseButton.ScrollLeft, 2)]
    [InlineData("scroll right", MacroMouseButton.ScrollRight, 1)]
    public void TryParseScroll_ParsesDirectionAndCount(string step, MacroMouseButton expectedButton, int expectedCount)
    {
        _ = RunScriptInputSyntax.TryParseScroll(step, out var button, out var count, out var error).Should().BeTrue();
        _ = error.Should().BeNull();
        _ = button.Should().Be(expectedButton);
        _ = count.Should().Be(expectedCount);
    }

    [Theory]
    [InlineData("scroll sideways")]
    [InlineData("scroll up 0")]
    [InlineData("scroll up -2")]
    [InlineData("scroll up many")]
    public void TryParseScroll_WhenRecognizedButInvalid_ReportsAnError(string step)
    {
        _ = RunScriptInputSyntax.TryParseScroll(step, out _, out _, out var error).Should().BeTrue();
        _ = error.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("left", MacroMouseButton.Left)]
    [InlineData("L", MacroMouseButton.Left)]
    [InlineData("forward", MacroMouseButton.Side2)]
    [InlineData("EXTRA", MacroMouseButton.Side2)]
    [InlineData("side", MacroMouseButton.Side1)]
    public void TryResolveButton_AcceptsCanonicalNamesAndAliases(string token, MacroMouseButton expected)
    {
        _ = RunScriptInputSyntax.TryResolveButton(token, out var button).Should().BeTrue();
        _ = button.Should().Be(expected);
    }

    [Fact]
    public void TryResolveButton_RejectsUnknownNames()
    {
        _ = RunScriptInputSyntax.TryResolveButton("scrollwheel", out var button).Should().BeFalse();
        _ = button.Should().Be(MacroMouseButton.None);
    }
}
