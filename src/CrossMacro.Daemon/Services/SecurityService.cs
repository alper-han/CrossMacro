using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Daemon.Security;
using Serilog;

namespace CrossMacro.Daemon.Services;

public class SecurityService : ISecurityService
{
    private static readonly TimeSpan AuthorizationCacheTtl = TimeSpan.FromMinutes(2);
    private readonly IRateLimiterService _rateLimiter;
    private readonly ISecurityAuditLogger _auditLogger;
    private readonly IPeerCredentialsProvider _peerCredentials;
    private readonly IPolkitAuthorizationService _polkitAuthorization;
    private readonly Dictionary<uint, DateTime> _authorizedUidCache = [];
    private readonly Lock _authorizationCacheLock = new();

    public SecurityService()
    {
        _rateLimiter = new RateLimiterService(
            new RateLimiter(maxConnectionsPerWindow: 10, windowSeconds: 60, banSeconds: 60));
        _auditLogger = new SecurityAuditLogger(new AuditLogger());
        _peerCredentials = new PeerCredentialsProvider();
        _polkitAuthorization = new PolkitAuthorizationService();
    }

    public SecurityService(
        IRateLimiterService rateLimiter,
        ISecurityAuditLogger auditLogger,
        IPeerCredentialsProvider peerCredentials,
        IPolkitAuthorizationService polkitAuthorization)
    {
        _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        _peerCredentials = peerCredentials ?? throw new ArgumentNullException(nameof(peerCredentials));
        _polkitAuthorization = polkitAuthorization ?? throw new ArgumentNullException(nameof(polkitAuthorization));
    }

    public async Task<(uint Uid, int Pid)?> ValidateConnectionAsync(Socket client)
    {
        // Get peer credentials
        var creds = _peerCredentials.GetCredentials(client);
        if (creds == null)
        {
            Log.Warning("[Security] Failed to get peer credentials, rejecting connection");
            _auditLogger.LogSecurityViolation(0, 0, "PEER_CRED_FAILED");
            client.Dispose();
            return null;
        }
        
        var (uid, gid, pid) = creds.Value;
        var executable = _peerCredentials.GetProcessExecutable(pid);
        
        Log.Information("Client connected: UID={Uid}, GID={Gid}, PID={Pid}, Exe={Exe}", 
            uid, gid, pid, executable ?? "unknown");
        
        // Reject root connections (unless configured otherwise, but default security policy says no)
        if (uid == 0)
        {
            Log.Warning("[Security] Root connection rejected (UID=0)");
            _auditLogger.LogConnectionAttempt(uid, pid, executable, false, "ROOT_REJECTED");
            client.Dispose();
            return null;
        }
        
        // Rate limiting
        if (_rateLimiter.IsRateLimited(uid))
        {
            Log.Warning("[Security] UID {Uid} is rate limited", uid);
            _auditLogger.LogRateLimited(uid, pid);
            client.Dispose();
            return null;
        }
        
        // Check group membership
        if (!_peerCredentials.IsUserInGroup(uid, "crossmacro"))
        {
            Log.Warning("[Security] UID {Uid} is not in 'crossmacro' group", uid);
            _auditLogger.LogConnectionAttempt(uid, pid, executable, false, "NOT_IN_GROUP");
            client.Dispose();
            return null;
        }
        
        // Polkit authorization
        bool polkitAuthorized;
        if (IsUidAuthorizationCached(uid))
        {
            Log.Debug("[Security] Reusing cached polkit authorization for UID {Uid}", uid);
            polkitAuthorized = true;
        }
        else
        {
            try
            {
                polkitAuthorized = await _polkitAuthorization.IsInputCaptureAuthorizedAsync(uid, pid);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Security] Polkit authorization check failed for UID {Uid}", uid);
                RemoveCachedAuthorization(uid);
                _auditLogger.LogConnectionAttempt(uid, pid, executable, false, "POLKIT_ERROR");
                client.Dispose();
                return null;
            }
        }
        
        if (!polkitAuthorized)
        {
            Log.Warning("[Security] Polkit authorization denied for UID {Uid}", uid);
            RemoveCachedAuthorization(uid);
            _auditLogger.LogConnectionAttempt(uid, pid, executable, false, "POLKIT_DENIED");
            client.Dispose();
            return null;
        }

        CacheAuthorization(uid);
        
        // Success
        _auditLogger.LogConnectionAttempt(uid, pid, executable, true);
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

    private bool IsUidAuthorizationCached(uint uid)
    {
        lock (_authorizationCacheLock)
        {
            PruneExpiredAuthorizations(DateTime.UtcNow);
            return _authorizedUidCache.ContainsKey(uid);
        }
    }

    private void CacheAuthorization(uint uid)
    {
        lock (_authorizationCacheLock)
        {
            _authorizedUidCache[uid] = DateTime.UtcNow + AuthorizationCacheTtl;
            PruneExpiredAuthorizations(DateTime.UtcNow);
        }
    }

    private void RemoveCachedAuthorization(uint uid)
    {
        lock (_authorizationCacheLock)
        {
            _authorizedUidCache.Remove(uid);
            PruneExpiredAuthorizations(DateTime.UtcNow);
        }
    }

    private void PruneExpiredAuthorizations(DateTime now)
    {
        if (_authorizedUidCache.Count == 0)
        {
            return;
        }

        List<uint>? expired = null;
        foreach (var kvp in _authorizedUidCache)
        {
            if (kvp.Value <= now)
            {
                expired ??= [];
                expired.Add(kvp.Key);
            }
        }

        if (expired == null)
        {
            return;
        }

        foreach (var uid in expired)
        {
            _authorizedUidCache.Remove(uid);
        }
    }
}
