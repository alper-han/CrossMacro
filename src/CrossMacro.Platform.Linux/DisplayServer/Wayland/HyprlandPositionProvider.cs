
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

public sealed class HyprlandPositionProvider : IMousePositionProvider
{
    private static readonly byte[] CursorPosCommand = Encoding.UTF8.GetBytes("cursorpos");
    private static readonly byte[] MonitorsCommand = Encoding.UTF8.GetBytes("monitors");

    private readonly HyprlandIpcClient _ipcClient;
    private bool _disposed;

    public bool IsSupported => _ipcClient.IsAvailable;
    public string ProviderName => "Hyprland IPC";

    public HyprlandPositionProvider() : this(new HyprlandIpcClient()) { /* Empty */ }

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
        {
            return null;
        }

        try
        {
            var response = await _ipcClient.SendCommandAsync(CursorPosCommand).ConfigureAwait(false);
            if (response is null)
            {
                return null;
            }

            return ParseCursorPosition(response);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[HyprlandPositionProvider] Failed to get cursor position");
            return null;
        }
    }

    public async Task<(int Width, int Height)?> GetScreenResolutionAsync()
    {
        var bounds = await GetDesktopBoundsAsync().ConfigureAwait(false);
        return bounds is not null ? (bounds.Value.Width, bounds.Value.Height) : null;
    }

    public async Task<ScreenRect?> GetDesktopBoundsAsync()
    {
        if (_disposed || !IsSupported)
        {
            return null;
        }

        try
        {
            var response = await _ipcClient.SendCommandAsync(MonitorsCommand).ConfigureAwait(false);
            if (response is null)
            {
                return null;
            }

            return ParseMonitorBounds(response);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.LogError(ex, "[HyprlandPositionProvider] Failed to get screen resolution");
            return null;
        }
    }

    internal static ScreenRect? ParseMonitorBounds(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;

        foreach (var block in output.Split("Monitor ", StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                if (TryParseMonitorBlock(block, out int blockWidth, out int blockHeight, out int posX, out int posY))
                {
                    minX = Math.Min(minX, posX);
                    minY = Math.Min(minY, posY);
                    maxX = Math.Max(maxX, checked(posX + blockWidth));
                    maxY = Math.Max(maxY, checked(posY + blockHeight));
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.Warning(ex, "[HyprlandPositionProvider] Error parsing monitor block");
            }
        }

        return minX < maxX && minY < maxY
            ? new ScreenRect(minX, minY, checked(maxX - minX), checked(maxY - minY))
            : null;
    }

    private static bool TryParseMonitorBlock(string block, out int width, out int height, out int posX, out int posY)
    {
        width = 0;
        height = 0;
        posX = 0;
        posY = 0;
        int transform = 0;
        double scale = 1.0;
        bool resolutionFound = false;

        var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed.Contains('x', StringComparison.Ordinal) && trimmed.Contains("at", StringComparison.Ordinal) &&
                !resolutionFound && TryParseResolutionLine(trimmed, ref width, ref height, ref posX, ref posY))
            {
                resolutionFound = true;
            }

            if (trimmed.StartsWith("scale:", StringComparison.Ordinal))
            {
                var scalePart = trimmed.Substring("scale:".Length).Trim();
                if (double.TryParse(scalePart, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double s))
                {
                    scale = s;
                }
            }

            if (trimmed.StartsWith("transform:", StringComparison.Ordinal))
            {
                var transformPart = trimmed.Substring("transform:".Length).Trim();
                _ = int.TryParse(transformPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out transform);
            }
        }

        if (resolutionFound && width > 0 && height > 0 && double.IsFinite(scale) && scale > 0)
        {
            if (transform is 1 or 3 or 5 or 7)
            {
                (width, height) = (height, width);
            }

            double logicalWidth = Math.Round(width / scale, MidpointRounding.AwayFromZero);
            double logicalHeight = Math.Round(height / scale, MidpointRounding.AwayFromZero);
            if (logicalWidth is < 1 or > int.MaxValue || logicalHeight is < 1 or > int.MaxValue)
            {
                return false;
            }

            width = (int)logicalWidth;
            height = (int)logicalHeight;
            return true;
        }

        return false;
    }

    private static bool TryParseResolutionLine(string trimmed, ref int width, ref int height, ref int posX, ref int posY)
    {
        var parts = trimmed.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            return false;
        }

        var resPart = parts[0].Split('@')[0].Split('x');
        var atIndex = Array.IndexOf(parts, "at");

        if (resPart.Length is 2 && atIndex >= 0 && atIndex + 1 < parts.Length)
        {
            var posPart = parts[atIndex + 1].Split('x');

            if (posPart.Length is 2 && int.TryParse(resPart[0], CultureInfo.InvariantCulture, out width) &&
                    int.TryParse(resPart[1], CultureInfo.InvariantCulture, out height) &&
                    int.TryParse(posPart[0], CultureInfo.InvariantCulture, out posX) &&
                    int.TryParse(posPart[1], CultureInfo.InvariantCulture, out posY))
            {
                return true;
            }
        }

        return false;
    }

    private static (int X, int Y)? ParseCursorPosition(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return null;
        }

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

        double roundedX = Math.Round(x, MidpointRounding.AwayFromZero);
        double roundedY = Math.Round(y, MidpointRounding.AwayFromZero);
        if (!double.IsFinite(roundedX) ||
            !double.IsFinite(roundedY) ||
            roundedX is < int.MinValue or > int.MaxValue ||
            roundedY is < int.MinValue or > int.MaxValue)
        {
            Log.Warning("[HyprlandPositionProvider] Cursor position is outside the supported range: {Response}", response);
            return null;
        }

        return ((int)roundedX, (int)roundedY);
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
