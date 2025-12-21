using System.Collections.Generic;
using System.Text.Json.Serialization;
using CrossMacro.Core.Models;

namespace CrossMacro.Infrastructure.Serialization;

/// <summary>
/// JSON serialization context for trim-safe serialization
/// This uses System.Text.Json source generators to avoid reflection
/// </summary>
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(HotkeySettings))]
[JsonSerializable(typeof(TextExpansion))]
[JsonSerializable(typeof(List<TextExpansion>))]
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
public partial class CrossMacroJsonContext : JsonSerializerContext
{
}
