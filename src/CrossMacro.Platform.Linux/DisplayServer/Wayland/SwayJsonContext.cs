using System.Text.Json.Serialization;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

[JsonSerializable(typeof(SwayNodeDto))]
[JsonSerializable(typeof(SwayWorkspaceDto[]))]
[JsonSerializable(typeof(SwayOutputDto[]))]
[JsonSerializable(typeof(SwayCommandResultDto[]))]
internal sealed partial class SwayJsonContext : JsonSerializerContext
{
}
