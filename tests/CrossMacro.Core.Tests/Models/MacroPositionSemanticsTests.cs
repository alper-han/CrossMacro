namespace CrossMacro.Core.Tests.Models;


public sealed class MacroPositionSemanticsTests
{
    [Fact]
    public void IsCoordinateBearing_WhenMouseMove_ReturnsTrue()
    {
        var ev = new MacroEvent { Type = EventType.MouseMove };

        _ = MacroPositionSemantics.IsCoordinateBearing(ev).Should().BeTrue();
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
            Button = MacroMouseButton.Left,
            UseCurrentPosition = false,
        };

        _ = MacroPositionSemantics.IsCoordinateBearing(ev).Should().BeTrue();
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
            Button = MacroMouseButton.Left,
            UseCurrentPosition = true,
            CoordinateMode = MouseCoordinateMode.Relative,
        };

        _ = MacroPositionSemantics.IsCoordinateBearing(ev).Should().BeFalse();
        _ = MacroPositionSemantics.ResolveCoordinateMode(ev, legacyIsAbsolute: false).Should().BeNull();
    }

    [Theory]
    [InlineData(MacroMouseButton.ScrollUp)]
    [InlineData(MacroMouseButton.ScrollDown)]
    [InlineData(MacroMouseButton.ScrollLeft)]
    [InlineData(MacroMouseButton.ScrollRight)]
    public void IsCoordinateBearing_WhenScrollClick_ReturnsFalse(MacroMouseButton button)
    {
        var ev = new MacroEvent
        {
            Type = EventType.Click,
            Button = button,
            CoordinateMode = MouseCoordinateMode.Absolute,
        };

        _ = MacroPositionSemantics.IsCoordinateBearing(ev).Should().BeFalse();
        _ = MacroPositionSemantics.ResolveCoordinateMode(ev, legacyIsAbsolute: true).Should().BeNull();
    }

    [Fact]
    public void ResolveCoordinateMode_WhenExplicitAbsoluteAndLegacyRelative_ReturnsAbsolute()
    {
        var ev = new MacroEvent
        {
            Type = EventType.MouseMove,
            CoordinateMode = MouseCoordinateMode.Absolute,
        };

        _ = MacroPositionSemantics.HasExplicitCoordinateMode(ev).Should().BeTrue();
        _ = MacroPositionSemantics.ResolveCoordinateMode(ev, legacyIsAbsolute: false).Should().Be(MouseCoordinateMode.Absolute);
    }

    [Fact]
    public void ResolveCoordinateMode_WhenExplicitRelativeAndLegacyAbsolute_ReturnsRelative()
    {
        var ev = new MacroEvent
        {
            Type = EventType.MouseMove,
            CoordinateMode = MouseCoordinateMode.Relative,
        };

        _ = MacroPositionSemantics.HasExplicitCoordinateMode(ev).Should().BeTrue();
        _ = MacroPositionSemantics.ResolveCoordinateMode(ev, legacyIsAbsolute: true).Should().Be(MouseCoordinateMode.Relative);
    }

    [Theory]
    [InlineData(true, MouseCoordinateMode.Absolute)]
    [InlineData(false, MouseCoordinateMode.Relative)]
    public void ResolveCoordinateMode_WhenCoordinateModeUnset_UsesLegacyFallback(bool legacyIsAbsolute, MouseCoordinateMode expected)
    {
        var ev = new MacroEvent { Type = EventType.MouseMove };

        _ = MacroPositionSemantics.HasExplicitCoordinateMode(ev).Should().BeFalse();
        _ = MacroPositionSemantics.ResolveCoordinateMode(ev, legacyIsAbsolute).Should().Be(expected);
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
            CoordinateMode = MouseCoordinateMode.Absolute,
        };

        _ = MacroPositionSemantics.ResolveCoordinateMode(ev, legacyIsAbsolute: true).Should().BeNull();
    }

    [Fact]
    public void ResolveCoordinateSpace_WhenAbsolute_ReturnsLogicalDesktop()
    {
        var ev = new MacroEvent
        {
            Type = EventType.MouseMove,
            CoordinateMode = MouseCoordinateMode.Absolute,
            CoordinateSpace = MouseCoordinateSpace.RawDevice,
        };

        _ = MacroPositionSemantics.ResolveCoordinateSpace(ev, legacyIsAbsolute: false)
            .Should().Be(MouseCoordinateSpace.LogicalDesktop);
    }

    [Theory]
    [InlineData(MouseCoordinateSpace.LogicalDesktop)]
    [InlineData(MouseCoordinateSpace.RawDevice)]
    public void ResolveCoordinateSpace_WhenRelativeSpaceIsExplicit_PreservesSpace(MouseCoordinateSpace coordinateSpace)
    {
        var ev = new MacroEvent
        {
            Type = EventType.MouseMove,
            CoordinateMode = MouseCoordinateMode.Relative,
            CoordinateSpace = coordinateSpace,
        };

        _ = MacroPositionSemantics.ResolveCoordinateSpace(ev, legacyIsAbsolute: true)
            .Should().Be(coordinateSpace);
    }

    [Fact]
    public void ResolveCoordinateSpace_WhenLegacyRelativeSpaceIsUnset_DefaultsToRawDevice()
    {
        var ev = new MacroEvent { Type = EventType.MouseMove };

        _ = MacroPositionSemantics.ResolveCoordinateSpace(ev, legacyIsAbsolute: false)
            .Should().Be(MouseCoordinateSpace.RawDevice);
    }

    [Fact]
    public void ResolveCoordinateSpace_WhenEventIsNotCoordinateBearing_ReturnsNull()
    {
        var ev = new MacroEvent
        {
            Type = EventType.KeyPress,
            CoordinateMode = MouseCoordinateMode.Relative,
            CoordinateSpace = MouseCoordinateSpace.LogicalDesktop,
        };

        _ = MacroPositionSemantics.ResolveCoordinateSpace(ev, legacyIsAbsolute: false).Should().BeNull();
    }

    [Fact]
    public void HasAnyLogicalDesktopCoordinateEvents_WhenLogicalRelativeExists_ReturnsTrue()
    {
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            Events =
            {
                new()
                {
                    Type = EventType.MouseMove,
                    CoordinateMode = MouseCoordinateMode.Relative,
                    CoordinateSpace = MouseCoordinateSpace.RawDevice,
                },
                new()
                {
                    Type = EventType.MouseMove,
                    CoordinateMode = MouseCoordinateMode.Relative,
                    CoordinateSpace = MouseCoordinateSpace.LogicalDesktop,
                },
            },
        };

        _ = MacroPositionSemantics.HasAnyLogicalDesktopCoordinateEvents(macro).Should().BeTrue();
    }

    [Fact]
    public void HasAnyAbsoluteCoordinateEvents_WhenEffectiveAbsoluteExists_ReturnsTrue()
    {
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            Events = {
                new() { Type = EventType.MouseMove, CoordinateMode = MouseCoordinateMode.Relative },
                new() { Type = EventType.Click, Button = MacroMouseButton.Left, CoordinateMode = MouseCoordinateMode.Absolute },
            },
        };

        _ = MacroPositionSemantics.HasAnyAbsoluteCoordinateEvents(macro).Should().BeTrue();
    }

    [Fact]
    public void HasAnyAbsoluteCoordinateEvents_WhenOnlyRelativeEvents_ReturnsFalse()
    {
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            Events = {
                new() { Type = EventType.MouseMove },
                new() { Type = EventType.Click, Button = MacroMouseButton.Left, CoordinateMode = MouseCoordinateMode.Relative },
            },
        };

        _ = MacroPositionSemantics.HasAnyAbsoluteCoordinateEvents(macro).Should().BeFalse();
    }

    [Fact]
    public void RequiresInitialCornerReset_WhenFirstPositionEventIsRelativeAndResetEnabled_ReturnsTrue()
    {
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            SkipInitialZeroZero = false,
            Events =
            {
                new() { Type = EventType.KeyPress, KeyCode = 30 },
                new()
                {
                    Type = EventType.MouseMove,
                    CoordinateMode = MouseCoordinateMode.Relative,
                },
            },
        };

        _ = MacroPositionSemantics.ResolveInitialCoordinateMode(macro)
            .Should().Be(MouseCoordinateMode.Relative);
        _ = MacroPositionSemantics.RequiresInitialCornerReset(macro).Should().BeTrue();
    }

    [Fact]
    public void RequiresInitialCornerReset_WhenCurrentPositionClickIsFirst_ReturnsFalse()
    {
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = false,
            Events =
            {
                new()
                {
                    Type = EventType.Click,
                    Button = MacroMouseButton.Left,
                    UseCurrentPosition = true,
                },
                new() { Type = EventType.MouseMove },
            },
        };

        _ = MacroPositionSemantics.ResolveInitialCoordinateMode(macro).Should().BeNull();
        _ = MacroPositionSemantics.RequiresInitialCornerReset(macro).Should().BeFalse();
    }

    [Fact]
    public void RequiresInitialCornerReset_WhenResetIsSkipped_ReturnsFalse()
    {
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = true,
            Events = { new() { Type = EventType.MouseMove } },
        };

        _ = MacroPositionSemantics.RequiresInitialCornerReset(macro).Should().BeFalse();
    }

    [Fact]
    public void GetCoordinateModeSummary_WhenNoCoordinateBearingEvents_ReturnsNone()
    {
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events = {
                new() { Type = EventType.KeyPress, KeyCode = 30 },
                new() { Type = EventType.Click, Button = MacroMouseButton.Left, UseCurrentPosition = true },
                new() { Type = EventType.Click, Button = MacroMouseButton.ScrollUp },
            },
        };

        _ = MacroPositionSemantics.GetCoordinateModeSummary(macro).Should().Be(CoordinateModeSummary.None);
    }

    [Fact]
    public void GetCoordinateModeSummary_WhenOnlyAbsoluteCoordinateEvents_ReturnsAbsolute()
    {
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = true,
            Events = {
                new() { Type = EventType.MouseMove },
                new() { Type = EventType.Click, Button = MacroMouseButton.Left, CoordinateMode = MouseCoordinateMode.Absolute },
            },
        };

        _ = MacroPositionSemantics.GetCoordinateModeSummary(macro).Should().Be(CoordinateModeSummary.Absolute);
    }

    [Fact]
    public void GetCoordinateModeSummary_WhenOnlyRelativeCoordinateEvents_ReturnsRelative()
    {
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            Events = {
                new() { Type = EventType.MouseMove },
                new() { Type = EventType.ButtonPress, Button = MacroMouseButton.Left, CoordinateMode = MouseCoordinateMode.Relative },
            },
        };

        _ = MacroPositionSemantics.GetCoordinateModeSummary(macro).Should().Be(CoordinateModeSummary.Relative);
    }

    [Fact]
    public void GetCoordinateModeSummary_WhenAbsoluteAndRelativeCoordinateEvents_ReturnsMixed()
    {
        var macro = new MacroSequence
        {
            IsAbsoluteCoordinates = false,
            Events = {
                new() { Type = EventType.MouseMove, CoordinateMode = MouseCoordinateMode.Absolute },
                new() { Type = EventType.Click, Button = MacroMouseButton.Left, CoordinateMode = MouseCoordinateMode.Relative },
            },
        };

        _ = MacroPositionSemantics.GetCoordinateModeSummary(macro).Should().Be(CoordinateModeSummary.Mixed);
    }
}
