
namespace CrossMacro.Daemon.Security;

/// <summary>
/// Audit logger for security-relevant events.
/// Logs connection attempts, capture operations, and simulation events.
///
/// NOTE: This class is designed specifically for the Linux daemon and uses
/// Linux-specific paths (systemd runtime directory, XDG state home).
/// It is not intended to be used on Windows or macOS.
/// </summary>
internal sealed class AuditLogger : IDisposable, IAsyncDisposable
{
    private readonly string _logDirectory;
    private readonly string _logPath;
    private readonly Lock _lock = new();
    private readonly bool _logSimulations;
    private readonly int _maxFileSizeBytes;
    private StreamWriter? _writer;

    /// <summary>
    /// Creates a new audit logger.
    /// </summary>
    /// <param name="logDirectory">Directory for audit logs</param>
    /// <param name="logSimulations">Whether to log individual simulation events (high volume)</param>
    /// <param name="maxFileSizeMB">Maximum log file size before rotation</param>
    public AuditLogger(string? logDirectory = null, bool logSimulations = false, int maxFileSizeMB = 10)
    {
        _logDirectory = logDirectory ?? GetDefaultLogDirectory();
        _logPath = Path.Combine(_logDirectory, "audit.log");
        _logSimulations = logSimulations;
        _maxFileSizeBytes = maxFileSizeMB * 1024 * 1024;

        EnsureLogDirectory();
        Log.Information("[AuditLogger] Audit log path: {Path}", _logPath);
    }

    private static string GetDefaultLogDirectory()
    {
        // Priority order:
        // 1. RUNTIME_DIRECTORY (set by systemd) - /run/crossmacro
        // 2. /run/crossmacro if exists (systemd managed)
        // 3. XDG_STATE_HOME/crossmacro (user-level fallback)

        var runtimeDir = Environment.GetEnvironmentVariable("RUNTIME_DIRECTORY");
        if (!string.IsNullOrEmpty(runtimeDir) && Directory.Exists(runtimeDir))
        {
            return runtimeDir;
        }

        // Check if systemd created the directory
        if (Directory.Exists(LinuxSystemPaths.RuntimeDirectory))
        {
            return LinuxSystemPaths.RuntimeDirectory;
        }

        // Fallback to XDG state home (writable by current user)
        var stateHome = Environment.GetEnvironmentVariable("XDG_STATE_HOME")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/state");
        return Path.Combine(stateHome, "crossmacro");
    }

    private void EnsureLogDirectory()
    {
        try
        {
            if (!Directory.Exists(_logDirectory))
            {
                _ = Directory.CreateDirectory(_logDirectory);
                Log.Information("[AuditLogger] Created log directory: {Dir}", _logDirectory);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Warning(ex, "[AuditLogger] Failed to create log directory: {Dir}", _logDirectory);
        }
    }

    /// <summary>
    /// Logs a connection attempt.
    /// </summary>
    public void LogConnectionAttempt(uint uid, int pid, string? executable, bool success, string? reason = null)
    {
        var action = success ? "CONNECT_OK" : "CONNECT_DENIED";
        var details = executable is not null ? FormatField("exe", executable) : "";
        if (!success && reason is not null)
        {
            details += $" {FormatField("reason", reason)}";
        }
        WriteEntry(uid, pid, action, details);
    }

    /// <summary>
    /// Logs a disconnection.
    /// </summary>
    public void LogDisconnect(uint uid, int pid, TimeSpan sessionDuration)
    {
        WriteEntry(uid, pid, "DISCONNECT", string.Create(CultureInfo.InvariantCulture, $"duration={sessionDuration.TotalSeconds:F1}s"));
    }

    /// <summary>
    /// Logs capture start.
    /// </summary>
    public void LogCaptureStart(uint uid, int pid, bool mouse, bool keyboard)
    {
        WriteEntry(uid, pid, "CAPTURE_START", $"mouse={mouse} keyboard={keyboard}");
    }

    /// <summary>
    /// Logs capture stop.
    /// </summary>
    public void LogCaptureStop(uint uid, int pid)
    {
        WriteEntry(uid, pid, "CAPTURE_STOP", "");
    }

    /// <summary>
    /// Logs a simulation event (only if enabled, high volume).
    /// </summary>
    public void LogSimulation(uint uid, int pid, ushort type, ushort code, int value)
    {
        if (!_logSimulations)
        {
            return;
        }

        WriteEntry(uid, pid, "SIMULATE", string.Create(CultureInfo.InvariantCulture, $"type={type} code={code} value={value}"));
    }

    /// <summary>
    /// Logs rate limiting event.
    /// </summary>
    public void LogRateLimited(uint uid, int pid)
    {
        WriteEntry(uid, pid, "RATE_LIMITED", "");
    }

    /// <summary>
    /// Logs a security violation.
    /// </summary>
    public void LogSecurityViolation(uint uid, int pid, string violation)
    {
        WriteEntry(uid, pid, "SECURITY_VIOLATION", FormatField("violation", violation));
    }

    private static string FormatField(string name, string? value)
    {
        return $"{name}={EscapeFieldValue(value)}";
    }

    private static string EscapeFieldValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            _ = builder.Append(character switch
            {
                '\r' => "\\r",
                '\n' => "\\n",
                '|' => "\\|",
                '=' => "\\=",
                _ when char.IsControl(character) => $"\\u{(int)character:X4}",
                _ => character,
            });
        }

        return builder.ToString();
    }

    private void WriteEntry(uint uid, int pid, string action, string details)
    {
        try
        {
            lock (_lock)
            {
                RotateIfNeeded();

                var timestamp = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
                var line = string.IsNullOrEmpty(details)
                    ? string.Create(CultureInfo.InvariantCulture, $"{timestamp}|UID={uid}|PID={pid}|{action}")
                    : string.Create(CultureInfo.InvariantCulture, $"{timestamp}|UID={uid}|PID={pid}|{action}|{details}");

                EnsureWriter();
                _writer?.WriteLine(line);
                _writer?.Flush();
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Debug(ex, "[AuditLogger] Failed to write audit entry");
        }
    }

    private void EnsureWriter()
    {
        if (_writer is null)
        {
            try
            {
                _writer = new StreamWriter(_logPath, append: true);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Log.Warning(ex, "[AuditLogger] Failed to open log file: {Path}", _logPath);
            }
        }
    }

    private void RotateIfNeeded()
    {
        try
        {
            if (!File.Exists(_logPath))
            {
                return;
            }

            var fileInfo = new FileInfo(_logPath);
            if (fileInfo.Length > _maxFileSizeBytes)
            {
                _writer?.Dispose();
                _writer = null;

                // Rotate: audit.log -> audit.log.1, audit.log.1 -> audit.log.2, etc.
                for (int i = 5; i >= 1; i--)
                {
                    var oldPath = i is 1 ? _logPath : string.Create(CultureInfo.InvariantCulture, $"{_logPath}.{i - 1}");
                    var newPath = string.Create(CultureInfo.InvariantCulture, $"{_logPath}.{i}");
                    if (File.Exists(oldPath))
                    {
                        if (File.Exists(newPath))
                        {
                            File.Delete(newPath);
                        }

                        File.Move(oldPath, newPath);
                    }
                }

                Log.Information("[AuditLogger] Rotated log file");
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Log.Debug(ex, "[AuditLogger] Failed to rotate log file");
        }
    }

    /// <summary>
    /// Disposes the audit logger.
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }

    public ValueTask DisposeAsync()
    {
        StreamWriter? writer;
        lock (_lock)
        {
            writer = _writer;
            _writer = null;
        }

        return writer is null ? ValueTask.CompletedTask : DisposeWriterAsync(writer);
    }

    private static async ValueTask DisposeWriterAsync(StreamWriter writer)
    {
        try
        {
            await writer.FlushAsync(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            await writer.DisposeAsync().ConfigureAwait(false);
        }
    }
}
