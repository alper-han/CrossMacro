namespace CrossMacro.Infrastructure.Tests.Services.Scripting;

public sealed class RunScriptEventTimelineTests
{
    [Fact]
    public void AddDelay_BeforeTheFirstEvent_AccumulatesTheInitialDelay()
    {
        var timeline = new RunScriptEventTimeline();

        timeline.AddDelay(fixedMicroseconds: 2_000, hasRandom: false, randomMinMs: 0, randomMaxMs: 0);
        timeline.AddDelay(fixedMicroseconds: 1_000, hasRandom: true, randomMinMs: 3, randomMaxMs: 5);
        timeline.AddDelay(fixedMicroseconds: 0, hasRandom: true, randomMinMs: 1, randomMaxMs: 2);

        _ = timeline.Initial.Should().Be(new RunScriptEventTimeline.Delay(3_000, HasRandom: true, RandomMinMs: 4, RandomMaxMs: 7));
        _ = timeline.Trailing.Should().Be(default(RunScriptEventTimeline.Delay));
    }

    [Fact]
    public void Emit_AppliesThePendingDelayToTheEventAndAdvancesTheTimestamp()
    {
        var timeline = new RunScriptEventTimeline();
        timeline.Emit(new MacroEvent { Type = EventType.KeyPress, KeyCode = 30 });

        timeline.AddDelay(fixedMicroseconds: 10_000, hasRandom: true, randomMinMs: 4, randomMaxMs: 6);
        timeline.Emit(new MacroEvent { Type = EventType.KeyRelease, KeyCode = 30 });

        var release = timeline.Sequence.Events[1];
        _ = release.DelayMicroseconds.Should().Be(10_000);
        _ = (release.HasRandomDelay, release.RandomDelayMinMs, release.RandomDelayMaxMs).Should().Be((true, 4, 6));
        _ = release.TimestampMicroseconds.Should().Be(10_000 + (4 * MacroTiming.MicrosecondsPerMillisecond));
        _ = timeline.Trailing.Should().Be(default(RunScriptEventTimeline.Delay));
    }

    [Fact]
    public void AddDelay_AfterAnEvent_StaysPendingAsTheTrailingDelay()
    {
        var timeline = new RunScriptEventTimeline();
        timeline.Emit(new MacroEvent { Type = EventType.Click, Button = MacroMouseButton.Left });

        timeline.AddDelay(fixedMicroseconds: 7_000, hasRandom: false, randomMinMs: 0, randomMaxMs: 0);

        _ = timeline.Trailing.FixedMicroseconds.Should().Be(7_000);
        _ = timeline.Initial.Should().Be(default(RunScriptEventTimeline.Delay));
        _ = timeline.HasEvents.Should().BeTrue();
    }
}
