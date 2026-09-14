
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.Niri;

public sealed class NiriResponse<T>
{
    [JsonPropertyName("Ok")]
    public T? Ok { get; set; }
}
