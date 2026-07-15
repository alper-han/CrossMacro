
namespace CrossMacro.Infrastructure.Tests.Services;

public class PersistedMacroCompatibilityTests
{
    [Fact]
    public void CanonicalSchema_UsesCurrentVersionAndFormat()
    {
        Canonical.PersistedMacroDocument.CurrentSchemaVersion.Should().Be(2);
        Canonical.PersistedMacroDocument.CurrentFormat.Should().Be("CrossMacroFormatV2");
        new Canonical.PersistedMacroDocument().Format.Should().Be("CrossMacroFormatV2");
    }

    [Fact]
    public void CompatibilityTypes_PreserveInheritanceAndForwardingFactories()
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

        var document = Compatibility.PersistedMacroDocument.FromRuntime(macro);
        var persistedEvent = Compatibility.PersistedMacroEvent.FromRuntime(macroEvent);

        document.Should().BeAssignableTo<Canonical.PersistedMacroDocument>();
        document.Events.Should().ContainSingle().Which.Should().BeAssignableTo<Canonical.PersistedMacroEvent>();
        persistedEvent.Should().BeAssignableTo<Canonical.PersistedMacroEvent>();
        persistedEvent.ToRuntime().Should().Be(macroEvent);
        Compatibility.PersistedMacroCodec.Decode(Compatibility.PersistedMacroCodec.Encode(macro))
            .Should().BeEquivalentTo(macro);
    }
}
