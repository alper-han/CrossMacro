using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CrossMacro.CI;

internal static class PackageContracts
{
    private static readonly string[] ExpectedPackagePaths =
    [
        "usr/lib/crossmacro/CrossMacro.UI",
        "usr/lib/crossmacro/daemon/CrossMacro.Daemon",
        "usr/lib/systemd/system/crossmacro.service",
        "usr/lib/udev/rules.d/99-crossmacro.rules",
        "usr/lib/modules-load.d/crossmacro.conf",
        "usr/share/applications/CrossMacro.desktop",
        "usr/share/polkit-1/actions/io.github.alper_han.crossmacro.policy",
        "usr/share/polkit-1/rules.d/50-crossmacro.rules",
    ];

    private static readonly string[] RequiredPackageInputs =
    {
        "scripts/daemon/crossmacro.service",
        "scripts/assets/99-crossmacro.rules",
        "scripts/assets/50-crossmacro.rules",
        "scripts/assets/crossmacro-modules.conf",
        "scripts/assets/CrossMacro.desktop",
        "scripts/assets/io.github.alper_han.crossmacro.policy",
    };

    private const string ExpectedServiceExecStart = "ExecStart=/usr/lib/crossmacro/daemon/CrossMacro.Daemon";

    public static int ValidateCommand(ParsedArguments arguments)
    {
        var root = CISupport.FindRepositoryRoot(arguments.Get("repo-root"));
        var errors = ValidateStatic(root);
        if (!arguments.Has("static-only"))
        {
            var package = arguments.Get("package");
            var kind = arguments.Get("kind");
            if (string.IsNullOrWhiteSpace(package) || string.IsNullOrWhiteSpace(kind))
            {
                errors.Add("--package and --kind are required unless --static-only is used");
            }
            else
            {
                var packagePath = CISupport.ResolvePath(package, Directory.GetCurrentDirectory());
                errors.AddRange(ValidateArchive(packagePath, kind));
            }
        }

        return CISupport.PrintResult("package contract validated", "package contract validation failed", errors);
    }

    private static List<string> ValidateStatic(string root)
    {
        var errors = new List<string>();
        foreach (var relativePath in RequiredPackageInputs)
        {
            var path = Path.Combine(root, relativePath);
            if (!File.Exists(path))
            {
                errors.Add($"required package input is missing: {path}");
            }
        }

        var servicePath = Path.Combine(root, "scripts/daemon/crossmacro.service");
        if (File.Exists(servicePath))
        {
            var serviceExecStart = CISupport.ReadLines(servicePath, true)
                .Select(line => line.Trim())
                .FirstOrDefault(line => line.StartsWith("ExecStart=", StringComparison.Ordinal));
            if (!string.Equals(serviceExecStart, ExpectedServiceExecStart, StringComparison.Ordinal))
            {
                errors.Add($"{servicePath}: expected '{ExpectedServiceExecStart}'");
            }
        }

        return errors;
    }

    private static List<string> ValidateArchive(string package, string kind)
    {
        if (!File.Exists(package))
        {
            return [$"package not found: {package}"];
        }

        kind = kind.Trim().ToLowerInvariant();
        if (kind is not ("deb" or "rpm" or "arch"))
        {
            return [$"unsupported package kind '{kind}'; expected deb, rpm, or arch"];
        }

        var (tool, args) = kind switch
        {
            "deb" => (CISupport.FindExecutable("dpkg-deb"), new[] { "--contents", package }),
            "rpm" => (CISupport.FindExecutable("rpm"), new[] { "-qpl", package }),
            "arch" => (CISupport.FindExecutable("bsdtar") ?? CISupport.FindExecutable("tar"), new[] { "-tf", package }),
            _ => throw new InvalidOperationException($"unhandled package kind '{kind}'"),
        };

        if (tool is null)
        {
            return [$"required package inspection tool is unavailable for {kind}"];
        }

        var (exitCode, output) = CISupport.RunProcess(tool, args, Directory.GetCurrentDirectory());
        if (exitCode != 0)
        {
            return [$"package listing failed for {package}:\n{output}"];
        }

        var entries = ParseArchiveEntries(output, kind)
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .ToHashSet(StringComparer.Ordinal);
        return ExpectedPackagePaths
            .Where(expected => !entries.Contains(expected))
            .Select(expected => $"{package}: missing payload path '{expected}'")
            .ToList();
    }

    private static IEnumerable<string> ParseArchiveEntries(string output, string kind)
    {
        foreach (var line in output.Split('\n'))
        {
            var entry = kind == "deb" ? ParseDpkgContentsPath(line) : line;
            if (!string.IsNullOrWhiteSpace(entry))
            {
                yield return NormalizeEntry(entry);
            }
        }
    }

    private static string? ParseDpkgContentsPath(string line)
    {
        var trimmed = line.Trim();
        // `dpkg-deb --contents` emits an ls-style metadata prefix; archive paths start with `./`.
        var pathStart = trimmed.IndexOf("./", StringComparison.Ordinal);
        return pathStart >= 0 ? trimmed[pathStart..] : null;
    }

    private static string NormalizeEntry(string entry) => entry.Trim().TrimStart('.', '/').TrimEnd('/');
}
