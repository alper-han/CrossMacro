using CrossMacro.Platform.Abstractions;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal static class PortalStreamGeometry
{
    private const uint MonitorSourceType = 1;

    public static PortalStreamValidationResult ValidateMonitorStreams(IReadOnlyList<PortalStream> streams, ScreenRect? requestedRegion = null)
    {
        if (streams.Count == 0)
        {
            return PortalStreamValidationResult.Failure(
                ScreenReadErrorKind.CaptureFailed,
                "Portal Start response did not include any streams.");
        }

        var monitors = new List<PortalMonitorStream>(streams.Count);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var bounds = new HashSet<ScreenRect>();

        for (var index = 0; index < streams.Count; index++)
        {
            var stream = streams[index];
            var validation = ValidateMonitorStream(stream, index);
            if (!validation.IsSuccess)
            {
                return validation;
            }

            var monitor = validation.Stream;
            if (!string.IsNullOrWhiteSpace(monitor.Id) && !ids.Add(monitor.Id))
            {
                return PortalStreamValidationResult.Failure(
                    ScreenReadErrorKind.CaptureFailed,
                    $"GNOME portal returned duplicate monitor stream id '{monitor.Id}'. CrossMacro rejects duplicate monitor metadata to avoid ambiguous coordinate routing.");
            }

            if (!bounds.Add(monitor.Bounds))
            {
                return PortalStreamValidationResult.Failure(
                    ScreenReadErrorKind.CaptureFailed,
                    $"GNOME portal returned duplicate monitor stream bounds {FormatBounds(monitor.Bounds)}. CrossMacro rejects duplicate monitor metadata to avoid ambiguous coordinate routing.");
            }

            monitors.Add(monitor);
        }

        if (requestedRegion is not null && !IsRegionCovered(requestedRegion.Value, monitors))
        {
            return PortalStreamValidationResult.Failure(
                ScreenReadErrorKind.OutOfBounds,
                $"Requested region {FormatBounds(requestedRegion.Value)} is outside validated XDG Desktop Portal monitor coverage {FormatBounds(monitors)}. CrossMacro cannot force GNOME portal to select all monitors or a specific monitor; retry and select the monitor containing the requested coordinates.");
        }

        return PortalStreamValidationResult.Success(monitors, GetUnionBounds(monitors));
    }

    public static IReadOnlyList<PortalMonitorStream> GetIntersectingStreams(IReadOnlyList<PortalMonitorStream> streams, ScreenRect region)
    {
        var result = new List<PortalMonitorStream>();
        foreach (var stream in streams)
        {
            if (Intersects(stream.Bounds, region))
            {
                result.Add(stream);
            }
        }

        return result;
    }

    public static bool TryGetIntersection(ScreenRect first, ScreenRect second, out ScreenRect intersection)
    {
        var left = Math.Max(first.X, second.X);
        var top = Math.Max(first.Y, second.Y);
        var right = Math.Min(first.Right, second.Right);
        var bottom = Math.Min(first.Bottom, second.Bottom);
        if (left >= right || top >= bottom)
        {
            intersection = default;
            return false;
        }

        intersection = new ScreenRect(left, top, checked(right - left), checked(bottom - top));
        return true;
    }

    private static PortalStreamValidationResult ValidateMonitorStream(PortalStream stream, int index)
    {
        var properties = stream.Properties;
        if (!TryReadSourceType(properties, out var sourceType))
        {
            return PortalStreamValidationResult.Failure(
                ScreenReadErrorKind.CaptureFailed,
                $"Portal stream {index} did not include source_type. CrossMacro cannot route coordinates without monitor source metadata.");
        }

        if (sourceType != MonitorSourceType)
        {
            return PortalStreamValidationResult.Failure(
                ScreenReadErrorKind.CaptureFailed,
                $"GNOME portal returned a non-monitor stream with source_type={sourceType}. CrossMacro requests monitor sources only, but cannot force GNOME portal selections; select monitor sources only.");
        }

        if (!TryReadPosition(properties, out var x, out var y))
        {
            return PortalStreamValidationResult.Failure(
                ScreenReadErrorKind.CaptureFailed,
                $"Portal monitor stream {index} did not include a valid position. CrossMacro cannot route coordinates without monitor geometry.");
        }

        if (!TryReadSize(properties, out var width, out var height))
        {
            return PortalStreamValidationResult.Failure(
                ScreenReadErrorKind.CaptureFailed,
                $"Portal monitor stream {index} did not include a valid positive size. CrossMacro cannot route coordinates without monitor geometry.");
        }

        return PortalStreamValidationResult.Success(new PortalMonitorStream(stream, TryReadId(properties), new ScreenRect(x, y, width, height)));
    }

    private static bool TryReadSourceType(IReadOnlyDictionary<string, object> properties, out uint sourceType)
    {
        sourceType = 0;
        return properties.TryGetValue("source_type", out var value) && TryToUInt32(value, out sourceType);
    }

    private static bool TryReadPosition(IReadOnlyDictionary<string, object> properties, out int x, out int y)
    {
        x = 0;
        y = 0;
        return properties.TryGetValue("position", out var position)
            && TryReadPair(position, allowNegative: true, out x, out y);
    }

    private static bool TryReadSize(IReadOnlyDictionary<string, object> properties, out int width, out int height)
    {
        width = 0;
        height = 0;
        return properties.TryGetValue("size", out var size)
            && TryReadPair(size, allowNegative: false, out width, out height)
            && width > 0
            && height > 0;
    }

    private static string? TryReadId(IReadOnlyDictionary<string, object> properties)
    {
        return properties.TryGetValue("id", out var id) && id is string value && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }

    private static bool TryReadPair(object value, bool allowNegative, out int first, out int second)
    {
        first = 0;
        second = 0;
        return value switch
        {
            Array { Length: >= 2 } items => TryToInt32(items.GetValue(0), allowNegative, out first) && TryToInt32(items.GetValue(1), allowNegative, out second),
            ValueTuple<object, object> tuple => TryToInt32(tuple.Item1, allowNegative, out first) && TryToInt32(tuple.Item2, allowNegative, out second),
            Tuple<object, object> tuple => TryToInt32(tuple.Item1, allowNegative, out first) && TryToInt32(tuple.Item2, allowNegative, out second),
            _ => false
        };
    }

    private static bool TryToInt32(object? value, bool allowNegative, out int result)
    {
        switch (value)
        {
            case int signed when allowNegative || signed > 0:
                result = signed;
                return true;
            case uint unsigned when unsigned <= int.MaxValue && (allowNegative || unsigned > 0):
                result = (int)unsigned;
                return true;
            default:
                result = 0;
                return false;
        }
    }

    private static bool TryToUInt32(object value, out uint result)
    {
        switch (value)
        {
            case uint unsigned:
                result = unsigned;
                return true;
            case int signed when signed >= 0:
                result = (uint)signed;
                return true;
            default:
                result = 0;
                return false;
        }
    }

    private static ScreenRect GetUnionBounds(IReadOnlyList<PortalMonitorStream> streams)
    {
        var left = streams[0].Bounds.X;
        var top = streams[0].Bounds.Y;
        var right = streams[0].Bounds.Right;
        var bottom = streams[0].Bounds.Bottom;

        for (var index = 1; index < streams.Count; index++)
        {
            var bounds = streams[index].Bounds;
            left = Math.Min(left, bounds.X);
            top = Math.Min(top, bounds.Y);
            right = Math.Max(right, bounds.Right);
            bottom = Math.Max(bottom, bounds.Bottom);
        }

        return new ScreenRect(left, top, checked(right - left), checked(bottom - top));
    }

    private static bool IsRegionCovered(ScreenRect region, IReadOnlyList<PortalMonitorStream> streams)
    {
        var currentY = region.Y;
        while (currentY < region.Bottom)
        {
            var nextY = region.Bottom;
            var intervals = new List<(int Left, int Right)>();

            foreach (var stream in streams)
            {
                var bounds = stream.Bounds;
                if (bounds.Y > currentY && bounds.Y < nextY)
                {
                    nextY = bounds.Y;
                }

                if (bounds.Y <= currentY && bounds.Bottom > currentY)
                {
                    nextY = Math.Min(nextY, bounds.Bottom);
                    var left = Math.Max(region.X, bounds.X);
                    var right = Math.Min(region.Right, bounds.Right);
                    if (left < right)
                    {
                        intervals.Add((left, right));
                    }
                }
            }

            if (!CoversHorizontalRange(intervals, region.X, region.Right))
            {
                return false;
            }

            currentY = nextY;
        }

        return true;
    }

    private static bool CoversHorizontalRange(List<(int Left, int Right)> intervals, int left, int right)
    {
        intervals.Sort(static (first, second) => first.Left.CompareTo(second.Left));
        var covered = left;
        foreach (var interval in intervals)
        {
            if (interval.Left > covered)
            {
                return false;
            }

            covered = Math.Max(covered, interval.Right);
            if (covered >= right)
            {
                return true;
            }
        }

        return false;
    }

    private static bool Intersects(ScreenRect first, ScreenRect second) =>
        first.X < second.Right && first.Right > second.X && first.Y < second.Bottom && first.Bottom > second.Y;

    private static string FormatBounds(IReadOnlyList<PortalMonitorStream> streams) =>
        string.Join(", ", streams.Select(stream => FormatBounds(stream.Bounds)));

    private static string FormatBounds(ScreenRect bounds) =>
        $"({bounds.X},{bounds.Y},{bounds.Width}x{bounds.Height})";
}

internal readonly record struct PortalMonitorStream(PortalStream Stream, string? Id, ScreenRect Bounds);

internal sealed class PortalStreamValidationResult
{
    private PortalStreamValidationResult(
        IReadOnlyList<PortalMonitorStream> streams,
        ScreenRect? selectedBounds,
        ScreenReadErrorKind? errorKind,
        string? errorMessage)
    {
        Streams = streams;
        SelectedBounds = selectedBounds;
        ErrorKind = errorKind;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess => ErrorKind is null;

    public IReadOnlyList<PortalMonitorStream> Streams { get; }

    public ScreenRect? SelectedBounds { get; }

    public ScreenReadErrorKind? ErrorKind { get; }

    public string? ErrorMessage { get; }

    public PortalMonitorStream Stream => Streams.Count == 1
        ? Streams[0]
        : throw new InvalidOperationException("Portal stream validation did not contain exactly one stream.");

    public static PortalStreamValidationResult Success(PortalMonitorStream stream) =>
        new([stream], stream.Bounds, null, null);

    public static PortalStreamValidationResult Success(IReadOnlyList<PortalMonitorStream> streams, ScreenRect selectedBounds) =>
        new(streams, selectedBounds, null, null);

    public static PortalStreamValidationResult Failure(ScreenReadErrorKind errorKind, string errorMessage) =>
        new([], null, errorKind, errorMessage);
}
