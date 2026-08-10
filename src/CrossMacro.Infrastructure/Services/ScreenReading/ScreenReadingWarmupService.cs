
namespace CrossMacro.Infrastructure.Services.ScreenReading;

public sealed class ScreenReadingWarmupService(
    IScreenFrameProvider frameProvider,
    IScreenReadingDiagnosticProvider? diagnosticProvider = null,
    IScreenReadingCapabilityReadiness? capabilityReadiness = null) : IScreenReadingWarmupService
{
    private static readonly ScreenRect WarmupRegion = new(0, 0, 1, 1);
    private static readonly TimeSpan WarmupTimeout = TimeSpan.FromSeconds(15);

    private readonly IScreenFrameProvider _frameProvider = frameProvider ?? throw new ArgumentNullException(nameof(frameProvider));
    private readonly IScreenReadingDiagnosticProvider? _diagnosticProvider = diagnosticProvider;
    private readonly IScreenReadingCapabilityReadiness? _capabilityReadiness = capabilityReadiness;
    private readonly Lock _lock = new();
    private Task? _warmupTask;

    public async Task WarmUpPortalSessionAsync(CancellationToken cancellationToken = default)
    {
        if (_capabilityReadiness is not null)
        {
            await _capabilityReadiness.EnsureReadyAsync(cancellationToken).ConfigureAwait(false);
        }

        if (!ShouldWarmUpPortalSession())
        {
            return;
        }

        Task warmupTask;
        lock (_lock)
        {
            if (_warmupTask is
            { IsCompleted: false } or
            { IsCompletedSuccessfully: true })
            {
                warmupTask = _warmupTask;
            }
            else
            {
                _warmupTask = WarmUpCoreAsync(cancellationToken);
                warmupTask = _warmupTask;
            }
        }

        await warmupTask.ConfigureAwait(false);
    }

    private bool ShouldWarmUpPortalSession()
    {
        if (_diagnosticProvider is null)
        {
            return false;
        }

        try
        {
            var snapshot = _diagnosticProvider.GetSnapshot();
            return string.Equals(snapshot.SelectedBackend, "Portal", StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[ScreenReadingWarmupService] Screen-reading diagnostics failed; skipping Portal warm-up");
            return false;
        }
    }

    private async Task WarmUpCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _frameProvider.CaptureFrameAsync(
                WarmupRegion,
                new ScreenReadOptions(WarmupTimeout, ScreenReadOptions.DefaultPollInterval, cancellationToken)).ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                Log.Warning(
                    "[ScreenReadingWarmupService] Portal screen-reading warm-up failed: {ErrorKind} {ErrorMessage}",
                    result.ErrorKind,
                    result.ErrorMessage);
                return;
            }

            result.Value?.Dispose();
            Log.Information("[ScreenReadingWarmupService] Portal screen-reading session warmed up");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Log.Debug("[ScreenReadingWarmupService] Portal screen-reading warm-up cancelled");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[ScreenReadingWarmupService] Portal screen-reading warm-up failed");
        }
    }
}
