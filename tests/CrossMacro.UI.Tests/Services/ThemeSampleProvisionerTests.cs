
namespace CrossMacro.UI.Tests.Services;

public sealed class ThemeSampleProvisionerTests : IDisposable
{
    private readonly string _themeDirectory = Path.Combine(Path.GetTempPath(), $"crossmacro-theme-samples-{Guid.NewGuid():N}");

    [Fact]
    public void EnsureProvisioned_WritesTemplateAndReadme()
    {
        _ = Directory.CreateDirectory(_themeDirectory);

        new ThemeSampleProvisioner().EnsureProvisioned(_themeDirectory);

        _ = File.Exists(Path.Combine(_themeDirectory, ThemeSampleProvisioner.TemplateFileName)).Should().BeTrue();
        _ = File.Exists(Path.Combine(_themeDirectory, ThemeSampleProvisioner.ReadmeFileName)).Should().BeTrue();
    }

    [Fact]
    public void EnsureProvisioned_NeverOverwritesExistingFiles()
    {
        _ = Directory.CreateDirectory(_themeDirectory);
        var templatePath = Path.Combine(_themeDirectory, ThemeSampleProvisioner.TemplateFileName);
        File.WriteAllText(templatePath, "user-edited");

        new ThemeSampleProvisioner().EnsureProvisioned(_themeDirectory);

        _ = File.ReadAllText(templatePath).Should().Be("user-edited");
    }

    [Fact]
    public void EnsureProvisioned_WithUnwritablePath_LogsAndDoesNotThrow()
    {
        var act = () => new ThemeSampleProvisioner().EnsureProvisioned("\0invalid");

        _ = act.Should().NotThrow();
    }

    public void Dispose()
    {
        if (Directory.Exists(_themeDirectory))
        {
            Directory.Delete(_themeDirectory, recursive: true);
        }
    }
}
