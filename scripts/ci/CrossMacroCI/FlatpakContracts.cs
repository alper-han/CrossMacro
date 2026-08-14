using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CrossMacro.CI;

internal static class FlatpakContracts
{
    private static readonly string[] ManifestNames =
    ["io.github.alper_han.crossmacro.yml", "io.github.alper_han.crossmacro.flathub.yml"];

    private static readonly string[] SharedScalars = ["app-id", "runtime", "runtime-version", "sdk", "command"];

    private static readonly string[] SharedBlocks = ["finish-args", "build-options", "cleanup"];

    private static readonly HashSet<string> RequiredFinishArgs =
    [
        "--socket=wayland", "--socket=fallback-x11", "--share=ipc", "--device=all",
        "--talk-name=org.kde.keyboard", "--talk-name=org.kde.KWin", "--talk-name=org.gnome.Shell",
        "--talk-name=org.freedesktop.Flatpak", "--filesystem=xdg-run/hypr:ro",
        "--filesystem=~/.local/share/gnome-shell/extensions:create", "--env=CROSSMACRO_FLATPAK=1",
    ];

    public static int ValidateCommand(ParsedArguments arguments)
    {
        var errors = Validate(CISupport.FindRepositoryRoot(arguments.Get("repo-root")));
        return CISupport.PrintResult("Flatpak manifest invariants validated", "Flatpak manifest invariants are inconsistent", errors);
    }

    private static List<string> Validate(string root)
    {
        var paths = ManifestNames.Select(name => Path.Combine(root, "flatpak", name)).ToArray();
        var errors = new List<string>();
        foreach (var path in paths)
        {
            errors.AddRange(ValidateManifest(path));
        }

        foreach (var path in paths.Where(path => CISupport.ReadText(path).Contains("/run/crossmacro", StringComparison.Ordinal)))
        {
            errors.Add($"{path}: portable Flatpak package must not expose the host daemon socket");
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        var texts = paths.Select(path => CISupport.ReadText(path)).ToArray();
        foreach (var key in SharedScalars)
        {
            var values = texts.Select(text => Scalar(text, key)).ToArray();
            if (!string.Equals(values[0], values[1], StringComparison.Ordinal))
            {
                errors.Add($"shared scalar '{key}' drift: '{values[0]}' != '{values[1]}'");
            }
        }

        foreach (var key in SharedBlocks)
        {
            var blocks = texts.Select(text => NormalizedLines(TopLevelBlock(text, key))).ToArray();
            if (!blocks[0].SequenceEqual(blocks[1], StringComparer.Ordinal))
            {
                errors.Add($"shared block '{key}' drift between local and Flathub manifests");
            }
        }

        for (var index = 0; index < paths.Length; index++)
        {
            var actual = FinishArgs(texts[index]);
            var missing = RequiredFinishArgs.Except(actual).OrderBy(value => value).ToArray();
            if (missing.Length > 0)
            {
                errors.Add($"{paths[index]}: missing finish-args: {string.Join(", ", missing)}");
            }
        }

        if (!texts[0].Contains("- type: dir", StringComparison.Ordinal) || !texts[0].Contains("path: ..", StringComparison.Ordinal))
        {
            errors.Add($"{paths[0]}: local manifest must keep a repository-directory source");
        }

        if (!texts[1].Contains("- type: git", StringComparison.Ordinal)
            || !texts[1].Contains("url: https://github.com/alper-han/CrossMacro.git", StringComparison.Ordinal))
        {
            errors.Add($"{paths[1]}: Flathub manifest must keep its tagged Git source");
        }

        if (!texts[1].Contains("tag: v", StringComparison.Ordinal))
        {
            errors.Add($"{paths[1]}: Flathub manifest must pin a release tag");
        }

        if (!texts.All(text => text.Contains("ln -s ../lib/crossmacro/CrossMacro.UI /app/bin/crossmacro", StringComparison.Ordinal)))
        {
            errors.Add("Flatpak manifests must install the direct CrossMacro.UI launcher symlink");
        }

        if (texts.Any(text => text.Contains("crossmacro.sh", StringComparison.Ordinal)))
        {
            errors.Add("Flatpak manifests must not install the removed hybrid daemon launcher");
        }

        return errors;
    }

    private static List<string> ValidateManifest(string path)
    {
        if (!File.Exists(path))
        {
            return [$"{path}: manifest not found"];
        }

        var text = CISupport.ReadText(path);
        var errors = new List<string>();
        foreach (var key in SharedScalars)
        {
            if (Scalar(text, key) is null)
            {
                errors.Add($"{path}: missing shared scalar '{key}'");
            }
        }

        if (!text.Contains("-p:CrossMacroPublishProfile=native-aot", StringComparison.Ordinal))
        {
            errors.Add($"{path}: Flatpak publish command must select native-aot profile");
        }

        var moduleBlock = TopLevelBlock(text, "modules");
        if (!moduleBlock.Any(line => line.Contains("name: crossmacro", StringComparison.Ordinal)))
        {
            errors.Add($"{path}: crossmacro module is missing");
        }

        if (!moduleBlock.Any(line => line.Contains("buildsystem: simple", StringComparison.Ordinal)))
        {
            errors.Add($"{path}: crossmacro module must use the simple buildsystem");
        }

        return errors;
    }

    private static string? Scalar(string text, string key)
    {
        var match = Regex.Match(text, $"(?m)^{Regex.Escape(key)}:\\s*(.*?)\\s*$");
        return match.Success ? match.Groups[1].Value.Trim().Trim('\'', '"') : null;
    }

    private static List<string> TopLevelBlock(string text, string key)
    {
        var lines = text.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
        var start = lines.FindIndex(line => line == $"{key}:" || line.StartsWith($"{key}:", StringComparison.Ordinal));
        if (start < 0)
        {
            return [];
        }

        var result = new List<string> { lines[start] };
        for (var index = start + 1; index < lines.Count; index++)
        {
            if (lines[index].Length > 0 && !char.IsWhiteSpace(lines[index][0]))
            {
                break;
            }

            result.Add(lines[index]);
        }

        return result;
    }

    private static string[] NormalizedLines(IEnumerable<string> lines) =>
        lines.Where(line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith('#'))
            .Select(line => line.TrimEnd())
            .ToArray();

    private static HashSet<string> FinishArgs(string text)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in TopLevelBlock(text, "finish-args"))
        {
            var match = Regex.Match(line, "^\\s*-\\s*(\\S+)");
            if (match.Success)
            {
                result.Add(match.Groups[1].Value.Trim('\'', '"'));
            }
        }

        return result;
    }
}
