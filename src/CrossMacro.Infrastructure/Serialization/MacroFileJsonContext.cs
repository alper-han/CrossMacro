using System.Text.Json.Serialization;
using CrossMacro.Core.Models.Macros;

namespace CrossMacro.Infrastructure.Serialization;

/// <summary>
/// Source-generated JSON metadata for the custom .macro file format.
/// Uses default naming to preserve existing embedded metadata casing.
/// </summary>

[JsonSerializable(typeof(TextInputBoundary))]
[JsonSerializable(typeof(ActionCommentBoundary))]
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
internal sealed partial class MacroFileJsonContext : JsonSerializerContext;
