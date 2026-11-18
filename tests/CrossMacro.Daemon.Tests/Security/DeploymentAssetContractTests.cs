using CrossMacro.Daemon.Contracts.Security;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace CrossMacro.Daemon.Tests.Security;

/// <summary>
/// The daemon only works when the shipped polkit policy, polkit rules and systemd unit agree with
/// the constants compiled into the daemon.
/// </summary>
public sealed partial class DeploymentAssetContractTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void PolkitPolicyAndRules_DeclareExactlyTheActionsTheDaemonChecks()
    {
        var policy = ReadPolicy();
        var policyActions = policy.Root!.Elements("action")
            .Select(action => (string?)action.Attribute("id"))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var rulesActions = ActionIdRegex
            .Matches(ReadRepoFile("scripts/assets/50-crossmacro.rules"))
            .Select(match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var expectedActions = PolkitActions.All.Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(expectedActions, policyActions);
        Assert.Equal(expectedActions, rulesActions);
    }

    [Fact]
    public void PolkitPolicy_CaptureActionImpliesSimulation()
    {
        var capture = ReadPolicy().Root!.Elements("action")
            .Single(action => string.Equals((string?)action.Attribute("id"), PolkitActions.InputCapture, StringComparison.Ordinal));

        var implied = capture.Elements("annotate")
            .Single(annotation => string.Equals((string?)annotation.Attribute("key"), "org.freedesktop.policykit.imply", StringComparison.Ordinal));

        Assert.Equal(PolkitActions.InputSimulate, implied.Value);
    }

    [Fact]
    public void PolkitPolicy_NamesTheServiceUserAsOwnerOfTheActionTheDaemonChecks()
    {
        // polkit refuses a check of another user's process unless the caller is root or an owner.
        var checkedAction = ReadPolicy().Root!.Elements("action")
            .Single(action => string.Equals((string?)action.Attribute("id"), PolkitChecker.CheckedAction, StringComparison.Ordinal));

        var owners = checkedAction.Elements("annotate")
            .Single(annotation => string.Equals((string?)annotation.Attribute("key"), "org.freedesktop.policykit.owner", StringComparison.Ordinal))
            .Value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        Assert.Contains("unix-user:" + ReadServiceSection()["User"], owners, StringComparer.Ordinal);
    }

    [Fact]
    public void SystemdUnit_RuntimeDirectoryHoldsTheDefaultSocketAndStaysGroupOnly()
    {
        var service = ReadServiceSection();

        Assert.Equal(
            Path.GetFileName(Path.GetDirectoryName(IpcProtocol.DefaultSocketPath)),
            service["RuntimeDirectory"]);
        Assert.Equal("0750", service["RuntimeDirectoryMode"]);
    }

    private static XDocument ReadPolicy()
    {
        using var reader = XmlReader.Create(
            Path.Combine(RepoRoot, "scripts/assets/io.github.alper_han.crossmacro.policy"),
            new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
        return XDocument.Load(reader);
    }

    private static Dictionary<string, string> ReadServiceSection()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var inService = false;

        foreach (var rawLine in ReadRepoFile("scripts/daemon/crossmacro.service").Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith('['))
            {
                inService = string.Equals(line, "[Service]", StringComparison.Ordinal);
                continue;
            }

            if (!inService || line.Length is 0 || line[0] is '#')
            {
                continue;
            }

            var separator = line.IndexOf('=', StringComparison.Ordinal);
            values[line[..separator]] = line[(separator + 1)..];
        }

        return values;
    }

    private static string ReadRepoFile(string relativePath) =>
        File.ReadAllText(Path.Combine(RepoRoot, relativePath));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CrossMacro.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
    }

    [GeneratedRegex("io\\.github\\.alper_han\\.crossmacro\\.input-(?:capture|simulate)", RegexOptions.NonBacktracking)]
    private static partial Regex ActionIdRegex { get; }
}
