
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

/// <summary>
/// IPC client interface for Sway (and i3) protocol.
/// </summary>
public interface ISwayIpcClient : IDisposable
{
    public bool IsAvailable { get; }
    public string? SocketPath { get; }

    /// <summary>
    /// Sends a binary IPC request to Sway and returns the JSON payload response.
    /// </summary>
    public Task<string?> SendRequestAsync(uint type, string payload = "", CancellationToken cancellationToken = default);
}
