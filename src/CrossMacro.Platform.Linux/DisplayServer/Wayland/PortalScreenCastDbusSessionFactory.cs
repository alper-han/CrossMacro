using CrossMacro.Platform.Abstractions;
using CrossMacro.Platform.Linux.DisplayServer.Wayland.DBus;

namespace CrossMacro.Platform.Linux.DisplayServer.Wayland;

internal sealed class PortalScreenCastDbusSessionFactory : IPortalScreenCastSessionFactory
{
    public static PortalScreenCastDbusSessionFactory Instance { get; } = new(null, PortalScreenCastSessionClientFactory.Instance);

    private readonly IPortalScreenCastRestoreTokenStore? _restoreTokenStore;
    private readonly IPortalScreenCastSessionClientFactory _clientFactory;

    public PortalScreenCastDbusSessionFactory(IPortalScreenCastRestoreTokenStore? restoreTokenStore)
        : this(restoreTokenStore, PortalScreenCastSessionClientFactory.Instance)
    {
    }

    internal PortalScreenCastDbusSessionFactory(
        IPortalScreenCastRestoreTokenStore? restoreTokenStore,
        IPortalScreenCastSessionClientFactory clientFactory)
    {
        _restoreTokenStore = restoreTokenStore;
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
    }

    public Task<PortalScreenCastSessionResult> StartSessionAsync(ScreenReadOptions options) =>
        StartSessionAsync(null, options);

    public async Task<PortalScreenCastSessionResult> StartSessionAsync(ScreenRect? requestedRegion, ScreenReadOptions options)
    {
        var restoreToken = _restoreTokenStore?.LoadRestoreToken();
        var firstAttempt = await StartSessionAttemptAsync(requestedRegion, options, restoreToken).ConfigureAwait(false);
        if (firstAttempt.Result.IsSuccess || !firstAttempt.CanRetryWithoutRestoreToken)
        {
            return firstAttempt.Result;
        }

        await ClearRestoreTokenAsync().ConfigureAwait(false);
        var retryAttempt = await StartSessionAttemptAsync(requestedRegion, options, restoreToken: null).ConfigureAwait(false);
        return retryAttempt.Result;
    }

    private async Task<StartSessionAttempt> StartSessionAttemptAsync(ScreenRect? requestedRegion, ScreenReadOptions options, string? restoreToken)
    {
        IPortalScreenCastSessionClient? client = null;
        try
        {
            client = await _clientFactory.ConnectAsync().ConfigureAwait(false);
            var session = await client.StartAsync(options, restoreToken).ConfigureAwait(false);
            var validation = PortalStreamGeometry.ValidateMonitorStreams(session.Streams, requestedRegion);
            if (!validation.IsSuccess)
            {
                session.Dispose();
                return new StartSessionAttempt(
                    PortalScreenCastSessionResult.Failure(
                        validation.ErrorKind ?? ScreenReadErrorKind.CaptureFailed,
                        validation.ErrorMessage ?? "XDG Desktop Portal ScreenCast returned unusable monitor metadata."),
                    !string.IsNullOrWhiteSpace(restoreToken));
            }

            if (!string.IsNullOrWhiteSpace(session.RestoreToken))
            {
                await SaveRestoreTokenAsync(session.RestoreToken).ConfigureAwait(false);
            }

            return new StartSessionAttempt(PortalScreenCastSessionResult.Success(session), CanRetryWithoutRestoreToken: false);
        }
        catch (PortalScreenCastException ex)
        {
            return new StartSessionAttempt(PortalScreenCastSessionResult.Failure(ex.ErrorKind, ex.Message), CanRetryWithoutRestoreToken: false);
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
            client?.DisposeIfNotOwnedBySession();
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

    private async Task ClearRestoreTokenAsync()
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

    private readonly record struct StartSessionAttempt(PortalScreenCastSessionResult Result, bool CanRetryWithoutRestoreToken);
}
