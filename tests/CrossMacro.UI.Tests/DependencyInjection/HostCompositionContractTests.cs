using System.Xml.Linq;

namespace CrossMacro.UI.Tests.DependencyInjection;

/// <summary>Checks project boundaries; executable Linux composition is covered by LinuxGuiCompositionTests.</summary>
public sealed class HostCompositionContractTests
{
    [Theory]
    [InlineData("Linux")]
    [InlineData("Windows")]
    [InlineData("MacOS")]
    public void HostProject_ReferencesItsPlatformAndSharesPresentationBootstrap(string platform)
    {
        var root = FindRepositoryRoot();
        var directory = Path.Combine(root, "src", $"CrossMacro.UI.{platform}");
        var project = XDocument.Load(Path.Combine(directory, $"CrossMacro.UI.{platform}.csproj"));
        var references = project.Descendants("ProjectReference")
            .Select(element => ((string?)element.Attribute("Include"))?.Replace('\\', '/'))
            .OfType<string>().Select(Path.GetFileNameWithoutExtension).ToArray();
        Assert.Contains($"CrossMacro.Platform.{platform}", references, StringComparer.Ordinal);
        Assert.Contains("CrossMacro.UI", references, StringComparer.Ordinal);
        Assert.Contains("CrossMacro.UI.Hosting", references, StringComparer.Ordinal);
        Assert.Contains("CrossMacro.Cli", references, StringComparer.Ordinal);
        Assert.Contains("CrossMacro.Mcp", references, StringComparer.Ordinal);
        Assert.DoesNotContain(references, reference => reference is not null
            && reference.StartsWith("CrossMacro.Platform.", StringComparison.Ordinal)
            && !string.Equals(reference, $"CrossMacro.Platform.{platform}", StringComparison.Ordinal));
        Assert.Contains(project.Descendants("AssemblyName"), element => element.Value is "CrossMacro.UI");
        Assert.Contains(project.Descendants("PublishAot"), element => element.Value is "false");
        var policy = XDocument.Load(Path.Combine(root, "Directory.Build.props"));
        Assert.Contains(policy.Descendants("IsAotCompatible"), element => element.Value is "true");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) && Directory.Exists(Path.Combine(directory.FullName, "tests")))
            { return directory.FullName; }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate the CrossMacro repository root.");
    }
}
