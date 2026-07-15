
namespace CrossMacro.Infrastructure.Persistence.Macros;

public static class PersistedMacroCodec
{
    public static PersistedMacroDocument Encode(MacroSequence macro) => PersistedMacroDocument.FromRuntime(macro);

    public static MacroSequence Decode(PersistedMacroDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.SchemaVersion > PersistedMacroDocument.CurrentSchemaVersion)
        {
            throw new InvalidOperationException($"Unsupported macro schema version {document.SchemaVersion}.");
        }

        return document.ToRuntime();
    }
}
