namespace CrossMacro.Core.Tests.Models;

public sealed class MacroEventTests
{
    [Fact]
    public void NewMacroEvent_HasDefaultValues()
    {
        var ev = new MacroEvent();

        _ = ev.Type.Should().Be(EventType.None);
        _ = ev.Button.Should().Be(MacroMouseButton.None);
        _ = ev.Timestamp.Should().Be(0);
        _ = ev.DelayMs.Should().Be(0);
        _ = ev.HasRandomDelay.Should().BeFalse();
        _ = ev.CoordinateMode.Should().BeNull();
        _ = ev.CoordinateSpace.Should().BeNull();
        _ = ev.UseCurrentPosition.Should().BeFalse();
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

    [Fact]
    public void MacroEvent_EqualityIncludesCoordinateAndCurrentPositionFields()
    {
        var first = new MacroEvent
        {
            Type = EventType.Click,
            CoordinateMode = MouseCoordinateMode.Relative,
            CoordinateSpace = MouseCoordinateSpace.LogicalDesktop,
            UseCurrentPosition = false,
        };
        var second = first;

        _ = second.Should().Be(first);
        _ = second.GetHashCode().Should().Be(first.GetHashCode());

        second.CoordinateSpace = MouseCoordinateSpace.RawDevice;
        _ = second.Should().NotBe(first);

        second = first;
        second.UseCurrentPosition = true;
        _ = second.Should().NotBe(first);
    }
}
