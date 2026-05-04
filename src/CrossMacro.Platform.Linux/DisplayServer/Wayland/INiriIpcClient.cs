namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal interface INiriIpcClient : IDisposable
{
    bool IsAvailable { get; }

    string? SocketPath { get; }

    Task<string?> SendRequestAsync(string requestJson, CancellationToken cancellationToken = default);
}
