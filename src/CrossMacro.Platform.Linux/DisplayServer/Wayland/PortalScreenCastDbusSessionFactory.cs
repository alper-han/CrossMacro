
namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class PortalScreenCastDbusSessionFactory : IPortalScreenCastSessionFactory
{
    public static PortalScreenCastDbusSessionFactory Instance { get; } = new(restoreTokenStore: null, PortalScreenCastSessionClientFactory.Instance);

    private readonly IPortalScreenCastRestoreTokenStore? _restoreTokenStore;
    private readonly IPortalScreenCastSessionClientFactory _clientFactory;
    private readonly Func<CancellationToken, Task<IDisposable>> _acquireRestoreTokenLease;

    public PortalScreenCastDbusSessionFactory(IPortalScreenCastRestoreTokenStore? restoreTokenStore)
        : this(restoreTokenStore, PortalScreenCastSessionClientFactory.Instance) { /* Empty */ }

    internal PortalScreenCastDbusSessionFactory(
        IPortalScreenCastRestoreTokenStore? restoreTokenStore,
        IPortalScreenCastSessionClientFactory clientFactory,
        Func<CancellationToken, Task<IDisposable>>? acquireRestoreTokenLease = null)
    {
        _restoreTokenStore = restoreTokenStore;
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _acquireRestoreTokenLease = acquireRestoreTokenLease ?? PortalScreenCastRestoreTokenLease.AcquireAsync;
    }

    public Task<PortalScreenCastSessionResult> StartSessionAsync(ScreenReadOptions options) =>
        StartSessionAsync(requestedRegion: null, options);

    public async Task<PortalScreenCastSessionResult> StartSessionAsync(ScreenRect? requestedRegion, ScreenReadOptions options)
    {
        using var restoreTokenLease = _restoreTokenStore is null
            ? null
            : await _acquireRestoreTokenLease(options.CancellationToken).ConfigureAwait(false);

        var restoreToken = _restoreTokenStore is null
            ? null
            : await _restoreTokenStore.LoadRestoreTokenAsync(options.CancellationToken).ConfigureAwait(false);
        var restoreData = _restoreTokenStore is null
            ? null
            : await _restoreTokenStore.LoadRestoreDataAsync(options.CancellationToken).ConfigureAwait(false);
        var firstAttempt = await StartSessionAttemptAsync(options, restoreToken, restoreData).ConfigureAwait(false);
        if (firstAttempt.Result.IsSuccess || !firstAttempt.CanRetryWithoutRestoreToken)
        {
            return firstAttempt.Result;
        }

        await ClearRestoreStateAsync().ConfigureAwait(false);
        var retryAttempt = await StartSessionAttemptAsync(options, restoreToken: null, restoreData: null).ConfigureAwait(false);
        return retryAttempt.Result;
    }

    private async Task<StartSessionAttempt> StartSessionAttemptAsync(ScreenReadOptions options, string? restoreToken, string? restoreData)
    {
        IPortalScreenCastSessionClient? client = null;
        PortalScreenCastSession? session = null;
        var sessionTransferred = false;
        try
        {
            client = await _clientFactory.ConnectAsync().ConfigureAwait(false);
            session = await client.StartAsync(options, restoreToken, restoreData).ConfigureAwait(false);
            var validation = PortalStreamGeometry.ValidateMonitorStreams(session.Streams);
            if (!validation.IsSuccess)
            {
                return new StartSessionAttempt(
                    PortalScreenCastSessionResult.Failure(
                        validation.ErrorKind ?? ScreenReadErrorKind.CaptureFailed,
                        validation.ErrorMessage ?? "XDG Desktop Portal ScreenCast returned unusable monitor metadata."),
                    CanRetryWithoutRestoreToken: HasRestoreState(restoreToken, restoreData)
                        && validation.ErrorKind is ScreenReadErrorKind.CaptureFailed);
            }

            if (!string.IsNullOrWhiteSpace(session.RestoreToken))
            {
                await SaveRestoreTokenAsync(session.RestoreToken).ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(session.RestoreData))
            {
                await SaveRestoreDataAsync(session.RestoreData).ConfigureAwait(false);
            }

            sessionTransferred = true;
            return new StartSessionAttempt(PortalScreenCastSessionResult.Success(session), CanRetryWithoutRestoreToken: false);
        }
        catch (PortalScreenCastException ex)
        {
            return new StartSessionAttempt(
                PortalScreenCastSessionResult.Failure(ex.ErrorKind, ex.Message),
                CanRetryWithoutRestoreToken: HasRestoreState(restoreToken, restoreData)
                    && ex.ErrorKind is ScreenReadErrorKind.CaptureFailed);
        }
        catch (OperationCanceledException)
        {
            return new StartSessionAttempt(PortalScreenCastSessionResult.Failure(ScreenReadErrorKind.Canceled, "XDG Desktop Portal ScreenCast session was canceled."), CanRetryWithoutRestoreToken: false);
        }
        catch (TimeoutException ex)
        {
            return new StartSessionAttempt(PortalScreenCastSessionResult.Failure(ScreenReadErrorKind.CaptureTimeout, ex.Message), CanRetryWithoutRestoreToken: false);
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException)
        {
            return new StartSessionAttempt(PortalScreenCastSessionResult.Failure(ScreenReadErrorKind.CaptureFailed, ex.Message), CanRetryWithoutRestoreToken: false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return new StartSessionAttempt(PortalScreenCastSessionResult.Failure(ScreenReadErrorKind.BackendUnavailable, ex.Message), CanRetryWithoutRestoreToken: false);
        }
        finally
        {
            if (!sessionTransferred)
            {
                session?.Dispose();
                client?.DisposeIfNotOwnedBySession();
            }
        }
    }

    private async Task SaveRestoreTokenAsync(string restoreToken)
    {
        if (_restoreTokenStore is null)
        {
            return;
        }

        try
        {
            await _restoreTokenStore.SaveRestoreTokenAsync(restoreToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            // The granted session is still usable; a failed best-effort token save should not break capture.
        }
    }

    private async Task SaveRestoreDataAsync(string restoreData)
    {
        if (_restoreTokenStore is null)
        {
            return;
        }

        try
        {
            await _restoreTokenStore.SaveRestoreDataAsync(restoreData).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            _ = ex;
        }
    }

    private async Task ClearRestoreStateAsync()
    {
        if (_restoreTokenStore is null)
        {
            return;
        }

        try
        {
            await _restoreTokenStore.ClearRestoreTokenAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            // The stale token will be ignored for this retry; persistence failure should not block capture.
        }
    }

    private static bool HasRestoreState(string? restoreToken, string? restoreData) =>
        !string.IsNullOrWhiteSpace(restoreToken) || !string.IsNullOrWhiteSpace(restoreData);

    private readonly record struct StartSessionAttempt(PortalScreenCastSessionResult Result, bool CanRetryWithoutRestoreToken);
}
