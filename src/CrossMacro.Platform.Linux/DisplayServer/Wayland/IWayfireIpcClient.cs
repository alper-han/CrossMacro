
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal interface IWayfireIpcClient : IDisposable
{
    bool IsAvailable { get; }
    string? SocketPath { get; }
    Task<string?> SendRequestAsync(string method, CancellationToken cancellationToken = default);
}
