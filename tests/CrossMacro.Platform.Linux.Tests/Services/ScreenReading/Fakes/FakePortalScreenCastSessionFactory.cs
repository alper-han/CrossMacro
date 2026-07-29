
namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

internal sealed class FakePortalScreenCastSessionFactory(PortalScreenCastSessionResult result) : IPortalScreenCastSessionFactory
{
    private readonly PortalScreenCastSessionResult _result = result;

    public int StartCalls { get; private set; }

    public ScreenRect? LastRequestedRegion { get; private set; }

    public Task<PortalScreenCastSessionResult> StartSessionAsync(ScreenReadOptions options)
    {
        return StartSessionAsync(requestedRegion: null, options);
    }

    public Task<PortalScreenCastSessionResult> StartSessionAsync(ScreenRect? requestedRegion, ScreenReadOptions options)
    {
        StartCalls++;
        LastRequestedRegion = requestedRegion;
        return Task.FromResult(_result);
    }

    public static PortalScreenCastSession CreateSession(int x = 0, int y = 0, int width = 2, int height = 1, uint nodeId = 42, string? id = "monitor-1")
    {
        var properties = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["source_type"] = 1U,
            ["position"] = new object[] { x, y },
            ["size"] = new object[] { width, height },
        };

        if (!string.IsNullOrWhiteSpace(id))
        {
            properties["id"] = id;
        }

        return new PortalScreenCastSession(
            "/org/freedesktop/portal/desktop/session/fake",
            [new PortalStreamDescriptor(nodeId, properties)],
            new SafeFileHandle(new IntPtr(-1), ownsHandle: false));
    }

    public static PortalScreenCastSession CreateSession(IReadOnlyList<PortalStreamDescriptor> streams)
    {
        return new PortalScreenCastSession(
            "/org/freedesktop/portal/desktop/session/fake",
            streams,
            new SafeFileHandle(new IntPtr(-1), ownsHandle: false));
    }
}
