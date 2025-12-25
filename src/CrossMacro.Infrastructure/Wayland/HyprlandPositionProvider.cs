using System;
using System.Text;
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
        // Cached command byte arrays to avoid repeated encoding
        private static readonly byte[] CursorPosCommand = Encoding.UTF8.GetBytes("cursorpos");
        private static readonly byte[] MonitorsCommand = Encoding.UTF8.GetBytes("monitors");

        private readonly HyprlandIpcClient _ipcClient;
        private bool _disposed;

        public bool IsSupported => _ipcClient.IsAvailable;
        public string ProviderName => "Hyprland IPC";

        public HyprlandPositionProvider() : this(new HyprlandIpcClient())
        {
        }

        public HyprlandPositionProvider(HyprlandIpcClient ipcClient)
        {
            _ipcClient = ipcClient ?? throw new ArgumentNullException(nameof(ipcClient));

            if (IsSupported)
            {
                Log.Information("[HyprlandPositionProvider] Using shared IPC client");
            }
        }

        public async Task<(int X, int Y)?> GetAbsolutePositionAsync()
        {
            if (_disposed || !IsSupported)
                return null;

            try
            {
                var response = await _ipcClient.SendCommandAsync(CursorPosCommand).ConfigureAwait(false);
                if (response == null) return null;
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
            if (_disposed || !IsSupported)
                return null;

            try
            {
                var response = await _ipcClient.SendCommandAsync(MonitorsCommand).ConfigureAwait(false);
                if (response == null) return null;
                return ParseMonitors(response);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[HyprlandPositionProvider] Failed to get screen resolution");
                return null;
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
