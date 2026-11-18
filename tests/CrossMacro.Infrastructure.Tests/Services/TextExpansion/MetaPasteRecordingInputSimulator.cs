namespace CrossMacro.Infrastructure.Tests.Services.TextExpansion;

internal sealed class MetaPasteRecordingInputSimulator(List<string> events)
    : RecordingInputSimulator(events), IPlatformPasteShortcutProvider
{
    public bool UsesMetaKeyForStandardPaste => true;
}
