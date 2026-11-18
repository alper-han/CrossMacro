namespace CrossMacro.Infrastructure.Tests.Services.Playback;

public sealed class PlaybackValidatorTests
{
    private const string ZeroZeroWarning =
        "Absolute macro contains click/down/up event(s) at (0,0); this may cause cursor jumps to top-left.";

    private readonly PlaybackValidator _validator = new(CreateKeyCodeMapper(), CreateSupportedPositionProvider());

    [Fact]
    public void Validate_WhenHiddenScreenReadScriptContainsUnsupportedStep_ReturnsError()
    {
        var macro = new MacroSequence
        {
            ScriptSteps = { "pixelcolor 1 2 sampled", "bogus" },
        };

        var result = _validator.Validate(macro);

        _ = result.IsValid.Should().BeFalse();
        _ = result.Errors.Should().Contain(error => error.Contains("Macro script steps are invalid", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("tap Backspace")]
    [InlineData("tap F13")]
    [InlineData("tap NumpadPlus")]
    public void Validate_WhenScriptUsesRuntimeMappedKey_ReturnsNoErrors(string scriptStep)
    {
        var macro = new MacroSequence
        {
            ScriptSteps = { "pixelcolor 1 2 sampled", scriptStep },
        };

        var result = _validator.Validate(macro);

        _ = result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_EmptyEvents_ReturnsError()
    {
        var result = _validator.Validate(new MacroSequence());

        _ = result.IsValid.Should().BeFalse();
        _ = result.Errors.Should().Equal("Macro is empty or null");
    }

    [Fact]
    public void Validate_ValidMacro_ReturnsNoErrorsAndNoWarnings()
    {
        var macro = new MacroSequence
        {
            Events = {
                new() { Type = EventType.MouseMove, X = 100, Y = 200 },
            },
        };

        var result = _validator.Validate(macro);

        _ = result.Errors.Should().BeEmpty();
        _ = result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithInvalidEventType_ReturnsError()
    {
        var macro = new MacroSequence
        {
            Events = {
                new() { Type = (EventType)999 },
            },
        };

        var result = _validator.Validate(macro);

        _ = result.IsValid.Should().BeFalse();
        _ = result.Errors.Should().Equal("Macro contains invalid/undefined EventType values");
    }

    [Fact]
    public void Validate_WithNoneEventType_WarnsButStaysValid()
    {
        var macro = new MacroSequence
        {
            Events = {
                new() { Type = EventType.None },
            },
        };

        var result = _validator.Validate(macro);

        _ = result.IsValid.Should().BeTrue();
        _ = result.Warnings.Should().Equal("Macro contains events with Type 'None'");
    }

    [Theory]
    [InlineData(EventType.Click)]
    [InlineData(EventType.ButtonPress)]
    [InlineData(EventType.ButtonRelease)]
    public void Validate_ButtonEventWithoutMouseButton_ReturnsError(EventType type)
    {
        var macro = new MacroSequence
        {
            Events = {
                new() { Type = type, Button = MacroMouseButton.None },
            },
        };

        var result = _validator.Validate(macro);

        _ = result.Errors.Should().Equal("Macro contains 1 click/down/up event(s) without a mouse button");
    }

    [Fact]
    public void Validate_AbsoluteMacroWithZeroZeroButtonAndNonZeroMouseContext_ReturnsWarning()
    {
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events = {
                new() { Type = EventType.MouseMove, X = 500, Y = 300 },
                new() { Type = EventType.ButtonPress, Button = MacroMouseButton.Left, X = 0, Y = 0 },
            },
        };

        var result = _validator.Validate(macro);

        _ = result.Warnings.Should().Equal(ZeroZeroWarning);
    }

    [Fact]
    public void Validate_ExplicitAbsoluteButtonEventInRelativeMacro_ReturnsWarning()
    {
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            Events = {
                new() { Type = EventType.MouseMove, X = 500, Y = 300, CoordinateMode = MouseCoordinateMode.Relative },
                new() { Type = EventType.ButtonPress, Button = MacroMouseButton.Left, X = 0, Y = 0, CoordinateMode = MouseCoordinateMode.Absolute },
            },
        };

        var result = _validator.Validate(macro);

        _ = result.Warnings.Should().Equal(ZeroZeroWarning);
    }

    [Fact]
    public void Validate_ExplicitRelativeButtonEventInAbsoluteMacro_DoesNotReturnZeroZeroWarning()
    {
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events = {
                new() { Type = EventType.MouseMove, X = 500, Y = 300, CoordinateMode = MouseCoordinateMode.Absolute },
                new() { Type = EventType.ButtonPress, Button = MacroMouseButton.Left, X = 0, Y = 0, CoordinateMode = MouseCoordinateMode.Relative },
            },
        };

        var result = _validator.Validate(macro);

        _ = result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Validate_CurrentPositionButtonEventWithNullMode_DoesNotReturnZeroZeroWarning()
    {
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events = {
                new() { Type = EventType.MouseMove, X = 400, Y = 250 },
                new() { Type = EventType.Click, Button = MacroMouseButton.Left, UseCurrentPosition = true, X = 0, Y = 0 },
            },
        };

        var result = _validator.Validate(macro);

        _ = result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ScrollEventWithNullMode_DoesNotReturnZeroZeroWarning()
    {
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events = {
                new() { Type = EventType.MouseMove, X = 400, Y = 250 },
                new() { Type = EventType.Click, Button = MacroMouseButton.ScrollUp, X = 0, Y = 0 },
            },
        };

        var result = _validator.Validate(macro);

        _ = result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Validate_LongDelay_ReturnsWarning()
    {
        var macro = new MacroSequence
        {
            Events = {
                new() { Type = EventType.MouseMove, DelayMs = 15000 },
            },
        };

        var result = _validator.Validate(macro);

        _ = result.Warnings.Should().Equal("Macro contains 1 delay(s) > 10 seconds (max: 15.0s)");
    }

    [Fact]
    public void Validate_VeryLongMacro_ReturnsWarning()
    {
        var macro = new MacroSequence
        {
            TotalDurationMs = 350000,
            Events = {
                new() { Type = EventType.MouseMove },
            },
        };

        var result = _validator.Validate(macro);

        _ = result.Warnings.Should().Equal("Macro is very long (5.8 minutes)");
    }

    [Fact]
    public void Validate_ManyEvents_ReturnsWarning()
    {
        var events = Enumerable.Range(0, 10001)
            .Select(_ => new MacroEvent { Type = EventType.MouseMove })
            .ToList();

        var macro = new MacroSequence();
        macro.ReplaceEvents(events);

        var result = _validator.Validate(macro);

        _ = result.Warnings.Should().Equal("Macro has 10001 events - playback may be resource intensive");
    }

    [Fact]
    public void Validate_WithUnsupportedPositionProvider_ReturnsWarning()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.IsSupported.Returns(returnThis: false);
        _ = positionProvider.ProviderName.Returns("MockProvider");

        var validator = new PlaybackValidator(CreateKeyCodeMapper(), positionProvider);
        var macro = new MacroSequence
        {
            Events = {
                new() { Type = EventType.MouseMove },
            },
        };

        var result = validator.Validate(macro);

        _ = result.Warnings.Should().Equal("Position provider 'MockProvider' is not supported on this system");
    }

    [Fact]
    public void Validate_WithNullPositionProvider_ReturnsFallbackWarning()
    {
        var validator = new PlaybackValidator(CreateKeyCodeMapper());
        var macro = new MacroSequence
        {
            Events = {
                new() { Type = EventType.MouseMove },
            },
        };

        var result = validator.Validate(macro);

        _ = result.Warnings.Should().Equal("No position provider available - using fallback mode");
    }

    private static IKeyCodeMapper CreateKeyCodeMapper()
    {
        return new KeyCodeMapper(new TestKeyboardLayoutService(mapsCharacters: false));
    }

    private static IMousePositionProvider CreateSupportedPositionProvider()
    {
        var positionProvider = Substitute.For<IMousePositionProvider>();
        _ = positionProvider.IsSupported.Returns(returnThis: true);
        _ = positionProvider.ProviderName.Returns("test-position");
        return positionProvider;
    }
}
