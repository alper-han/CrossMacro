using System.Text.Json.Serialization;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public sealed class NiriResponse<T>
{
    [JsonPropertyName("Ok")]
    public T? Ok { get; set; }
}
