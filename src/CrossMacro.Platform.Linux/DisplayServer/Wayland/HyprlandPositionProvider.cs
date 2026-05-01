using System;
using System.Text;
using System.Threading.Tasks;
using CrossMacro.Core.Logging;
using CrossMacro.Core.Services;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland
{
    public class HyprlandPositionProvider : IMousePositionProvider
    {
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

            var monitorBlocks = output.Split("Monitor ", StringSplitOptions.RemoveEmptyEntries);

            foreach (var block in monitorBlocks)
            {
                try
                {
                    var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    
                    int width = 0;
                    int height = 0;
                    int posX = 0;
                    int posY = 0;
                    double scale = 1.0;
                    bool resolutionFound = false;

                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        
                        if (trimmed.Contains('x') && trimmed.Contains("at") && !resolutionFound)
                        {
                            var parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 3)
                            {
                                var resPart = parts[0].Split('@')[0].Split('x');
                                var atIndex = Array.IndexOf(parts, "at");
                                
                                if (resPart.Length == 2 && atIndex >= 0 && atIndex + 1 < parts.Length)
                                {
                                    var posPart = parts[atIndex + 1].Split('x');
                                    
                                    if (posPart.Length == 2)
                                    {
                                        if (int.TryParse(resPart[0], out width) &&
                                            int.TryParse(resPart[1], out height) &&
                                            int.TryParse(posPart[0], out posX) &&
                                            int.TryParse(posPart[1], out posY))
                                        {
                                            resolutionFound = true;
                                        }
                                    }
                                }
                            }
                        }
                        
                        if (trimmed.StartsWith("scale:"))
                        {
                            var scalePart = trimmed.Substring("scale:".Length).Trim();
                            if (double.TryParse(scalePart, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double s))
                            {
                                scale = s;
                            }
                        }
                    }

                    if (resolutionFound && width > 0 && height > 0)
                    {
                        int logicalWidth = (int)Math.Round(width / scale);
                        int logicalHeight = (int)Math.Round(height / scale);

                        maxWidth = Math.Max(maxWidth, posX + logicalWidth);
                        maxHeight = Math.Max(maxHeight, posY + logicalHeight);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[HyprlandPositionProvider] Error parsing monitor block");
                }
            }

            return maxWidth > 0 && maxHeight > 0 ? (maxWidth, maxHeight) : null;
        }

        private (int X, int Y)? ParseCursorPosition(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return null;

            ReadOnlySpan<char> span = response.AsSpan().Trim();
            
            int commaIndex = span.IndexOf(',');
            if (commaIndex <= 0)
            {
                Log.Warning("[HyprlandPositionProvider] Failed to parse cursor position: {Response}", response);
                return null;
            }
            
            var xSpan = span.Slice(0, commaIndex).Trim();
            if (!double.TryParse(xSpan, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double x))
            {
                Log.Warning("[HyprlandPositionProvider] Failed to parse X coordinate: {Response}", response);
                return null;
            }
            
            var ySpan = span.Slice(commaIndex + 1).Trim();
            if (!double.TryParse(ySpan, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double y))
            {
                Log.Warning("[HyprlandPositionProvider] Failed to parse Y coordinate: {Response}", response);
                return null;
            }
            
            return ((int)Math.Round(x), (int)Math.Round(y));
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
