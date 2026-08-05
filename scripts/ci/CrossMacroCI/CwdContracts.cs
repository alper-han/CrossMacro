using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CrossMacro.CI;

internal static class CwdContracts
{
    private static readonly string[] ShellWrappers =
    [
        "scripts/build_deb.sh", "scripts/build_rpm.sh", "scripts/build_appimage.sh", "scripts/build_flatpak.sh", "scripts/build_macos.sh",
    ];

    private static readonly string[] ShellImplementations =
    [
        "scripts/packaging/deb/build.sh", "scripts/packaging/rpm/build.sh", "scripts/packaging/appimage/build.sh",
        "scripts/packaging/flatpak/build.sh", "scripts/packaging/macos/build.sh",
    ];

    private static readonly string[] PowerShellWrappers =
    ["scripts/msix/build-msix.ps1", "scripts/msix/build-msix-store-upload.ps1", "scripts/msix/prepare-msix.ps1"];

    private static readonly string[] PowerShellImplementations =
    [
        "scripts/packaging/msix/build-msix.ps1", "scripts/packaging/msix/build-msix-store-upload.ps1",
        "scripts/packaging/msix/prepare-msix.ps1",
    ];

    private static readonly Dictionary<string, string> ExpectedShellWrapperTargets = new(StringComparer.Ordinal)
    {
        ["scripts/build_deb.sh"] = "packaging/deb/build.sh",
        ["scripts/build_rpm.sh"] = "packaging/rpm/build.sh",
        ["scripts/build_appimage.sh"] = "packaging/appimage/build.sh",
        ["scripts/build_flatpak.sh"] = "packaging/flatpak/build.sh",
        ["scripts/build_macos.sh"] = "packaging/macos/build.sh",
    };

    private static readonly Dictionary<string, string> ExpectedPowerShellWrapperTargets = new(StringComparer.Ordinal)
    {
        ["scripts/msix/build-msix.ps1"] = "../packaging/msix/build-msix.ps1",
        ["scripts/msix/build-msix-store-upload.ps1"] = "../packaging/msix/build-msix-store-upload.ps1",
        ["scripts/msix/prepare-msix.ps1"] = "../packaging/msix/prepare-msix.ps1",
    };

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
        var packageScripts = ShellWrappers.Concat(ShellImplementations).Concat(PowerShellWrappers).Concat(PowerShellImplementations).ToArray();
        foreach (var script in packageScripts.Where(script => !File.Exists(Path.Combine(root, script))))
        {
            errors.Add($"missing expected script: {Path.Combine(root, script)}");
        }

        var existingShellScripts = ShellWrappers.Concat(ShellImplementations)
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
                    var args = new List<string> { "-n" };
                    args.AddRange(existingShellScripts);
                    var (exitCode, output) = CISupport.RunProcess("bash", args, cwd);
                    if (exitCode != 0)
                    {
                        errors.Add($"bash -n package scripts (cwd={cwd}): {output}");
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

        foreach (var (script, target) in ExpectedShellWrapperTargets)
        {
            var path = Path.Combine(root, script);
            if (!File.Exists(path))
            {
                continue;
            }

            var text = CISupport.ReadText(path);
            var expected = new[]
            {
                "SCRIPT_DIR=\"$(cd \"$(dirname \"${BASH_SOURCE[0]}\")\" && pwd)\"",
                $"exec \"$SCRIPT_DIR/{target}\" \"$@\"",
            };
            errors.AddRange(expected.Where(item => !text.Contains(item, StringComparison.Ordinal)).Select(item => $"{script}: missing wrapper delegation: {item}"));
        }

        foreach (var script in ShellImplementations)
        {
            if (File.Exists(Path.Combine(root, script)))
            {
                errors.AddRange(ValidateStaticScript(root, script));
            }
        }

        foreach (var (script, target) in ExpectedPowerShellWrapperTargets)
        {
            var path = Path.Combine(root, script);
            if (!File.Exists(path))
            {
                continue;
            }

            var text = CISupport.ReadText(path, true);
            var expected = new[]
            {
                "$ScriptDir = if ($PSScriptRoot)", $"$TargetScript = Join-Path $ScriptDir '{target}'",
                "$forwardArgs = @", "& $TargetScript @forwardArgs", "exit $LASTEXITCODE",
            };
            errors.AddRange(expected.Where(item => !text.Contains(item, StringComparison.Ordinal)).Select(item => $"{script}: missing wrapper delegation: {item}"));
            errors.AddRange(ValidateStaticScript(root, script));
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
