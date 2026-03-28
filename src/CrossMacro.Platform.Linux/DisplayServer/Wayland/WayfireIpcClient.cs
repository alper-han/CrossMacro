using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Serilog;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal interface IWayfireIpcClient : IDisposable
{
    bool IsAvailable { get; }
    string? SocketPath { get; }
    Task<string?> SendRequestAsync(string method, CancellationToken cancellationToken = default);
}

/// <summary>
/// Shared IPC client for communicating with Wayfire via Unix socket.
/// Protocol uses a 4-byte little-endian length prefix followed by JSON payload.
/// </summary>
public sealed class WayfireIpcClient : IWayfireIpcClient
{
    private const int SocketTimeoutMs = 1000;
    private const int MaxResponseBytes = 4 * 1024 * 1024;
    private static readonly TimeSpan SocketValidationTimeout = TimeSpan.FromMilliseconds(250);
    private const string WayfireSocketEnvVar = "WAYFIRE_SOCKET";
    private const string RuntimeDirEnvVar = "XDG_RUNTIME_DIR";
    private const string CandidatePattern = "wayfire-wayland-*.socket";

    private readonly Func<string, string?> _getEnvironmentVariable;
    private readonly Func<string, bool> _fileExists;
    private readonly Func<string, bool> _directoryExists;
    private readonly Func<string, string, string[]> _getFiles;
    private readonly Func<string, bool> _canConnectSocket;

    private readonly string? _socketPath;
    private bool _disposed;

    public bool IsAvailable { get; }
    public string? SocketPath => _socketPath;

    public WayfireIpcClient()
        : this(
            Environment.GetEnvironmentVariable,
            File.Exists,
            Directory.Exists,
            Directory.GetFiles)
    {
    }

    internal WayfireIpcClient(
        Func<string, string?> getEnvironmentVariable,
        Func<string, bool> fileExists,
        Func<string, bool> directoryExists,
        Func<string, string, string[]> getFiles)
        : this(getEnvironmentVariable, fileExists, directoryExists, getFiles, CanConnectSocket)
    {
    }

    internal WayfireIpcClient(
        Func<string, string?> getEnvironmentVariable,
        Func<string, bool> fileExists,
        Func<string, bool> directoryExists,
        Func<string, string, string[]> getFiles,
        Func<string, bool> canConnectSocket)
    {
        _getEnvironmentVariable = getEnvironmentVariable ?? throw new ArgumentNullException(nameof(getEnvironmentVariable));
        _fileExists = fileExists ?? throw new ArgumentNullException(nameof(fileExists));
        _directoryExists = directoryExists ?? throw new ArgumentNullException(nameof(directoryExists));
        _getFiles = getFiles ?? throw new ArgumentNullException(nameof(getFiles));
        _canConnectSocket = canConnectSocket ?? throw new ArgumentNullException(nameof(canConnectSocket));

        _socketPath = DiscoverSocketPath();
        IsAvailable = !string.IsNullOrWhiteSpace(_socketPath);

        if (IsAvailable)
        {
            Log.Information("[WayfireIpcClient] Socket found: {SocketPath}", _socketPath);
        }
        else
        {
            Log.Debug("[WayfireIpcClient] Wayfire socket not available");
        }
    }

    public async Task<string?> SendRequestAsync(string method, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);

        if (_disposed || !IsAvailable || _socketPath == null)
        {
            return null;
        }

        try
        {
            return await SendRequestInternalAsync(method, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Log.Debug("[WayfireIpcClient] Request timed out for method: {Method}", method);
            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[WayfireIpcClient] Failed to send request: {Method}", method);
            return null;
        }
    }

    private async Task<string> SendRequestInternalAsync(string method, CancellationToken cancellationToken)
    {
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        using var timeoutCts = new CancellationTokenSource(SocketTimeoutMs);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var endpoint = new UnixDomainSocketEndPoint(_socketPath!);
        await socket.ConnectAsync(endpoint, linkedCts.Token).ConfigureAwait(false);

        var requestPayload = BuildRequestPayload(method);

        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, requestPayload.Length);

        await SendAllAsync(socket, header, linkedCts.Token).ConfigureAwait(false);
        await SendAllAsync(socket, requestPayload, linkedCts.Token).ConfigureAwait(false);

        var responseHeader = new byte[4];
        await ReadExactAsync(socket, responseHeader, linkedCts.Token).ConfigureAwait(false);

        int responseLength = BinaryPrimitives.ReadInt32LittleEndian(responseHeader);
        if (responseLength <= 0 || responseLength > MaxResponseBytes)
        {
            throw new InvalidDataException($"Invalid Wayfire IPC response length: {responseLength}");
        }

        var responsePayload = new byte[responseLength];
        await ReadExactAsync(socket, responsePayload, linkedCts.Token).ConfigureAwait(false);

        return Encoding.UTF8.GetString(responsePayload);
    }

    private string? DiscoverSocketPath()
    {
        var directSocket = _getEnvironmentVariable(WayfireSocketEnvVar);
        if (IsSocketPathUsable(directSocket))
        {
            return directSocket!.Trim();
        }

        foreach (var candidate in EnumerateCandidateSockets())
        {
            if (IsSocketPathUsable(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private IEnumerable<string> EnumerateCandidateSockets()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var candidate in EnumerateCandidateSocketsInDirectory(_getEnvironmentVariable(RuntimeDirEnvVar)))
        {
            if (seen.Add(candidate))
            {
                yield return candidate;
            }
        }

        foreach (var candidate in EnumerateCandidateSocketsInDirectory(Path.GetTempPath()))
        {
            if (seen.Add(candidate))
            {
                yield return candidate;
            }
        }
    }

    private IEnumerable<string> EnumerateCandidateSocketsInDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !_directoryExists(directory))
        {
            yield break;
        }

        string[] files;
        try
        {
            files = _getFiles(directory, CandidatePattern);
        }
        catch
        {
            yield break;
        }

        Array.Sort(files, StringComparer.Ordinal);

        foreach (var file in files)
        {
            yield return file;
        }
    }

    private bool IsSocketPathUsable(string? socketPath)
    {
        if (string.IsNullOrWhiteSpace(socketPath))
        {
            return false;
        }

        var trimmedPath = socketPath.Trim();
        if (!_fileExists(trimmedPath))
        {
            return false;
        }

        return _canConnectSocket(trimmedPath);
    }

    private static bool CanConnectSocket(string socketPath)
    {
        try
        {
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            using var cts = new CancellationTokenSource(SocketValidationTimeout);
            socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), cts.Token).GetAwaiter().GetResult();
            return socket.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static async Task SendAllAsync(Socket socket, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        int sentTotal = 0;
        while (sentTotal < payload.Length)
        {
            int sent = await socket.SendAsync(payload.Slice(sentTotal), SocketFlags.None, cancellationToken).ConfigureAwait(false);
            if (sent <= 0)
            {
                throw new IOException("Failed to write to Wayfire socket.");
            }

            sentTotal += sent;
        }
    }

    private static async Task ReadExactAsync(Socket socket, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        int readTotal = 0;
        while (readTotal < buffer.Length)
        {
            int read = await socket.ReceiveAsync(buffer.Slice(readTotal), SocketFlags.None, cancellationToken).ConfigureAwait(false);
            if (read <= 0)
            {
                throw new IOException("Unexpected EOF while reading Wayfire socket response.");
            }

            readTotal += read;
        }
    }

    private static ReadOnlyMemory<byte> BuildRequestPayload(string method)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);

        writer.WriteStartObject();
        writer.WriteString("method", method);
        writer.WritePropertyName("data");
        writer.WriteStartObject();
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.Flush();

        return buffer.WrittenMemory;
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
