using System;
using System.Buffers;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Services;
using Serilog;

namespace CrossMacro.Infrastructure.Wayland
{
    /// <summary>
    /// Mouse position provider for Hyprland compositor using IPC socket
    /// </summary>
    public class HyprlandPositionProvider : IMousePositionProvider
    {
        private const int SocketTimeoutMs = 1000;
        private const int BufferSize = 4096;
        
        // Cached command byte arrays to avoid repeated encoding
        private static readonly byte[] CursorPosCommand = Encoding.UTF8.GetBytes("cursorpos");
        private static readonly byte[] MonitorsCommand = Encoding.UTF8.GetBytes("monitors");
        
        private readonly string? _socketPath;
        private bool _disposed;

        public bool IsSupported { get; }
        public string ProviderName => "Hyprland IPC";

        public HyprlandPositionProvider()
        {
            // Detect Hyprland socket path
            // First try to find the socket directory directly
            var runtimeDir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
            
            if (string.IsNullOrEmpty(runtimeDir))
            {
                IsSupported = false;
                Log.Warning("[HyprlandPositionProvider] XDG_RUNTIME_DIR not found");
                return;
            }

            var hyprDir = Path.Combine(runtimeDir, "hypr");
            
            if (!Directory.Exists(hyprDir))
            {
                IsSupported = false;
                Log.Warning("[HyprlandPositionProvider] Hyprland directory not found: {HyprDir}", hyprDir);
                return;
            }

            // Find the first available socket (there should only be one active instance)
            try
            {
                var instanceDirs = Directory.GetDirectories(hyprDir);
                foreach (var instanceDir in instanceDirs)
                {
                    var socketPath = Path.Combine(instanceDir, ".socket.sock");
                    if (File.Exists(socketPath))
                    {
                        _socketPath = socketPath;
                        IsSupported = true;
                        Log.Information("[HyprlandPositionProvider] Socket found: {SocketPath}", _socketPath);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[HyprlandPositionProvider] Error searching for Hyprland socket");
            }

            IsSupported = false;
            Log.Warning("[HyprlandPositionProvider] No Hyprland socket found in {HyprDir}", hyprDir);
        }

        public async Task<(int X, int Y)?> GetAbsolutePositionAsync()
        {
            if (_disposed || !IsSupported || _socketPath == null)
                return null;

            try
            {
                var response = await SendCommandAsync(CursorPosCommand).ConfigureAwait(false);
                return ParseCursorPosition(response);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[HyprlandPositionProvider] Failed to get cursor position");
                return null;
            }
        }

        public async Task<(int Width, int Height)?> GetScreenResolutionAsync()
        {
            if (_disposed || !IsSupported || _socketPath == null)
                return null;

            try
            {
                var response = await SendCommandAsync(MonitorsCommand).ConfigureAwait(false);
                return ParseMonitors(response);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[HyprlandPositionProvider] Failed to get screen resolution");
                return null;
            }
        }

        private async Task<string> SendCommandAsync(byte[] commandBytes)
        {
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            using var cts = new CancellationTokenSource(SocketTimeoutMs);
            
            try
            {
                var endpoint = new UnixDomainSocketEndPoint(_socketPath!);
                
                // Connect
                await socket.ConnectAsync(endpoint, cts.Token).ConfigureAwait(false);

                // Send command (byte array is pre-encoded for performance)
                await socket.SendAsync(commandBytes, SocketFlags.None, cts.Token).ConfigureAwait(false);

                // Read response using ArrayPool to reduce allocations
                var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
                try
                {
                    using var ms = new MemoryStream();
                    int received;
                    
                    // Read until we get less than buffer size (indicating end of data)
                    // or timeout occurs (handled by CancellationToken)
                    do
                    {
                        received = await socket.ReceiveAsync(new Memory<byte>(buffer, 0, BufferSize), SocketFlags.None, cts.Token).ConfigureAwait(false);
                        if (received > 0)
                        {
                            await ms.WriteAsync(buffer.AsMemory(0, received), cts.Token).ConfigureAwait(false);
                        }
                    } while (received == BufferSize);

                    // Use GetBuffer() to avoid extra allocation, but need to use Length for correct size
                    return Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length).Trim();
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
            finally
            {
                // Gracefully shutdown if still connected
                if (socket.Connected)
                {
                    try
                    {
                        socket.Shutdown(SocketShutdown.Both);
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "[HyprlandPositionProvider] Socket shutdown error");
                    }
                }
            }
        }

        private (int Width, int Height)? ParseMonitors(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
                return null;

            int maxWidth = 0;
            int maxHeight = 0;

            // Parse Hyprland monitors output
            // Expected format: "\t1920x1080@60.00300 at 0x0"
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                // Look for resolution lines: contains "x", "at", and "@"
                if (!line.Contains('x') || !line.Contains("at") || !line.Contains('@'))
                    continue;

                try
                {
                    var trimmed = line.Trim();
                    var parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    
                    if (parts.Length < 3)
                        continue;

                    // Parse resolution: "1920x1080@60.00300"
                    var resolutionPart = parts[0].Split('@')[0]; // "1920x1080"
                    var resParts = resolutionPart.Split('x');
                    
                    if (resParts.Length != 2)
                        continue;

                    // Parse position: "0x0" (after "at")
                    var atIndex = Array.IndexOf(parts, "at");
                    if (atIndex < 0 || atIndex + 1 >= parts.Length)
                        continue;

                    var positionPart = parts[atIndex + 1]; // "0x0"
                    var posParts = positionPart.Split('x');
                    
                    if (posParts.Length != 2)
                        continue;

                    if (!int.TryParse(resParts[0], out int width) ||
                        !int.TryParse(resParts[1], out int height) ||
                        !int.TryParse(posParts[0], out int posX) ||
                        !int.TryParse(posParts[1], out int posY))
                        continue;

                    // Calculate bounding box
                    maxWidth = Math.Max(maxWidth, posX + width);
                    maxHeight = Math.Max(maxHeight, posY + height);
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "[HyprlandPositionProvider] Failed to parse monitor line: {Line}", line);
                    continue;
                }
            }

            return maxWidth > 0 && maxHeight > 0 ? (maxWidth, maxHeight) : null;
        }

        private (int X, int Y)? ParseCursorPosition(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return null;

            // Hyprland cursorpos returns format: "1920, 1080"
            // Use Span-based parsing to avoid allocations
            ReadOnlySpan<char> span = response.AsSpan().Trim();
            
            // Find comma position
            int commaIndex = span.IndexOf(',');
            if (commaIndex <= 0)
            {
                Log.Warning("[HyprlandPositionProvider] Failed to parse cursor position: {Response}", response);
                return null;
            }
            
            // Parse X coordinate (before comma)
            var xSpan = span.Slice(0, commaIndex).Trim();
            if (!int.TryParse(xSpan, out int x))
            {
                Log.Warning("[HyprlandPositionProvider] Failed to parse X coordinate: {Response}", response);
                return null;
            }
            
            // Parse Y coordinate (after comma)
            var ySpan = span.Slice(commaIndex + 1).Trim();
            if (!int.TryParse(ySpan, out int y))
            {
                Log.Warning("[HyprlandPositionProvider] Failed to parse Y coordinate: {Response}", response);
                return null;
            }
            
            return (x, y);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
                
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
