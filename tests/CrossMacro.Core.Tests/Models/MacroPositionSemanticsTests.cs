namespace CrossMacro.Core.Tests.Models;

using System.Collections.Generic;
using CrossMacro.Core.Models;
using FluentAssertions;

public class MacroPositionSemanticsTests
{
    [Fact]
    public void IsCoordinateBearing_WhenMouseMove_ReturnsTrue()
    {
        var ev = new MacroEvent { Type = EventType.MouseMove };

        MacroPositionSemantics.IsCoordinateBearing(ev).Should().BeTrue();
    }

    [Theory]
    [InlineData(EventType.ButtonPress)]
    [InlineData(EventType.ButtonRelease)]
    [InlineData(EventType.Click)]
    public void IsCoordinateBearing_WhenNonScrollMouseButtonUsesStoredPosition_ReturnsTrue(EventType eventType)
    {
        var ev = new MacroEvent
        {
            Type = eventType,
            Button = MouseButton.Left,
            UseCurrentPosition = false
        };

        MacroPositionSemantics.IsCoordinateBearing(ev).Should().BeTrue();
    }

    [Theory]
    [InlineData(EventType.ButtonPress)]
    [InlineData(EventType.ButtonRelease)]
    [InlineData(EventType.Click)]
    public void IsCoordinateBearing_WhenCurrentPositionMouseButton_ReturnsFalse(EventType eventType)
    {
        var ev = new MacroEvent
        {
            Type = eventType,
            Button = MouseButton.Left,
            UseCurrentPosition = true,
            CoordinateMode = MouseCoordinateMode.Relative
        };

        MacroPositionSemantics.IsCoordinateBearing(ev).Should().BeFalse();
        MacroPositionSemantics.ResolveCoordinateMode(ev, legacyIsAbsolute: false).Should().BeNull();
    }

    [Theory]
    [InlineData(MouseButton.ScrollUp)]
    [InlineData(MouseButton.ScrollDown)]
    [InlineData(MouseButton.ScrollLeft)]
    [InlineData(MouseButton.ScrollRight)]
    public void IsCoordinateBearing_WhenScrollClick_ReturnsFalse(MouseButton button)
    {
        var ev = new MacroEvent
        {
            Type = EventType.Click,
            Button = button,
            CoordinateMode = MouseCoordinateMode.Absolute
        };

        MacroPositionSemantics.IsCoordinateBearing(ev).Should().BeFalse();
        MacroPositionSemantics.ResolveCoordinateMode(ev, legacyIsAbsolute: true).Should().BeNull();
    }

    [Fact]
    public void ResolveCoordinateMode_WhenExplicitAbsoluteAndLegacyRelative_ReturnsAbsolute()
    {
        var ev = new MacroEvent
        {
            Type = EventType.MouseMove,
            CoordinateMode = MouseCoordinateMode.Absolute
        };

        MacroPositionSemantics.HasExplicitCoordinateMode(ev).Should().BeTrue();
        MacroPositionSemantics.ResolveCoordinateMode(ev, legacyIsAbsolute: false).Should().Be(MouseCoordinateMode.Absolute);
    }

    [Fact]
    public void ResolveCoordinateMode_WhenExplicitRelativeAndLegacyAbsolute_ReturnsRelative()
    {
        var ev = new MacroEvent
        {
            Type = EventType.MouseMove,
            CoordinateMode = MouseCoordinateMode.Relative
        };

        MacroPositionSemantics.HasExplicitCoordinateMode(ev).Should().BeTrue();
        MacroPositionSemantics.ResolveCoordinateMode(ev, legacyIsAbsolute: true).Should().Be(MouseCoordinateMode.Relative);
    }

    [Theory]
    [InlineData(true, MouseCoordinateMode.Absolute)]
    [InlineData(false, MouseCoordinateMode.Relative)]
    public void ResolveCoordinateMode_WhenCoordinateModeUnset_UsesLegacyFallback(bool legacyIsAbsolute, MouseCoordinateMode expected)
    {
        var ev = new MacroEvent { Type = EventType.MouseMove };

        MacroPositionSemantics.HasExplicitCoordinateMode(ev).Should().BeFalse();
        MacroPositionSemantics.ResolveCoordinateMode(ev, legacyIsAbsolute).Should().Be(expected);
    }

    [Theory]
    [InlineData(EventType.KeyPress)]
    [InlineData(EventType.KeyRelease)]
    [InlineData(EventType.None)]
    public void ResolveCoordinateMode_WhenNotCoordinateBearing_ReturnsNull(EventType eventType)
    {
        var ev = new MacroEvent
        {
            Type = eventType,
            CoordinateMode = MouseCoordinateMode.Absolute
        };

        MacroPositionSemantics.ResolveCoordinateMode(ev, legacyIsAbsolute: true).Should().BeNull();
    }

    [Fact]
    public void HasAnyAbsoluteCoordinateEvents_WhenEffectiveAbsoluteExists_ReturnsTrue()
    {
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, CoordinateMode = MouseCoordinateMode.Relative },
                new() { Type = EventType.Click, Button = MouseButton.Left, CoordinateMode = MouseCoordinateMode.Absolute }
            }
        };

        MacroPositionSemantics.HasAnyAbsoluteCoordinateEvents(macro).Should().BeTrue();
    }

    [Fact]
    public void HasAnyAbsoluteCoordinateEvents_WhenOnlyRelativeEvents_ReturnsFalse()
    {
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove },
                new() { Type = EventType.Click, Button = MouseButton.Left, CoordinateMode = MouseCoordinateMode.Relative }
            }
        };

        MacroPositionSemantics.HasAnyAbsoluteCoordinateEvents(macro).Should().BeFalse();
    }

    [Fact]
    public void GetCoordinateModeSummary_WhenNoCoordinateBearingEvents_ReturnsNone()
    {
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.KeyPress, KeyCode = 30 },
                new() { Type = EventType.Click, Button = MouseButton.Left, UseCurrentPosition = true },
                new() { Type = EventType.Click, Button = MouseButton.ScrollUp }
            }
        };

        MacroPositionSemantics.GetCoordinateModeSummary(macro).Should().Be(CoordinateModeSummary.None);
    }

    [Fact]
    public void GetCoordinateModeSummary_WhenOnlyAbsoluteCoordinateEvents_ReturnsAbsolute()
    {
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove },
                new() { Type = EventType.Click, Button = MouseButton.Left, CoordinateMode = MouseCoordinateMode.Absolute }
            }
        };

        MacroPositionSemantics.GetCoordinateModeSummary(macro).Should().Be(CoordinateModeSummary.Absolute);
    }

    [Fact]
    public void GetCoordinateModeSummary_WhenOnlyRelativeCoordinateEvents_ReturnsRelative()
    {
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove },
                new() { Type = EventType.ButtonPress, Button = MouseButton.Left, CoordinateMode = MouseCoordinateMode.Relative }
            }
        };

        MacroPositionSemantics.GetCoordinateModeSummary(macro).Should().Be(CoordinateModeSummary.Relative);
    }

    [Fact]
    public void GetCoordinateModeSummary_WhenAbsoluteAndRelativeCoordinateEvents_ReturnsMixed()
    {
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            Events = new List<MacroEvent>
            {
                new() { Type = EventType.MouseMove, CoordinateMode = MouseCoordinateMode.Absolute },
                new() { Type = EventType.Click, Button = MouseButton.Left, CoordinateMode = MouseCoordinateMode.Relative }
            }
        };

        MacroPositionSemantics.GetCoordinateModeSummary(macro).Should().Be(CoordinateModeSummary.Mixed);
    }
}
