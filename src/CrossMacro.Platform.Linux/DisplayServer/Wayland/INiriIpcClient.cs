namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal interface INiriIpcClient : IDisposable
{
    public bool IsAvailable { get; }

    public string? SocketPath { get; }

    public Task<string?> SendRequestAsync(string requestJson, CancellationToken cancellationToken = default);
}
