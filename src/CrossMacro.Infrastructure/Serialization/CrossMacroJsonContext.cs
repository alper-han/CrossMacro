
using CrossMacro.Infrastructure.Persistence.Settings;
using CrossMacro.Infrastructure.Persistence.Macros;

namespace CrossMacro.Infrastructure.Serialization;

/// <summary>
/// JSON serialization context for trim-safe serialization
/// This uses System.Text.Json source generators to avoid reflection
/// </summary>
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(HotkeySettings))]
[JsonSerializable(typeof(TextExpansionEntry))]
[JsonSerializable(typeof(List<TextExpansionEntry>))]
[JsonSerializable(typeof(ScheduledTask))]
[JsonSerializable(typeof(List<ScheduledTask>))]
[JsonSerializable(typeof(ShortcutTask))]
[JsonSerializable(typeof(List<ShortcutTask>))]
[JsonSerializable(typeof(ShortcutWindowRule))]
[JsonSerializable(typeof(List<ShortcutWindowRule>))]
[JsonSerializable(typeof(TriggerTask))]
[JsonSerializable(typeof(List<TriggerTask>))]
[JsonSerializable(typeof(GlobalSettings))]
[JsonSerializable(typeof(ProfileSettings))]
[JsonSerializable(typeof(PersistedGlobalSettings))]
[JsonSerializable(typeof(PersistedProfileSettings))]
[JsonSerializable(typeof(ProfileInfo))]
[JsonSerializable(typeof(List<ProfileInfo>))]
[JsonSerializable(typeof(ProfileRegistry))]
[JsonSerializable(typeof(PersistedMacroDocument))]
[JsonSerializable(typeof(PersistedLoadedMacroSession))]
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
public sealed partial class CrossMacroJsonContext : JsonSerializerContext;
