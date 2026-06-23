using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer.Wayland;
using Microsoft.Win32.SafeHandles;

namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

internal sealed class FakePortalScreenCastSessionFactory : IPortalScreenCastSessionFactory
{
    private readonly PortalScreenCastSessionResult _result;

    public FakePortalScreenCastSessionFactory(PortalScreenCastSessionResult result)
    {
        _result = result;
    }

    public int StartCalls { get; private set; }

    public ScreenRect? LastRequestedRegion { get; private set; }

    public Task<PortalScreenCastSessionResult> StartSessionAsync(ScreenReadOptions options)
    {
        return StartSessionAsync(null, options);
    }

    public Task<PortalScreenCastSessionResult> StartSessionAsync(ScreenRect? requestedRegion, ScreenReadOptions options)
    {
        StartCalls++;
        LastRequestedRegion = requestedRegion;
        return Task.FromResult(_result);
    }

    public static PortalScreenCastSession CreateSession(int x = 0, int y = 0, int width = 2, int height = 1, uint nodeId = 42, string? id = "monitor-1")
    {
        var properties = new Dictionary<string, object>
        {
            ["source_type"] = 1U,
            ["position"] = new object[] { x, y },
            ["size"] = new object[] { width, height }
        };

        if (!string.IsNullOrWhiteSpace(id))
        {
            properties["id"] = id;
        }

        return new PortalScreenCastSession(
            "/org/freedesktop/portal/desktop/session/fake",
            [new PortalStream(nodeId, properties)],
            new SafeFileHandle(new IntPtr(-1), ownsHandle: false));
    }

    public static PortalScreenCastSession CreateSession(IReadOnlyList<PortalStream> streams)
    {
        return new PortalScreenCastSession(
            "/org/freedesktop/portal/desktop/session/fake",
            streams,
            new SafeFileHandle(new IntPtr(-1), ownsHandle: false));
    }
}
