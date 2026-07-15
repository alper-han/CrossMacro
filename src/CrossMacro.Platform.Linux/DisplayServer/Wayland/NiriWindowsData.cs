
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public sealed class NiriWindowsData
{
    [JsonPropertyName("Windows")]
    public NiriWindowDto[]? Windows { get; set; }
}
