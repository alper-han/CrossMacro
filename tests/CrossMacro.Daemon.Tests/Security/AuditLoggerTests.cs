namespace CrossMacro.Daemon.Tests.Security;

using System;
using System.IO;
using CrossMacro.Daemon.Security;
using CrossMacro.Infrastructure.Linux.Native;

[Collection(AuditEnvironmentCollection.Name)]
public sealed class AuditLoggerTests
{
    [Fact]
    public void LogConnectionAttempt_WithCustomDirectory_WritesAuditEntry()
    {
        var directory = CreateTempDirectory();

        try
        {
            var logger = new AuditLogger(directory);

            logger.LogConnectionAttempt(1000, 123, "/usr/bin/crossmacro-ui", success: false, "POLKIT_DENIED");
            logger.Dispose();

            var logPath = Path.Combine(directory, "audit.log");
            Assert.True(File.Exists(logPath));
            var text = File.ReadAllText(logPath);
            Assert.Contains("UID=1000|PID=123|CONNECT_DENIED", text, StringComparison.Ordinal);
            Assert.Contains("exe=/usr/bin/crossmacro-ui reason=POLKIT_DENIED", text, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void LogConnectionAttempt_WhenFieldsContainDelimiters_EscapesAuditValues()
    {
        var directory = CreateTempDirectory();

        try
        {
            var logger = new AuditLogger(directory);

            logger.LogConnectionAttempt(
                1000,
                123,
                "/tmp/app|UID=0\nFAKE",
                success: false,
                "DENIED|reason=bad\rnext");
            logger.Dispose();

            var text = File.ReadAllText(Path.Combine(directory, "audit.log"));
            var lines = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

            var line = Assert.Single(lines);
            Assert.Contains("exe=/tmp/app\\|UID\\=0\\nFAKE", line, StringComparison.Ordinal);
            Assert.Contains("reason=DENIED\\|reason\\=bad\\rnext", line, StringComparison.Ordinal);
            Assert.DoesNotContain("FAKE" + Environment.NewLine, text, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void LogSecurityViolation_WhenViolationContainsControlCharacters_EscapesAuditValues()
    {
        var directory = CreateTempDirectory();

        try
        {
            var logger = new AuditLogger(directory);

            logger.LogSecurityViolation(1004, 357, "BAD|VALUE\nSECOND\u0001");
            logger.Dispose();

            var text = File.ReadAllText(Path.Combine(directory, "audit.log"));
            var line = Assert.Single(text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));

            Assert.Contains("SECURITY_VIOLATION|violation=BAD\\|VALUE\\nSECOND\\u0001", line, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void LogSimulation_WhenDisabled_DoesNotWriteAuditEntry()
    {
        var directory = CreateTempDirectory();

        try
        {
            var logger = new AuditLogger(directory, logSimulations: false);

            logger.LogSimulation(1005, 468, type: 1, code: 2, value: 3);
            logger.Dispose();

            var logPath = Path.Combine(directory, "audit.log");
            Assert.False(File.Exists(logPath));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void LogSimulation_WhenEnabled_WritesSimulationAuditEntry()
    {
        var directory = CreateTempDirectory();

        try
        {
            var logger = new AuditLogger(directory, logSimulations: true);

            logger.LogSimulation(1006, 579, type: 1, code: 272, value: 1);
            logger.Dispose();

            var text = File.ReadAllText(Path.Combine(directory, "audit.log"));
            Assert.Contains("UID=1006|PID=579|SIMULATE|type=1 code=272 value=1", text, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void LogConnectionAttempt_WhenCustomLogExceedsLimit_RotatesExistingAuditLog()
    {
        var directory = CreateTempDirectory();
        var logPath = Path.Combine(directory, "audit.log");

        try
        {
            File.WriteAllText(logPath, new string('x', 32));
            var logger = new AuditLogger(directory, maxFileSizeMB: 0);

            logger.LogConnectionAttempt(1001, 456, null, success: true);
            logger.Dispose();

            Assert.True(File.Exists(Path.Combine(directory, "audit.log.1")));
            var current = File.ReadAllText(logPath);
            Assert.Contains("UID=1001|PID=456|CONNECT_OK", current, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public void Ctor_WhenRuntimeDirectoryEnvironmentExists_UsesItBeforeXdgStateHome()
    {
        var runtimeDirectory = CreateTempDirectory();
        var xdgStateHome = CreateTempDirectory();
        var originalRuntimeDirectory = Environment.GetEnvironmentVariable("RUNTIME_DIRECTORY");
        var originalXdgStateHome = Environment.GetEnvironmentVariable("XDG_STATE_HOME");

        try
        {
            Environment.SetEnvironmentVariable("RUNTIME_DIRECTORY", runtimeDirectory);
            Environment.SetEnvironmentVariable("XDG_STATE_HOME", xdgStateHome);
            var logger = new AuditLogger();

            logger.LogDisconnect(1002, 789, TimeSpan.FromSeconds(1.5));
            logger.Dispose();

            var runtimeLog = Path.Combine(runtimeDirectory, "audit.log");
            var xdgLog = Path.Combine(xdgStateHome, "crossmacro", "audit.log");
            Assert.True(File.Exists(runtimeLog));
            Assert.False(File.Exists(xdgLog));
            Assert.Contains("UID=1002|PID=789|DISCONNECT|duration=1.5s", File.ReadAllText(runtimeLog), StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("RUNTIME_DIRECTORY", originalRuntimeDirectory);
            Environment.SetEnvironmentVariable("XDG_STATE_HOME", originalXdgStateHome);
            DeleteDirectory(runtimeDirectory);
            DeleteDirectory(xdgStateHome);
        }
    }

    [Fact]
    public void Ctor_WhenRuntimeDirectoryEnvironmentMissing_UsesXdgStateHomeFallback()
    {
        if (Directory.Exists(LinuxSystemPaths.RuntimeDirectory))
        {
            return;
        }

        var xdgStateHome = CreateTempDirectory();
        var originalRuntimeDirectory = Environment.GetEnvironmentVariable("RUNTIME_DIRECTORY");
        var originalXdgStateHome = Environment.GetEnvironmentVariable("XDG_STATE_HOME");

        try
        {
            Environment.SetEnvironmentVariable("RUNTIME_DIRECTORY", null);
            Environment.SetEnvironmentVariable("XDG_STATE_HOME", xdgStateHome);
            var logger = new AuditLogger();

            logger.LogSecurityViolation(1003, 246, "PEER_CRED_FAILED");
            logger.Dispose();

            var logPath = Path.Combine(xdgStateHome, "crossmacro", "audit.log");
            Assert.True(File.Exists(logPath));
            Assert.Contains("UID=1003|PID=246|SECURITY_VIOLATION|violation=PEER_CRED_FAILED", File.ReadAllText(logPath), StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("RUNTIME_DIRECTORY", originalRuntimeDirectory);
            Environment.SetEnvironmentVariable("XDG_STATE_HOME", originalXdgStateHome);
            DeleteDirectory(xdgStateHome);
        }
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"crossmacro-audit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
