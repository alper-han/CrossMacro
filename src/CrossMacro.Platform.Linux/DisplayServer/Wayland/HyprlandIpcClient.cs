
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

/// <summary>
/// Shared IPC client for communicating with Hyprland compositor via Unix socket.
/// </summary>
public sealed class HyprlandIpcClient : IDisposable
{
    private const int SocketTimeoutMs = 1000;
    private const int BufferSize = 4096;
    private bool _disposed;

    /// <summary>
    /// Indicates whether Hyprland IPC is available on this system.
    /// </summary>
    public bool IsAvailable { get; }

    /// <summary>
    /// Gets the socket path used for communication.
    /// </summary>
    public string? SocketPath { get; }

    public HyprlandIpcClient()
    {
        SocketPath = DiscoverSocketPath();
        IsAvailable = SocketPath is not null;

        if (IsAvailable)
        {
            Log.Information("[HyprlandIpcClient] Socket found: {SocketPath}", SocketPath);
        }
        else
        {
            Log.Debug("[HyprlandIpcClient] Hyprland socket not available");
        }
    }

    public HyprlandIpcClient(LinuxEnvironmentSnapshot environment)
    {
        SocketPath = DiscoverSocketPath(environment.HyprlandInstanceSignature, environment.RuntimeDir);
        IsAvailable = SocketPath is not null;

        if (IsAvailable)
        {
            Log.Information("[HyprlandIpcClient] Socket found: {SocketPath}", SocketPath);
        }
        else
        {
            Log.Debug("[HyprlandIpcClient] Hyprland socket not available");
        }
    }

    /// <summary>
    /// Sends a command to Hyprland and returns the response.
    /// </summary>
    /// <param name="command">The command to send (e.g., "cursorpos", "monitors", "devices")</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    /// <returns>The response string, or null if unavailable/failed</returns>
    public async Task<string?> SendCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        if (_disposed || !IsAvailable || SocketPath is null)
        {
            return null;
        }

        try
        {
            return await SendCommandInternalAsync(Encoding.UTF8.GetBytes(command), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[HyprlandIpcClient] Failed to send command: {Command}", command);
            return null;
        }
    }

    /// <summary>
    /// Sends a pre-encoded command for performance-critical paths.
    /// </summary>
    public async Task<string?> SendCommandAsync(byte[] commandBytes, CancellationToken cancellationToken = default)
    {
        if (_disposed || !IsAvailable || SocketPath is null)
        {
            return null;
        }

        try
        {
            return await SendCommandInternalAsync((ReadOnlyMemory<byte>)commandBytes, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[HyprlandIpcClient] Failed to send command");
            return null;
        }
    }

    /// <summary>
    /// Sends a pre-encoded command for performance-critical paths.
    /// </summary>
    public async Task<string?> SendCommandAsync(ReadOnlyMemory<byte> commandBytes, CancellationToken cancellationToken = default)
    {
        if (_disposed || !IsAvailable || SocketPath is null)
        {
            return null;
        }

        try
        {
            return await SendCommandInternalAsync(commandBytes, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[HyprlandIpcClient] Failed to send command");
            return null;
        }
    }

    private async Task<string> SendCommandInternalAsync(ReadOnlyMemory<byte> commandBytes, CancellationToken cancellationToken)
    {
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        using var timeoutCts = new CancellationTokenSource(SocketTimeoutMs);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var endpoint = new UnixDomainSocketEndPoint(SocketPath!);

            // Connect
            await socket.ConnectAsync(endpoint, linkedCts.Token).ConfigureAwait(false);

            // Send command
            _ = await socket.SendAsync(commandBytes, SocketFlags.None, linkedCts.Token).ConfigureAwait(false);

            // Read response using ArrayPool to reduce allocations
            var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
            try
            {
                using var ms = new MemoryStream();
                int received;

                do
                {
                    received = await socket.ReceiveAsync(
                        new Memory<byte>(buffer, 0, BufferSize),
                        SocketFlags.None,
                        linkedCts.Token).ConfigureAwait(false);

                    if (received > 0)
                    {
                        await ms.WriteAsync(buffer.AsMemory(0, received), linkedCts.Token).ConfigureAwait(false);
                    }
                } while (received == BufferSize);

                return Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length).Trim();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        finally
        {
            if (socket.Connected)
            {
                try
                {
                    socket.Shutdown(SocketShutdown.Both);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    // Ignore shutdown errors
                }
            }
        }
    }

    private static string? DiscoverSocketPath() => DiscoverSocketPath(
        Environment.GetEnvironmentVariable("HYPRLAND_INSTANCE_SIGNATURE"),
        Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR"));

    private static string? DiscoverSocketPath(string? instanceSignature, string? runtimeDir)
    {
        // Check if running on Hyprland
        if (string.IsNullOrEmpty(instanceSignature))
        {
            return null;
        }

        if (string.IsNullOrEmpty(runtimeDir))
        {
            return null;
        }

        var hyprDir = Path.Combine(runtimeDir, "hypr");
        if (!Directory.Exists(hyprDir))
        {
            return null;
        }

        try
        {
            foreach (var instanceDir in Directory.GetDirectories(hyprDir))
            {
                var socketPath = Path.Combine(instanceDir, ".socket.sock");
                if (File.Exists(socketPath))
                {
                    return socketPath;
                }
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Debug(ex, "[HyprlandIpcClient] Error searching for socket");
        }

        return null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
