
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class PersistedMacroCompatibilityTests
{
    [Fact]
    public void CanonicalSchema_UsesCurrentVersionAndFormat()
    {
        _ = PersistedMacroDocument.CurrentSchemaVersion.Should().Be(4);
        _ = PersistedMacroDocument.CurrentFormat.Should().Be("CrossMacroFormatV4");
        _ = new PersistedMacroDocument().Format.Should().Be("CrossMacroFormatV4");
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
            CoordinateSpace = MouseCoordinateSpace.LogicalDesktop,
            UseCurrentPosition = true,
        };
        var macro = new MacroSequence { Events = { macroEvent } };

        var persistedEvent = PersistedMacroEvent.FromRuntime(macroEvent);

        _ = persistedEvent.ToRuntime().Should().Be(macroEvent);
        _ = PersistedMacroCodec.Decode(PersistedMacroCodec.Encode(macro))
            .Should().BeEquivalentTo(macro);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CanonicalCodec_RejectsNonPositiveSchemaVersions(int schemaVersion)
    {
        var document = new PersistedMacroDocument { SchemaVersion = schemaVersion };

        _ = Assert.Throws<InvalidOperationException>(() => PersistedMacroCodec.Decode(document))
            .Message.Should().Contain(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Unsupported macro schema version {0}", schemaVersion));
    }

    [Fact]
    public void PersistedDocument_UsesEmptyRuntimeCollectionsWhenPersistedCollectionsAreNull()
    {
        var document = new PersistedMacroDocument
        {
            Events = null!,
            ScriptSteps = null!,
            TextInputBoundaries = null!,
            Images = null!,
        };

        var runtime = document.ToRuntime();

        _ = runtime.Events.Should().BeEmpty();
        _ = runtime.ScriptSteps.Should().BeEmpty();
        _ = runtime.TextInputBoundaries.Should().BeEmpty();
        _ = runtime.Images.Should().BeEmpty();
    }

    [Fact]
    public void PersistedDocument_RejectsNullPersistedEvent()
    {
        var document = new PersistedMacroDocument
        {
            Events = [null!],
        };

        _ = Assert.Throws<InvalidDataException>(() => document.ToRuntime())
            .Message.Should().Be("Persisted macro event cannot be null.");
    }
}
