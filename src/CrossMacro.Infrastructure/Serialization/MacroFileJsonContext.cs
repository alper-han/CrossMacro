using System.Text.Json.Serialization;
using CrossMacro.Core.Models;

namespace CrossMacro.Infrastructure.Serialization;

/// <summary>
/// Source-generated JSON metadata for the custom .macro file format.
/// Uses default naming to preserve existing embedded metadata casing.
/// </summary>
[JsonSerializable(typeof(TextInputBoundary))]
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
internal partial class MacroFileJsonContext : JsonSerializerContext
{
}
