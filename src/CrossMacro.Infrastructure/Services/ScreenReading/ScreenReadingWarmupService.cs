using CrossMacro.Core.Logging;
using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Abstractions.Diagnostics;

namespace CrossMacro.Infrastructure.Services.ScreenReading;

public sealed class ScreenReadingWarmupService : IScreenReadingWarmupService
{
    private static readonly ScreenRect WarmupRegion = new(0, 0, 1, 1);
    private static readonly TimeSpan WarmupTimeout = TimeSpan.FromSeconds(15);

    private readonly IScreenFrameProvider _frameProvider;
    private readonly IScreenReadingDiagnosticProvider? _diagnosticProvider;
    private readonly Lock _lock = new();
    private Task? _warmupTask;

    public ScreenReadingWarmupService(
        IScreenFrameProvider frameProvider,
        IScreenReadingDiagnosticProvider? diagnosticProvider = null)
    {
        _frameProvider = frameProvider ?? throw new ArgumentNullException(nameof(frameProvider));
        _diagnosticProvider = diagnosticProvider;
    }

    public Task WarmUpPortalSessionAsync(CancellationToken cancellationToken = default)
    {
        if (!ShouldWarmUpPortalSession())
        {
            return Task.CompletedTask;
        }

        lock (_lock)
        {
            if (_warmupTask is { IsCompleted: false } || _warmupTask is { IsCompletedSuccessfully: true })
            {
                return _warmupTask;
            }

            _warmupTask = WarmUpCoreAsync(cancellationToken);
            return _warmupTask;
        }
    }

    private bool ShouldWarmUpPortalSession()
    {
        if (_diagnosticProvider == null)
        {
            return false;
        }

        try
        {
            var snapshot = _diagnosticProvider.GetSnapshot();
            return string.Equals(snapshot.SelectedBackend, "Portal", StringComparison.Ordinal);
        }
        catch (Exception ex)
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
                new ScreenReadOptions(WarmupTimeout, ScreenReadOptions.Default.PollInterval, cancellationToken)).ConfigureAwait(false);

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
        catch (Exception ex)
        {
            Log.Warning(ex, "[ScreenReadingWarmupService] Portal screen-reading warm-up failed");
        }
    }
}
