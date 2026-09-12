namespace CrossMacro.Platform.Linux.Tests.DisplayServer.Wayland;

public sealed class PortalScreenCastSupportProbeTests
{
    [Fact]
    public void TryReadAllText_ReadsConfigWithinBoundedLimit()
    {
        var directory = CreateTemporaryDirectory();
        var path = Path.Combine(directory, "portals.conf");
        try
        {
            File.WriteAllText(path, "[preferred]\norg.freedesktop.impl.portal.ScreenCast=hyprland");

            Assert.Contains("ScreenCast=hyprland", PortalScreenCastSupportProbe.TryReadAllText(path), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void TryReadAllText_RejectsContentBeyondBoundedLimit()
    {
        var directory = CreateTemporaryDirectory();
        var path = Path.Combine(directory, "portals.conf");
        try
        {
            File.WriteAllBytes(path, new byte[(64 * 1024) + 1]);

            Assert.Null(PortalScreenCastSupportProbe.TryReadAllText(path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"crossmacro-portal-probe-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
