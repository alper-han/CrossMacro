
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
[JsonSerializable(typeof(TriggerTask))]
[JsonSerializable(typeof(List<TriggerTask>))]
[JsonSerializable(typeof(GlobalSettings))]
[JsonSerializable(typeof(ProfileSettings))]
[JsonSerializable(typeof(ProfileInfo))]
[JsonSerializable(typeof(List<ProfileInfo>))]
[JsonSerializable(typeof(ProfileRegistry))]
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
public partial class CrossMacroJsonContext : JsonSerializerContext;
