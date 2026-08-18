namespace CrossMacro.Core.Tests.Models;


public sealed class MacroEventTests
{
    [Fact]
    public void NewMacroEvent_HasDefaultValues()
    {
        // Arrange & Act
        var ev = new MacroEvent();

        // Assert
        _ = ev.Type.Should().Be(EventType.None); // Default enum value
        _ = ev.X.Should().Be(0);
        _ = ev.Y.Should().Be(0);
        _ = ev.Button.Should().Be(MacroMouseButton.None);
        _ = ev.Timestamp.Should().Be(0);
        _ = ev.DelayMs.Should().Be(0);
        _ = ev.HasRandomDelay.Should().BeFalse();
        _ = ev.RandomDelayMinMs.Should().Be(0);
        _ = ev.RandomDelayMaxMs.Should().Be(0);
        _ = ev.KeyCode.Should().Be(0);
        _ = ev.CoordinateMode.Should().BeNull();
    }

    [Fact]
    public void Timing_UsesMicrosecondsAsTheSingleMutableSourceOfTruth()
    {
        var ev = new MacroEvent
        {
            TimestampMicroseconds = 12_345,
            DelayMicroseconds = 678,
        };

        _ = ev.Timestamp.Should().Be(12);
        _ = ev.DelayMs.Should().Be(0);

        ev.DelayMs = 7;
        ev.Timestamp = 19;

        _ = ev.DelayMicroseconds.Should().Be(7_000);
        _ = ev.TimestampMicroseconds.Should().Be(19_000);
    }

    [Theory]
    [InlineData("20", 20_000)]
    [InlineData("2.375ms", 2_375)]
    [InlineData("250us", 250)]
    [InlineData("250µs", 250)]
    public void MacroTiming_ParsesUserDurationsWithoutLosingMicroseconds(string input, long expectedMicroseconds)
    {
        _ = MacroTiming.TryParseDurationMicroseconds(input, out var actual).Should().BeTrue();
        _ = actual.Should().Be(expectedMicroseconds);
    }

    [Fact]
    public void MacroTiming_FormatsSubMillisecondDurationForEditorAndScriptRoundTrip()
    {
        _ = MacroTiming.FormatDuration(2_375).Should().Be("2.375ms");
        _ = MacroTiming.FormatDuration(250).Should().Be("250us");
        _ = MacroTiming.FormatScriptDuration(2_375).Should().Be("2.375ms");
        _ = MacroTiming.FormatScriptDuration(2_000).Should().Be("2");
    }

    [Fact]
    public void MacroTiming_RejectsMillisecondsThatOverflowMicrosecondTimeline()
    {
        bool parsed = true;

        var action = () => parsed = MacroTiming.TryParseDurationMicroseconds(
            "9223372036854776ms",
            out _);

        _ = action.Should().NotThrow();
        _ = parsed.Should().BeFalse();
    }

    [Theory]
    [InlineData(EventType.ButtonPress)]
    [InlineData(EventType.ButtonRelease)]
    [InlineData(EventType.MouseMove)]
    [InlineData(EventType.Click)]
    [InlineData(EventType.KeyPress)]
    [InlineData(EventType.KeyRelease)]
    public void MacroEvent_CanSetAllEventTypes(EventType eventType)
    {
        // Arrange & Act
        var ev = new MacroEvent { Type = eventType };

        // Assert
        _ = ev.Type.Should().Be(eventType);
    }

    [Theory]
    [InlineData(MacroMouseButton.None)]
    [InlineData(MacroMouseButton.Left)]
    [InlineData(MacroMouseButton.Right)]
    [InlineData(MacroMouseButton.Middle)]
    [InlineData(MacroMouseButton.ScrollUp)]
    [InlineData(MacroMouseButton.ScrollDown)]
    public void MacroEvent_CanSetAllMouseButtons(MacroMouseButton button)
    {
        // Arrange & Act
        var ev = new MacroEvent { Button = button };

        // Assert
        _ = ev.Button.Should().Be(button);
    }

    [Fact]
    public void MacroEvent_CanSetCoordinates()
    {
        // Arrange & Act
        var ev = new MacroEvent { X = 1920, Y = 1080 };

        // Assert
        _ = ev.X.Should().Be(1920);
        _ = ev.Y.Should().Be(1080);
    }

    [Fact]
    public void MacroEvent_CanSetNegativeCoordinates()
    {
        // Some scenarios might have negative relative coordinates
        var ev = new MacroEvent { X = -100, Y = -50 };

        _ = ev.X.Should().Be(-100);
        _ = ev.Y.Should().Be(-50);
    }

    [Theory]
    [InlineData(MouseCoordinateMode.Absolute)]
    [InlineData(MouseCoordinateMode.Relative)]
    public void MacroEvent_CanSetCoordinateMode(MouseCoordinateMode coordinateMode)
    {
        var ev = new MacroEvent
        {
            Type = EventType.MouseMove,
            CoordinateMode = coordinateMode,
        };

        _ = ev.CoordinateMode.Should().Be(coordinateMode);
    }

    [Fact]
    public void MacroEvent_CanSetKeyCode()
    {
        // Arrange - Linux KEY_A = 30
        var ev = new MacroEvent { Type = EventType.KeyPress, KeyCode = 30 };

        // Assert
        _ = ev.KeyCode.Should().Be(30);
    }
}
