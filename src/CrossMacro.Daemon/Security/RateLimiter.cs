using System;
using System.Collections.Generic;
using Serilog;

namespace CrossMacro.Daemon.Security;

/// <summary>
/// Rate limiter to prevent DoS attacks via excessive connection attempts.
/// Tracks connection attempts per UID and blocks if limit is exceeded.
/// </summary>
public class RateLimiter
{
    private readonly Dictionary<uint, ConnectionRecord> _connectionAttempts = new();
    private readonly object _lock = new();
    
    private readonly int _maxConnectionsPerWindow;
    private readonly TimeSpan _windowDuration;
    private readonly TimeSpan _banDuration;

    /// <summary>
    /// Creates a new rate limiter with specified limits.
    /// </summary>
    /// <param name="maxConnectionsPerWindow">Maximum connections allowed in the time window</param>
    /// <param name="windowSeconds">Time window duration in seconds</param>
    /// <param name="banSeconds">Ban duration in seconds after limit exceeded</param>
    public RateLimiter(int maxConnectionsPerWindow = 10, int windowSeconds = 60, int banSeconds = 60)
    {
        _maxConnectionsPerWindow = maxConnectionsPerWindow;
        _windowDuration = TimeSpan.FromSeconds(windowSeconds);
        _banDuration = TimeSpan.FromSeconds(banSeconds);
    }

    /// <summary>
    /// Checks if a UID is currently rate limited.
    /// </summary>
    /// <param name="uid">The user ID to check</param>
    /// <returns>True if rate limited, false otherwise</returns>
    public bool IsRateLimited(uint uid)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            CleanupExpired(now);

            if (_connectionAttempts.TryGetValue(uid, out var record))
            {
                // Check if currently banned
                if (record.BannedUntil.HasValue && now < record.BannedUntil.Value)
                {
                    var remaining = record.BannedUntil.Value - now;
                    Log.Warning("[RateLimiter] UID {Uid} is banned for {Seconds}s more", uid, (int)remaining.TotalSeconds);
                    return true;
                }

                // Reset if window expired
                if (now - record.WindowStart > _windowDuration)
                {
                    record.WindowStart = now;
                    record.Count = 1;
                    record.BannedUntil = null;
                    return false;
                }

                // Increment and check limit
                record.Count++;
                if (record.Count > _maxConnectionsPerWindow)
                {
                    record.BannedUntil = now + _banDuration;
                    Log.Warning("[RateLimiter] UID {Uid} exceeded rate limit ({Count}/{Max}), banned for {Seconds}s",
                        uid, record.Count, _maxConnectionsPerWindow, (int)_banDuration.TotalSeconds);
                    return true;
                }

                return false;
            }
            else
            {
                // First connection from this UID
                _connectionAttempts[uid] = new ConnectionRecord
                {
                    WindowStart = now,
                    Count = 1,
                    BannedUntil = null
                };
                return false;
            }
        }
    }

    /// <summary>
    /// Records a successful connection (resets the rate limit counter for the UID).
    /// </summary>
    public void RecordSuccess(uint uid)
    {
        lock (_lock)
        {
            if (_connectionAttempts.TryGetValue(uid, out var record))
            {
                // Reset on successful connection to be less aggressive
                record.Count = Math.Max(0, record.Count - 1);
            }
        }
    }

    /// <summary>
    /// Gets the current status of rate limiting for a UID.
    /// </summary>
    public (int attemptCount, bool isBanned) GetStatus(uint uid)
    {
        lock (_lock)
        {
            if (_connectionAttempts.TryGetValue(uid, out var record))
            {
                var isBanned = record.BannedUntil.HasValue && DateTime.UtcNow < record.BannedUntil.Value;
                return (record.Count, isBanned);
            }
            return (0, false);
        }
    }

    private void CleanupExpired(DateTime now)
    {
        var toRemove = new List<uint>();
        foreach (var kvp in _connectionAttempts)
        {
            var record = kvp.Value;
            // Remove entries older than window + ban duration
            if (now - record.WindowStart > _windowDuration + _banDuration)
            {
                toRemove.Add(kvp.Key);
            }
        }

        foreach (var uid in toRemove)
        {
            _connectionAttempts.Remove(uid);
        }
    }

    private class ConnectionRecord
    {
        public DateTime WindowStart { get; set; }
        public int Count { get; set; }
        public DateTime? BannedUntil { get; set; }
    }
}
