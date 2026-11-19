namespace CrossMacro.Core.Tests.Models;

public sealed class MacroSequenceTests
{
    [Fact]
    public void IsValid_EmptyEvents_ReturnsFalse()
    {
        var macro = new MacroSequence();

        var result = macro.IsValid();

        _ = result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithScriptStepsAndEmptyEvents_ReturnsTrue()
    {
        var macro = new MacroSequence();
        macro.ReplaceScriptSteps(["pixelcolor 10 20 color"]);

        var result = macro.IsValid();

        _ = result.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WithMalformedScriptStepsAndEmptyEvents_ReturnsTrueForStructuralValidationOnly()
    {
        var macro = new MacroSequence();
        macro.ReplaceScriptSteps(["pixelcolor 1"]);

        var result = macro.IsValid();

        _ = result.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WithOnlyBlankScriptStepsAndEmptyEvents_ReturnsFalse()
    {
        var macro = new MacroSequence();
        macro.ReplaceScriptSteps([" ", ""]);

        var result = macro.IsValid();

        _ = result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_EventWithNegativeTimestamp_ReturnsFalse()
    {
        var macro = new MacroSequence
        {
            Events =
            {
                new() { Type = EventType.MouseMove, Timestamp = -1, DelayMs = 0 },
            },
        };

        var result = macro.IsValid();

        _ = result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_EventWithNegativeDelay_ReturnsFalse()
    {
        var macro = new MacroSequence
        {
            Events =
            {
                new() { Type = EventType.MouseMove, Timestamp = 0, DelayMs = -100 },
            },
        };

        var result = macro.IsValid();

        _ = result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_EventWithInvalidRandomDelayBounds_ReturnsFalse()
    {
        var macro = new MacroSequence
        {
            Events =
            {
                new()
                {
                    Type = EventType.MouseMove,
                    Timestamp = 0,
                    DelayMs = 0,
                    HasRandomDelay = true,
                    RandomDelayMinMs = 200,
                    RandomDelayMaxMs = 100,
                },
            },
        };

        var result = macro.IsValid();

        _ = result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_TrailingRandomDelayWithInvalidBounds_ReturnsFalse()
    {
        var macro = new MacroSequence
        {
            HasTrailingRandomDelay = true,
            TrailingDelayMinMs = 300,
            TrailingDelayMaxMs = 100,
            Events =
            {
                new() { Type = EventType.MouseMove, Timestamp = 0, DelayMs = 0 },
            },
        };

        var result = macro.IsValid();

        _ = result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_ValidEvents_ReturnsTrue()
    {
        var macro = new MacroSequence
        {
            Events =
            {
                new() { Type = EventType.MouseMove, Timestamp = 0, DelayMs = 0, X = 100, Y = 200 },
                new() { Type = EventType.ButtonPress, Timestamp = 100, DelayMs = 100, Button = MacroMouseButton.Left },
                new() { Type = EventType.ButtonRelease, Timestamp = 150, DelayMs = 50, Button = MacroMouseButton.Left },
            },
        };

        var result = macro.IsValid();

        _ = result.Should().BeTrue();
    }

    [Fact]
    public void CalculateDuration_EmptyEvents_SetsDurationToZero()
    {
        var macro = new MacroSequence();

        macro.CalculateDuration();

        _ = macro.TotalDurationMs.Should().Be(0);
    }

    [Fact]
    public void CalculateDuration_WithEvents_SetsDurationToLastTimestamp()
    {
        var macro = new MacroSequence
        {
            Events =
            {
                new() { Timestamp = 0 },
                new() { Timestamp = 500 },
                new() { Timestamp = 1500 },
            },
        };

        macro.CalculateDuration();

        _ = macro.TotalDurationMs.Should().Be(1500);
    }

    [Fact]
    public void Clone_CreatesDetachedCopy()
    {
        var original = new MacroSequence
        {
            Id = Guid.NewGuid(),
            Name = "Original",
            Events = {
                new()
                {
                    Type = EventType.MouseMove,
                    X = 10,
                    Y = 20,
                    Timestamp = 30,
                    DelayMs = 40,
                    CoordinateMode = MouseCoordinateMode.Absolute,
                },
            },
            ScriptSteps = { "move 10 20" },
            TextInputBoundaries =
            {
                new(0, 2, "hello"),
            },
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            TotalDurationMs = 30,
            RecordedAt = new DateTime(2026, 1, 1, 0, 0, 10, DateTimeKind.Utc),
            ActualDuration = TimeSpan.FromSeconds(2),
            MouseMoveCount = 1,
            ClickCount = 0,
            EventsPerSecond = 0.5,
            IsAbsoluteCoordinates = true,
            SkipInitialZeroZero = true,
            TrailingDelayMs = 100,
            HasTrailingRandomDelay = true,
            TrailingDelayMinMs = 50,
            TrailingDelayMaxMs = 150,
        };

        var clone = original.Clone();

        _ = clone.Should().NotBeSameAs(original);
        _ = clone.Should().BeEquivalentTo(original);
        _ = clone.Events.Should().NotBeSameAs(original.Events);
        _ = clone.Events[0].CoordinateMode.Should().Be(MouseCoordinateMode.Absolute);
        _ = clone.ScriptSteps.Should().NotBeSameAs(original.ScriptSteps);
        _ = clone.TextInputBoundaries.Should().NotBeSameAs(original.TextInputBoundaries);

        clone.Name = "Updated";
        clone.Events.Add(new MacroEvent { Type = EventType.KeyPress });
        clone.ScriptSteps.Add("press a");
        clone.TextInputBoundaries.Add(new TextInputBoundary(2, 2, "world"));

        _ = original.Name.Should().Be("Original");
        _ = original.Events.Should().HaveCount(1);
        _ = original.ScriptSteps.Should().ContainSingle();
        _ = original.TextInputBoundaries.Should().ContainSingle();
    }

    [Fact]
    public void NewMacroSequence_HasDefaultValues()
    {
        var macro = new MacroSequence();

        _ = macro.Id.Should().NotBeEmpty();
        _ = macro.Name.Should().Be("Unnamed Macro");
        _ = macro.Events.Should().NotBeNull();
        _ = macro.Events.Should().BeEmpty();
        _ = macro.CreatedAt.Should().NotBe(default);
        _ = macro.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void ReplaceCollections_PreservesCollectionIdentityAndOrdering()
    {
        var macro = new MacroSequence();
        var events = macro.Events;
        var scriptSteps = macro.ScriptSteps;
        var boundaries = macro.TextInputBoundaries;
        var images = macro.Images;

        macro.ReplaceEvents([new MacroEvent { KeyCode = 30 }, new MacroEvent { KeyCode = 31 }]);
        macro.ReplaceScriptSteps(["move 1 2", "click left"]);
        macro.ReplaceTextInputBoundaries([new TextInputBoundary(0, 2, "hi")]);
        macro.ReplaceImages([new KeyValuePair<string, string>("Target", "encoded")]);

        _ = macro.Events.Should().BeSameAs(events);
        _ = macro.ScriptSteps.Should().BeSameAs(scriptSteps);
        _ = macro.TextInputBoundaries.Should().BeSameAs(boundaries);
        _ = macro.Images.Should().BeSameAs(images);
        _ = macro.Events.Select(item => item.KeyCode).Should().Equal(30, 31);
        _ = macro.ScriptSteps.Should().Equal("move 1 2", "click left");
        _ = macro.Images["Target"].Should().Be("encoded");
    }

    [Fact]
    public void ReplaceEvents_WithOwnCollection_KeepsEvents()
    {
        var macro = new MacroSequence { Events = { new MacroEvent { KeyCode = 30 }, new MacroEvent { KeyCode = 31 } } };

        macro.ReplaceEvents(macro.Events);

        _ = macro.Events.Select(item => item.KeyCode).Should().Equal(30, 31);
    }

    [Fact]
    public void ReplaceEvents_WithNull_Throws()
    {
        var macro = new MacroSequence();

        var act = () => macro.ReplaceEvents(null!);

        _ = act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Clone_CopiesActionCommentsAndImagesIndependently()
    {
        var original = new MacroSequence
        {
            ActionComments = { new ActionCommentBoundary(0, 1, 0, 2, "note") },
            Images = { ["Target"] = "encoded" },
        };

        var clone = original.Clone();
        clone.ActionComments.Clear();
        clone.Images["Target"] = "changed";

        _ = original.ActionComments.Should().ContainSingle();
        _ = original.Images["Target"].Should().Be("encoded");
        _ = clone.Images.Should().ContainKey("Target");
    }
}
