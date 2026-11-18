
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland.Kde;

public interface IKWinScreenShotSupportProbe
{
    /// <summary>Checks KWin availability without occupying a thread while it retries.</summary>
    public Task<WaylandSupportResult> ProbeSupportAsync(CancellationToken cancellationToken = default);
}
