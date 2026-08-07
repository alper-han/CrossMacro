using System.Text.Json.Serialization;

namespace CrossMacro.UI.Themes;

[JsonSerializable(typeof(ThemeFileDocument))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
internal sealed partial class ThemeJsonContext : JsonSerializerContext;
