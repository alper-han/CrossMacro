
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

/// <summary>
/// Minimal IPC client for Niri's JSON socket protocol.
/// </summary>
internal sealed class NiriIpcClient : INiriIpcClient
{
    private const string SocketPathEnvironmentVariable = "NIRI_SOCKET";
    private const string RuntimeDirectoryEnvironmentVariable = "XDG_RUNTIME_DIR";
    private const int SocketTimeoutMs = 1000;
    private const int BufferSize = 8192;
    private bool _disposed;

    public NiriIpcClient()
        : this(
            Environment.GetEnvironmentVariable(SocketPathEnvironmentVariable),
            Environment.GetEnvironmentVariable(RuntimeDirectoryEnvironmentVariable))
    { /* Empty */ }

    public NiriIpcClient(LinuxEnvironmentSnapshot environment)
        : this(environment.NiriSocket, environment.RuntimeDir) { /* Empty */ }

    internal NiriIpcClient(string? socketPath)
        : this(socketPath, Environment.GetEnvironmentVariable(RuntimeDirectoryEnvironmentVariable)) { /* Empty */ }

    internal NiriIpcClient(string? socketPath, string? runtimeDirectory)
    {
        SocketPath = TryNormalizeSocketPath(socketPath, runtimeDirectory, out var normalizedSocketPath)
            ? normalizedSocketPath
            : null;
        IsAvailable = SocketPath is not null && File.Exists(SocketPath);

        if (IsAvailable)
        {
            Log.Information("[NiriIpcClient] Socket found: {SocketPath}", SocketPath);
        }
        else
        {
            Log.Debug("[NiriIpcClient] Niri socket not available");
        }
    }

    public bool IsAvailable { get; }

    public string? SocketPath { get; }

    internal static bool TryNormalizeSocketPath(string? socketPath, string? runtimeDirectory, out string? normalizedSocketPath)
    {
        normalizedSocketPath = null;

        if (string.IsNullOrWhiteSpace(socketPath) || string.IsNullOrWhiteSpace(runtimeDirectory))
        {
            return false;
        }

        try
        {
            var normalizedRuntimeDirectory = Path.GetFullPath(runtimeDirectory);
            var normalizedCandidate = Path.GetFullPath(socketPath);

            if (!Path.IsPathRooted(normalizedCandidate) || !Path.IsPathRooted(normalizedRuntimeDirectory))
            {
                return false;
            }

            var runtimeDirectoryWithSeparator = EnsureTrailingSeparator(normalizedRuntimeDirectory);
            if (!normalizedCandidate.StartsWith(runtimeDirectoryWithSeparator, StringComparison.Ordinal))
            {
                return false;
            }

            normalizedSocketPath = normalizedCandidate;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static string EnsureTrailingSeparator(string directory)
    {
        return directory.EndsWith(Path.DirectorySeparatorChar)
            ? directory
            : directory + Path.DirectorySeparatorChar;
    }

    public async Task<string?> SendRequestAsync(string requestJson, CancellationToken cancellationToken = default)
    {
        if (_disposed || !IsAvailable || SocketPath is null)
        {
            return null;
        }

        try
        {
            return await SendRequestCoreAsync(requestJson, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[NiriIpcClient] Failed to send IPC request");
            return null;
        }
    }

    private async Task<string> SendRequestCoreAsync(string requestJson, CancellationToken cancellationToken)
    {
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        using var timeoutCts = new CancellationTokenSource(SocketTimeoutMs);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var endpoint = new UnixDomainSocketEndPoint(SocketPath!);
            await socket.ConnectAsync(endpoint, linkedCts.Token).ConfigureAwait(false);

            var request = requestJson.EndsWith('\n') ? requestJson : requestJson + "\n";
            var requestBytes = Encoding.UTF8.GetBytes(request);
            _ = await socket.SendAsync(requestBytes, SocketFlags.None, linkedCts.Token).ConfigureAwait(false);

            var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
            try
            {
                using var ms = new MemoryStream();
                while (true)
                {
                    var received = await socket.ReceiveAsync(
                        new Memory<byte>(buffer, 0, BufferSize),
                        SocketFlags.None,
                        linkedCts.Token).ConfigureAwait(false);

                    if (received <= 0)
                    {
                        break;
                    }

                    await ms.WriteAsync(buffer.AsMemory(0, received), linkedCts.Token).ConfigureAwait(false);
                    if (EndsWithNewLine(buffer, received))
                    {
                        break;
                    }
                }

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
                    // Ignore shutdown errors during cleanup.
                }
            }
        }
    }

    private static bool EndsWithNewLine(byte[] buffer, int length)
    {
        return length > 0 && buffer[length - 1] == (byte)'\n';
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
