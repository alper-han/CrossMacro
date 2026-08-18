
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal interface IWayfireIpcClient : IDisposable
{
    public bool IsAvailable { get; }
    public string? SocketPath { get; }
    public Task<string?> SendRequestAsync(string method, CancellationToken cancellationToken = default);
}
