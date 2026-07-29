
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public sealed class NiriWindowsData
{
    [JsonPropertyName("Windows")]
    public IReadOnlyList<NiriWindowDto>? Windows { get; set; }
}
