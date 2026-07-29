
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

/// <summary>
/// IPC client for Sway's binary socket protocol (i3-ipc).
/// </summary>
public sealed class SwayIpcClient : ISwayIpcClient
{
    private const string SocketPathEnvironmentVariable = "SWAYSOCK";
    private const int SocketTimeoutMs = 2000;

    // Magic string "i3-ipc"
    private static readonly byte[] MagicString = [0x69, 0x33, 0x2d, 0x69, 0x70, 0x63];
    private bool _disposed;

    public SwayIpcClient()
        : this(Environment.GetEnvironmentVariable(SocketPathEnvironmentVariable)) { /* Empty */ }

    public SwayIpcClient(LinuxEnvironmentSnapshot environment)
        : this(environment.SwaySocket) { /* Empty */ }

    internal SwayIpcClient(string? socketPath)
    {
        SocketPath = socketPath;
        IsAvailable = !string.IsNullOrWhiteSpace(SocketPath) && File.Exists(SocketPath);

        if (IsAvailable)
        {
            Log.Information("[SwayIpcClient] Socket found: {SocketPath}", SocketPath);
        }
        else
        {
            Log.Debug("[SwayIpcClient] Sway socket not available");
        }
    }

    public bool IsAvailable { get; }

    public string? SocketPath { get; }

    public async Task<string?> SendRequestAsync(uint type, string payload = "", CancellationToken cancellationToken = default)
    {
        if (_disposed || !IsAvailable || SocketPath is null)
        {
            return null;
        }

        try
        {
            return await SendRequestCoreAsync(type, payload, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[SwayIpcClient] Failed to send IPC request");
            return null;
        }
    }

    private async Task<string?> SendRequestCoreAsync(uint type, string payload, CancellationToken cancellationToken)
    {
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        using var timeoutCts = new CancellationTokenSource(SocketTimeoutMs);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var endpoint = new UnixDomainSocketEndPoint(SocketPath!);
            await socket.ConnectAsync(endpoint, linkedCts.Token).ConfigureAwait(false);

            await SendRequestPayloadAsync(socket, type, payload, linkedCts.Token).ConfigureAwait(false);

            return await ReadRequestResponseAsync(socket, linkedCts.Token).ConfigureAwait(false);
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
                    // Ignore shutdown errors during cleanup
                }
            }
        }
    }

    private static async Task SendRequestPayloadAsync(Socket socket, uint type, string payload, CancellationToken ct)
    {
        var payloadBytes = string.IsNullOrEmpty(payload) ? [] : Encoding.UTF8.GetBytes(payload);
        var header = new byte[14];

        Buffer.BlockCopy(MagicString, 0, header, 0, 6);
        Buffer.BlockCopy(BitConverter.GetBytes((uint)payloadBytes.Length), 0, header, 6, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(type), 0, header, 10, 4);

        _ = await socket.SendAsync(header, SocketFlags.None, ct).ConfigureAwait(false);
        if (payloadBytes.Length > 0)
        {
            _ = await socket.SendAsync(payloadBytes, SocketFlags.None, ct).ConfigureAwait(false);
        }
    }

    private static async Task<string?> ReadRequestResponseAsync(Socket socket, CancellationToken ct)
    {
        var resHeader = new byte[14];
        int receivedHeader = await ReadExactlyAsync(socket, resHeader, 14, ct).ConfigureAwait(false);
        if (receivedHeader < 14)
        {
            Log.Warning("[SwayIpcClient] Incomplete response header received");
            return null;
        }

        for (int i = 0; i < 6; i++)
        {
            if (resHeader[i] != MagicString[i])
            {
                Log.Warning("[SwayIpcClient] Invalid magic string in response");
                return null;
            }
        }

        uint resLength = BitConverter.ToUInt32(resHeader, 6);
        if (resLength == 0)
        {
            return string.Empty;
        }

        var resPayload = new byte[resLength];
        int receivedPayload = await ReadExactlyAsync(socket, resPayload, (int)resLength, ct).ConfigureAwait(false);

        if (receivedPayload < resLength)
        {
            Log.Warning("[SwayIpcClient] Incomplete response payload received");
            return null;
        }

        return Encoding.UTF8.GetString(resPayload);
    }

    private static async Task<int> ReadExactlyAsync(Socket socket, byte[] buffer, int count, CancellationToken cancellationToken)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = await socket.ReceiveAsync(new Memory<byte>(buffer, totalRead, count - totalRead), SocketFlags.None, cancellationToken).ConfigureAwait(false);
            if (read is 0)
            {
                break; // Connection closed
            }
            totalRead += read;
        }
        return totalRead;
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
