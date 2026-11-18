using CrossMacro.Core.Models;

namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class MacroStatisticsTests
{
    [Fact]
    public void Apply_CountsMovesSeparatelyFromMouseButtonEventsAndIgnoresKeys()
    {
        var sequence = new MacroSequence();
        foreach (var type in new[]
        {
            EventType.MouseMove,
            EventType.MouseMove,
            EventType.Click,
            EventType.ButtonPress,
            EventType.ButtonRelease,
            EventType.KeyPress,
            EventType.KeyRelease,
        })
        {
            sequence.Events.Add(new MacroEvent { Type = type });
        }

        MacroStatistics.Apply(sequence);

        _ = sequence.MouseMoveCount.Should().Be(2);
        _ = sequence.ClickCount.Should().Be(3);
    }
}
