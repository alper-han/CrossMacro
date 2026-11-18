
namespace CrossMacro.Cli.Serialization;

public sealed record HeadlessRuntimeData(
    bool GlobalHotkeys,
    bool Scheduler,
    bool Shortcuts,
    bool TextExpansion,
    bool HotkeyActions
);
