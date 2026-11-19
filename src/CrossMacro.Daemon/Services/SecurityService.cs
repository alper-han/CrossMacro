
namespace CrossMacro.Daemon.Services;

internal sealed class SecurityService(
    IRateLimiterService rateLimiter,
    ISecurityAuditLogger auditLogger,
    IPeerCredentialsProvider peerCredentials,
    IPolkitAuthorizationService polkitAuthorization) : ISecurityService, IAsyncDisposable
{
    private readonly IRateLimiterService _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
    private readonly ISecurityAuditLogger _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
    private readonly IPeerCredentialsProvider _peerCredentials = peerCredentials ?? throw new ArgumentNullException(nameof(peerCredentials));
    private readonly IPolkitAuthorizationService _polkitAuthorization = polkitAuthorization ?? throw new ArgumentNullException(nameof(polkitAuthorization));

    public async Task<(uint Uid, int Pid)?> ValidateConnectionAsync(Socket client, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var creds = _peerCredentials.GetCredentials(client);
        if (creds is null)
        {
            Log.Warning("[Security] Failed to get peer credentials, rejecting connection");
            _auditLogger.LogSecurityViolation(0, 0, "PEER_CRED_FAILED");
            return null;
        }

        var (uid, gid, pid) = creds.Value;

        Log.Information("Client connected: UID={Uid}, GID={Gid}, PID={Pid}", uid, gid, pid);

        if (!await IsPeerAllowedAsync(uid, pid, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        _auditLogger.LogConnectionAttempt(uid, pid, success: true);
        _rateLimiter.RecordSuccess(uid);

        return (uid, pid);
    }

    public void LogDisconnect(uint uid, int pid, TimeSpan duration)
    {
        _auditLogger.LogDisconnect(uid, pid, duration);
        Log.Information("Client disconnected (session: {Duration}s)", duration.TotalSeconds);
    }

    public void LogCaptureStart(uint uid, int pid, bool mouse, bool kb)
    {
        _auditLogger.LogCaptureStart(uid, pid, mouse, kb);
    }

    public void LogCaptureStop(uint uid, int pid)
    {
        _auditLogger.LogCaptureStop(uid, pid);
    }

    public void LogSimulation(uint uid, int pid, ushort type, ushort code, int value)
    {
        _auditLogger.LogSimulation(uid, pid, type, code, value);
    }

    public ValueTask DisposeAsync()
    {
        return _auditLogger.DisposeAsync();
    }

    private async Task<bool> IsPeerAllowedAsync(uint uid, int pid, CancellationToken cancellationToken)
    {
        if (uid == 0)
        {
            Log.Warning("[Security] Root connection rejected (UID=0)");
            _auditLogger.LogConnectionAttempt(uid, pid, success: false, "ROOT_REJECTED");
            return false;
        }

        if (_rateLimiter.IsRateLimited(uid))
        {
            Log.Warning("[Security] UID {Uid} is rate limited", uid);
            _auditLogger.LogRateLimited(uid, pid);
            return false;
        }

        if (!_peerCredentials.IsUserInGroup(uid, DaemonAccessGroup.Name))
        {
            Log.Warning("[Security] UID {Uid} is not in 'crossmacro' group", uid);
            _auditLogger.LogConnectionAttempt(uid, pid, success: false, "NOT_IN_GROUP");
            return false;
        }

        return await IsPolkitAuthorizedAsync(uid, pid, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> IsPolkitAuthorizedAsync(uint uid, int pid, CancellationToken cancellationToken)
    {
        bool polkitAuthorized;
        try
        {
            polkitAuthorized = await _polkitAuthorization.IsAuthorizedAsync(uid, pid, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[Security] Polkit authorization check failed for UID {Uid}", uid);
            _auditLogger.LogConnectionAttempt(uid, pid, success: false, "POLKIT_ERROR");
            return false;
        }

        if (!polkitAuthorized)
        {
            Log.Warning("[Security] Polkit authorization denied for UID {Uid}", uid);
            _auditLogger.LogConnectionAttempt(uid, pid, success: false, "POLKIT_DENIED");
        }

        return polkitAuthorized;
    }
}
