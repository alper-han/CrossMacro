
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class PersistedMacroCompatibilityTests
{
    [Fact]
    public void CanonicalSchema_UsesCurrentVersionAndFormat()
    {
        _ = PersistedMacroDocument.CurrentSchemaVersion.Should().Be(2);
        _ = PersistedMacroDocument.CurrentFormat.Should().Be("CrossMacroFormatV2");
        _ = new PersistedMacroDocument().Format.Should().Be("CrossMacroFormatV2");
    }

    [Fact]
    public void CanonicalCodec_RoundTripsMacroSequence()
    {
        var macroEvent = new MacroEvent
        {
            Type = EventType.Click,
            X = 10,
            Y = 20,
            Button = MacroMouseButton.Left,
            CoordinateMode = MouseCoordinateMode.Absolute,
            UseCurrentPosition = true,
        };
        var macro = new MacroSequence { Events = { macroEvent } };

        var persistedEvent = PersistedMacroEvent.FromRuntime(macroEvent);

        _ = persistedEvent.ToRuntime().Should().Be(macroEvent);
        _ = PersistedMacroCodec.Decode(PersistedMacroCodec.Encode(macro))
            .Should().BeEquivalentTo(macro);
    }
}
