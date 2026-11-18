using CrossMacro.Platform.Linux.DisplayServer.Wayland.Protocols.WlrScreencopy;

namespace CrossMacro.Platform.Linux.Tests.Services.ScreenReading.Fakes;

/// <summary>Answers every backend's support probe with a fixed result.</summary>
internal sealed class FakeSupportProbe(WaylandSupportResult support) :
    IExtImageCopySupportProbe,
    IWlrScreencopySupportProbe,
    IPortalScreenCastSupportProbe,
    IKWinScreenShotSupportProbe
{
    public WaylandSupportResult ProbeSupport() => support;

    public Task<WaylandSupportResult> ProbeSupportAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(support);
}
