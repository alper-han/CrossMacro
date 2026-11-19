using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CrossMacro.CI;

internal static class CwdContracts
{
    private static readonly string[] ShellImplementations =
    [
        "scripts/packaging/deb/build.sh", "scripts/packaging/rpm/build.sh", "scripts/packaging/appimage/build.sh",
        "scripts/packaging/flatpak/build.sh", "scripts/packaging/macos/build.sh",
    ];

    private static readonly string[] PowerShellImplementations =
    [
        "scripts/packaging/msix/build-msix.ps1", "scripts/packaging/msix/build-msix-store-upload.ps1",
        "scripts/packaging/msix/prepare-msix.ps1",
    ];

    private static readonly string[] RelativePathPatterns =
    [
        "(?<![A-Za-z0-9_$/{.-])(?:\\.\\./)?src/",
        "(?<![A-Za-z0-9_$/{.-])(?:\\.\\./)?docs/",
        "(?<![A-Za-z0-9_$/{.-])assets/",
        "(?<![A-Za-z0-9_$/{.-])daemon/",
        "(?<![A-Za-z0-9_$/{.-])flatpak/",
        "(?<![A-Za-z0-9_$/{.-])CrossMacro\\.sln",
        "(?<![A-Za-z0-9_$/{.-])README\\.md",
        "(?<![A-Za-z0-9_$/{.-])LICENSE",
    ];

    private static readonly string[] PathAnchors =
    [
        "$PROJECT_ROOT", "$SCRIPTS_DIR", "$projectRoot", "$scriptsDir", "$ProjectRoot", "$ScriptsDir",
        "$ARTIFACT_ROOT", "$APPIMAGE_OUTPUT_DIR", "$APPIMAGE_WORK_DIR", "$FLATPAK_OUTPUT_DIR", "$FLATPAK_WORK_DIR",
    ];

    public static int ValidateCommand(ParsedArguments arguments)
    {
        var root = CISupport.FindRepositoryRoot(arguments.Get("repo-root"));
        var errors = new List<string>();
        var packageScripts = ShellImplementations.Concat(PowerShellImplementations).ToArray();
        foreach (var script in packageScripts.Where(script => !File.Exists(Path.Combine(root, script))))
        {
            errors.Add($"missing expected script: {Path.Combine(root, script)}");
        }

        var existingShellScripts = ShellImplementations
            .Where(script => File.Exists(Path.Combine(root, script)))
            .Select(script => Path.Combine(root, script))
            .ToArray();

        var temporaryDirectory = CreateTemporaryDirectory();
        try
        {
            if (existingShellScripts.Length > 0)
            {
                foreach (var cwd in new[] { root, temporaryDirectory })
                {
                    foreach (var script in existingShellScripts)
                    {
                        var (exitCode, output) = CISupport.RunProcess("bash", ["-n", script], cwd);
                        if (exitCode != 0)
                        {
                            errors.Add($"bash -n {script} (cwd={cwd}): {output}");
                        }
                    }
                }
            }
        }
        finally
        {
            try
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
            catch (IOException)
            {
                // Validation should report script errors, not fail because a temp directory is busy.
            }
            catch (UnauthorizedAccessException)
            {
                // Validation should report script errors, not fail because a temp directory is busy.
            }
        }

        foreach (var script in ShellImplementations)
        {
            if (File.Exists(Path.Combine(root, script)))
            {
                errors.AddRange(ValidateStaticScript(root, script));
            }
        }

        foreach (var script in PowerShellImplementations)
        {
            if (File.Exists(Path.Combine(root, script)))
            {
                errors.AddRange(ValidateStaticScript(root, script));
            }
        }

        return CISupport.PrintResult("packaging scripts are CWD-independent and syntactically valid", "CWD-independence validation failed", errors);
    }

    private static IEnumerable<string> ValidateStaticScript(string root, string relativePath)
    {
        var path = Path.Combine(root, relativePath);
        var text = CISupport.ReadText(path, true);
        foreach (var line in text.Split('\n').Select((value, index) => (value, index + 1)))
        {
            var code = line.value.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(code) || code.TrimStart().StartsWith('#'))
            {
                continue;
            }

            if (RelativePathPatterns.Any(pattern => Regex.IsMatch(code, pattern))
                && !PathAnchors.Any(anchor => code.Contains(anchor, StringComparison.Ordinal)))
            {
                yield return $"{relativePath}: line {line.Item2}: unanchored repo path: {code.Trim()}";
            }
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"crossmacro-cwd-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
