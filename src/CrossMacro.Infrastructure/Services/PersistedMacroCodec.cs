
namespace CrossMacro.Infrastructure.Services;

public static class PersistedMacroCodec
{
    public static PersistedMacroDocument Encode(MacroSequence macro) => PersistedMacroDocument.FromRuntime(macro);

    public static MacroSequence Decode(PersistedMacroDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Canonical.PersistedMacroCodec.Decode(document);
    }
}
