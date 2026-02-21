namespace CrossMacro.Daemon.Tests.Services;

using System;
using System.IO;
using CrossMacro.Daemon.Services;

public class LinuxPermissionServiceTests
{
    [Fact]
    public void ConfigureSocketPermissions_WhenSocketPathDoesNotExist_DoesNotThrow()
    {
        var service = new LinuxPermissionService();
        var missingPath = Path.Combine(Path.GetTempPath(), $"crossmacro-missing-{Guid.NewGuid():N}.sock");

        var ex = Record.Exception(() => service.ConfigureSocketPermissions(missingPath));

        Assert.Null(ex);
    }

    [Fact]
    public void ConfigureSocketPermissions_WhenSocketPathExists_DoesNotThrow()
    {
        var service = new LinuxPermissionService();
        var socketPath = Path.Combine(Path.GetTempPath(), $"crossmacro-existing-{Guid.NewGuid():N}.sock");
        File.WriteAllText(socketPath, string.Empty);

        try
        {
            var ex = Record.Exception(() => service.ConfigureSocketPermissions(socketPath));
            Assert.Null(ex);
        }
        finally
        {
            if (File.Exists(socketPath))
            {
                File.Delete(socketPath);
            }
        }
    }
}
