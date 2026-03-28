using System.Text.Json;
using CrossMacro.Core.Services;
using Serilog;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

/// <summary>
/// Mouse position provider for Wayfire compositor.
/// Requires ipc and ipc-rules plugins to expose cursor/output methods.
/// </summary>
public class WayfirePositionProvider : IMousePositionProvider
{
    private const string CursorMethod = "window-rules/get_cursor_position";
    private const string ListOutputsMethod = "window-rules/list-outputs";
    private static readonly TimeSpan CapabilityProbeTimeout = TimeSpan.FromSeconds(1);

    private readonly IWayfireIpcClient _ipcClient;
    private readonly SemaphoreSlim _layoutGate = new(1, 1);

    private bool _disposed;
    private volatile bool _isSupported;
    private bool _hasLayout;

    private int _originX;
    private int _originY;
    private int _layoutWidth;
    private int _layoutHeight;

    public string ProviderName => "Wayfire IPC";
    public bool IsSupported => !_disposed && _isSupported;

    public WayfirePositionProvider() : this(new WayfireIpcClient())
    {
    }

    internal WayfirePositionProvider(IWayfireIpcClient ipcClient)
    {
        _ipcClient = ipcClient ?? throw new ArgumentNullException(nameof(ipcClient));
        _isSupported = ProbeCapabilities();

        if (_isSupported)
        {
            Log.Information("[WayfirePositionProvider] Capability probe succeeded");
        }
        else
        {
            Log.Debug("[WayfirePositionProvider] Capability probe failed; provider unavailable");
        }
    }

    public async Task<(int X, int Y)?> GetAbsolutePositionAsync()
    {
        if (_disposed || !_isSupported)
        {
            return null;
        }

        await EnsureLayoutAsync().ConfigureAwait(false);
        if (!_isSupported)
        {
            return null;
        }

        var response = await _ipcClient.SendRequestAsync(CursorMethod).ConfigureAwait(false);
        if (!TryParseCursorPosition(response, out var rawX, out var rawY, out var methodUnavailable))
        {
            if (methodUnavailable)
            {
                DisableProvider("cursor method unavailable");
            }

            return null;
        }

        int normalizedX = rawX - Volatile.Read(ref _originX);
        int normalizedY = rawY - Volatile.Read(ref _originY);

        int width = Volatile.Read(ref _layoutWidth);
        int height = Volatile.Read(ref _layoutHeight);

        if (width > 0)
        {
            normalizedX = Math.Clamp(normalizedX, 0, width - 1);
        }

        if (height > 0)
        {
            normalizedY = Math.Clamp(normalizedY, 0, height - 1);
        }

        return (normalizedX, normalizedY);
    }

    public async Task<(int Width, int Height)?> GetScreenResolutionAsync()
    {
        if (_disposed || !_isSupported)
        {
            return null;
        }

        var layout = await RefreshLayoutAsync().ConfigureAwait(false);
        return layout.HasValue ? (layout.Value.Width, layout.Value.Height) : null;
    }

    private bool ProbeCapabilities()
    {
        if (!_ipcClient.IsAvailable)
        {
            return false;
        }

        try
        {
            using var cts = new CancellationTokenSource(CapabilityProbeTimeout);

            var cursorResponse = _ipcClient.SendRequestAsync(CursorMethod, cts.Token).GetAwaiter().GetResult();
            if (!TryParseCursorPosition(cursorResponse, out _, out _, out _))
            {
                return false;
            }

            var outputsResponse = _ipcClient.SendRequestAsync(ListOutputsMethod, cts.Token).GetAwaiter().GetResult();
            if (!TryParseOutputLayout(outputsResponse, out var layout, out _))
            {
                return false;
            }

            SetLayout(layout);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[WayfirePositionProvider] Capability probe error");
            return false;
        }
    }

    private async Task EnsureLayoutAsync()
    {
        if (Volatile.Read(ref _hasLayout))
        {
            return;
        }

        await _layoutGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed || !_isSupported || Volatile.Read(ref _hasLayout))
            {
                return;
            }

            await RefreshLayoutCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _layoutGate.Release();
        }
    }

    private async Task<OutputLayout?> RefreshLayoutAsync()
    {
        await _layoutGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed || !_isSupported)
            {
                return null;
            }

            return await RefreshLayoutCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _layoutGate.Release();
        }
    }

    private async Task<OutputLayout?> RefreshLayoutCoreAsync()
    {
        var response = await _ipcClient.SendRequestAsync(ListOutputsMethod).ConfigureAwait(false);
        if (!TryParseOutputLayout(response, out var layout, out var methodUnavailable))
        {
            if (methodUnavailable)
            {
                DisableProvider("output listing method unavailable");
            }

            return null;
        }

        SetLayout(layout);
        return layout;
    }

    private void SetLayout(OutputLayout layout)
    {
        Interlocked.Exchange(ref _originX, layout.OriginX);
        Interlocked.Exchange(ref _originY, layout.OriginY);
        Interlocked.Exchange(ref _layoutWidth, layout.Width);
        Interlocked.Exchange(ref _layoutHeight, layout.Height);
        Volatile.Write(ref _hasLayout, true);
    }

    private void DisableProvider(string reason)
    {
        _isSupported = false;
        Log.Warning("[WayfirePositionProvider] Disabled provider: {Reason}", reason);
    }

    internal static bool TryParseCursorPosition(
        string? response,
        out int x,
        out int y,
        out bool methodUnavailable)
    {
        x = 0;
        y = 0;
        methodUnavailable = false;

        if (string.IsNullOrWhiteSpace(response))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            if (TryGetMethodError(root, out methodUnavailable))
            {
                return false;
            }

            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("pos", out var posElement) ||
                posElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!TryGetNumericValue(posElement, "x", out var xValue) ||
                !TryGetNumericValue(posElement, "y", out var yValue))
            {
                return false;
            }

            x = (int)Math.Round(xValue);
            y = (int)Math.Round(yValue);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    internal static bool TryParseOutputLayout(
        string? response,
        out OutputLayout layout,
        out bool methodUnavailable)
    {
        layout = default;
        methodUnavailable = false;

        if (string.IsNullOrWhiteSpace(response))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            if (TryGetMethodError(root, out methodUnavailable))
            {
                return false;
            }

            JsonElement outputs;
            if (root.ValueKind == JsonValueKind.Array)
            {
                outputs = root;
            }
            else if (root.ValueKind == JsonValueKind.Object &&
                     root.TryGetProperty("outputs", out var outputsElement) &&
                     outputsElement.ValueKind == JsonValueKind.Array)
            {
                outputs = outputsElement;
            }
            else
            {
                return false;
            }

            bool hasAnyGeometry = false;
            int minX = 0;
            int minY = 0;
            int maxX = 0;
            int maxY = 0;

            foreach (var output in outputs.EnumerateArray())
            {
                if (output.ValueKind != JsonValueKind.Object ||
                    !output.TryGetProperty("geometry", out var geometry) ||
                    geometry.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!TryGetNumericValue(geometry, "x", out var gx) ||
                    !TryGetNumericValue(geometry, "y", out var gy) ||
                    !TryGetNumericValue(geometry, "width", out var gw) ||
                    !TryGetNumericValue(geometry, "height", out var gh))
                {
                    continue;
                }

                int x = (int)Math.Round(gx);
                int y = (int)Math.Round(gy);
                int width = (int)Math.Round(gw);
                int height = (int)Math.Round(gh);

                if (width <= 0 || height <= 0)
                {
                    continue;
                }

                int right = x + width;
                int bottom = y + height;

                if (!hasAnyGeometry)
                {
                    hasAnyGeometry = true;
                    minX = x;
                    minY = y;
                    maxX = right;
                    maxY = bottom;
                    continue;
                }

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, right);
                maxY = Math.Max(maxY, bottom);
            }

            if (!hasAnyGeometry)
            {
                return false;
            }

            layout = new OutputLayout(
                OriginX: minX,
                OriginY: minY,
                Width: maxX - minX,
                Height: maxY - minY);

            return layout.Width > 0 && layout.Height > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryGetMethodError(JsonElement root, out bool methodUnavailable)
    {
        methodUnavailable = false;

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("error", out var errorElement))
        {
            return false;
        }

        if (errorElement.ValueKind != JsonValueKind.String)
        {
            return true;
        }

        var errorText = errorElement.GetString();
        methodUnavailable = !string.IsNullOrWhiteSpace(errorText) &&
            errorText.Contains("No such method", StringComparison.OrdinalIgnoreCase);
        return true;
    }

    private static bool TryGetNumericValue(JsonElement element, string property, out double value)
    {
        value = 0;

        if (!element.TryGetProperty(property, out var prop))
        {
            return false;
        }

        if (prop.ValueKind == JsonValueKind.Number)
        {
            return prop.TryGetDouble(out value);
        }

        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _layoutGate.Dispose();
        _ipcClient.Dispose();
        GC.SuppressFinalize(this);
    }
}

internal readonly record struct OutputLayout(int OriginX, int OriginY, int Width, int Height);
