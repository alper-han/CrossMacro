using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CrossMacro.CI;

internal static class PublishContracts
{
    private static readonly string[] NativeAotRecipes =
    [
        "scripts/ci/publish-linux-artifacts.sh",
        "scripts/daemon/install.sh",
        "scripts/packaging/deb/build.sh",
        "scripts/packaging/rpm/build.sh",
        "scripts/packaging/macos/build.sh",
        "scripts/packaging/msix/build-msix.ps1",
        "scripts/packaging/arch/PKGBUILD",
        "flatpak/io.github.alper_han.crossmacro.yml",
        "flatpak/io.github.alper_han.crossmacro.flathub.yml",
        "flake.nix",
        "scripts/flatpak-dotnet-generator.sh",
    ];

    private static readonly string[] PortableTrimmedRecipes =
    ["scripts/ci/publish-windows-portable.ps1"];

    private static readonly string[] CentralizedPublishFlags =
    [
        "--self-contained", "-p:PublishAot=", "-p:PublishReadyToRun=", "-p:OptimizationPreference=",
        "-p:StripSymbols=", "-p:IlcTrimMetadata=", "-p:DebugType=", "-p:DebugSymbols=",
        "-p:PublishTrimmed=", "-p:TrimMode=", "-p:PublishSingleFile=",
    ];

    public static int ValidateCommand(ParsedArguments arguments)
    {
        var root = CISupport.FindRepositoryRoot(arguments.Get("repo-root"));
        var errors = new List<string>();
        var targetsPath = Path.Combine(root, "Directory.Build.targets");
        if (!File.Exists(targetsPath))
        {
            errors.Add($"missing publish policy file: {targetsPath}");
        }
        else
        {
            var targets = CISupport.ReadText(targetsPath);
            foreach (var required in new[] { "CrossMacroPublishProfile", "native-aot", "portable-trimmed", "SelfContained", "PublishReadyToRun", "PublishTrimmed" })
            {
                if (!targets.Contains(required, StringComparison.Ordinal))
                {
                    errors.Add($"Directory.Build.targets: publish policy is missing '{required}'");
                }
            }
        }

        errors.AddRange(ValidateRecipes(root, NativeAotRecipes, "native-aot"));
        errors.AddRange(ValidateRecipes(root, PortableTrimmedRecipes, "portable-trimmed"));
        return CISupport.PrintResult("publish matrix validated", "publish matrix validation failed", errors);
    }

    private static IEnumerable<string> ValidateRecipes(string root, IEnumerable<string> recipes, string profile)
    {
        foreach (var relativePath in recipes)
        {
            var path = Path.Combine(root, relativePath);
            if (!File.Exists(path))
            {
                yield return $"missing {profile} recipe: {path}";
                continue;
            }

            var text = CISupport.ReadText(path, replaceInvalid: true);
            var selector = $"CrossMacroPublishProfile={profile}";
            if (!text.Contains(selector, StringComparison.Ordinal))
            {
                yield return $"{path}: must select {selector}";
            }

            foreach (var flag in FindCentralizedPublishFlags(text))
            {
                yield return $"{path}: repeated publish flag '{flag}' should be owned by Directory.Build.targets";
            }
        }
    }

    private static IEnumerable<string> FindCentralizedPublishFlags(string text)
    {
        var codeLines = text.Split('\n')
            .Select(line => line.TrimEnd('\r').TrimStart())
            .Where(line => !string.IsNullOrWhiteSpace(line)
                && !line.StartsWith("#", StringComparison.Ordinal)
                && !line.StartsWith("//", StringComparison.Ordinal)
                && !line.StartsWith("<!--", StringComparison.Ordinal));

        foreach (var flag in CentralizedPublishFlags.Where(flag => codeLines.Any(line => line.Contains(flag, StringComparison.Ordinal))))
        {
            yield return flag;
        }
    }
}
