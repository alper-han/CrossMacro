using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace CrossMacro.CI;

internal static class DocumentationContracts
{
    private static readonly string[] ProductDocs =
    [
        "README.md", "CONTRIBUTING.md", "docs/cli.md", "docs/linux.md", "docs/mcp.md", "docs/macos.md", "docs/man/crossmacro.1",
    ];

    private static readonly string[] CliReferenceCommands =
    ["doctor", "settings get", "schedule list", "shortcut list", "run --step", "screen pixel"];

    private static readonly string[] CliManCommands =
    ["doctor", "settings", "schedule", "shortcut", "screenshot"];

    private static readonly string[] RequiredProductMetadata =
    [
        "docs/man/crossmacro.1", "scripts/assets/CrossMacro.desktop", "scripts/assets/io.github.alper_han.crossmacro.desktop",
        "scripts/assets/io.github.alper_han.crossmacro.metainfo.xml",
        "flatpak/io.github.alper_han.crossmacro.desktop", "flatpak/io.github.alper_han.crossmacro.metainfo.xml",
    ];

    private const string AppImageComponentId = "io.github.alper_han.crossmacro";

    public static int ValidateCommand(ParsedArguments arguments)
    {
        var errors = Validate(CISupport.FindRepositoryRoot(arguments.Get("repo-root")));
        return CISupport.PrintResult("documentation contract validated", "documentation contract validation failed", errors);
    }

    private static List<string> Validate(string root)
    {
        var errors = ValidateLinks(root);
        var cliPath = Path.Combine(root, "docs", "cli.md");
        var manPath = Path.Combine(root, "docs", "man", "crossmacro.1");
        var cli = File.Exists(cliPath) ? CISupport.ReadText(cliPath, true) : string.Empty;
        var man = File.Exists(manPath) ? CISupport.ReadText(manPath, true) : string.Empty;

        foreach (var command in CliReferenceCommands.Where(command => !cli.Contains(command, StringComparison.Ordinal)))
        {
            errors.Add($"docs/cli.md: documented command contract missing: {command}");
        }

        foreach (var command in CliManCommands.Where(command => !man.Contains(command, StringComparison.Ordinal)))
        {
            errors.Add($"docs/man/crossmacro.1: command contract missing: {command}");
        }

        foreach (var relativePath in RequiredProductMetadata.Where(relativePath => !File.Exists(Path.Combine(root, relativePath))))
        {
            errors.Add($"required product metadata is missing: {relativePath}");
        }

        var appImageMetainfo = Path.Combine(root, "scripts", "assets", $"{AppImageComponentId}.metainfo.xml");
        if (!File.Exists(appImageMetainfo))
        {
            errors.Add($"required AppImage metainfo is missing: {appImageMetainfo}");
        }
        else
        {
            try
            {
                var component = XDocument.Load(appImageMetainfo).Root;
                if (!string.Equals(component?.Element("id")?.Value, AppImageComponentId, StringComparison.Ordinal))
                {
                    errors.Add($"{appImageMetainfo}: AppImage component ID must be {AppImageComponentId}");
                }

                var launchable = component?.Element("launchable");
                var expectedDesktop = $"{AppImageComponentId}.desktop";
                if (launchable is null
                    || !string.Equals(launchable.Attribute("type")?.Value, "desktop-id", StringComparison.Ordinal)
                    || !string.Equals(launchable.Value.Trim(), expectedDesktop, StringComparison.Ordinal))
                {
                    errors.Add($"{appImageMetainfo}: launchable must reference {expectedDesktop}");
                }
            }
            catch (XmlException exception)
            {
                errors.Add($"{appImageMetainfo}: invalid XML: {exception.Message}");
            }
        }

        var appImageDesktop = Path.Combine(root, "scripts", "assets", $"{AppImageComponentId}.desktop");
        if (!File.Exists(appImageDesktop))
        {
            errors.Add($"required AppImage desktop file is missing: {appImageDesktop}");
        }
        else
        {
            var desktop = CISupport.ReadText(appImageDesktop, true);
            if (!HasDesktopEntryValue(desktop, "Type", "Application"))
            {
                errors.Add($"{appImageDesktop}: desktop entry must declare Type=Application");
            }

            if (!HasDesktopEntryValue(desktop, "Icon", "crossmacro"))
            {
                errors.Add($"{appImageDesktop}: AppImage icon name must match the bundled root icon");
            }
        }

        return errors;
    }

    private static bool HasDesktopEntryValue(string text, string key, string expectedValue)
    {
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim().TrimEnd('\r');
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            if (string.Equals(line[..separator].Trim(), key, StringComparison.Ordinal)
                && string.Equals(line[(separator + 1)..].Trim(), expectedValue, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> ValidateLinks(string root)
    {
        var errors = new List<string>();
        foreach (var path in DocumentationPaths(root).Where(path => Path.GetExtension(path).Equals(".md", StringComparison.OrdinalIgnoreCase)))
        {
            var text = CISupport.ReadText(path, true);
            foreach (Match match in Regex.Matches(text, "!?\\[[^\\]]+\\]\\(([^)]+)\\)"))
            {
                ValidateLocalTarget(path, match.Groups[1].Value.Trim().Split(' ', 2)[0].Trim('<', '>'), errors);
            }

            foreach (Match match in Regex.Matches(text, "<(?:a|img|source)\\b[^>]+?\\b(?:href|src|srcset)=\\\"([^\\\"]+)\\\"", RegexOptions.IgnoreCase))
            {
                var value = match.Groups[1].Value;
                foreach (var target in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                             .Select(static candidate => candidate.Split(' ', 2)[0]))
                {
                    ValidateLocalTarget(path, target, errors);
                }
            }
        }

        return errors;
    }

    private static void ValidateLocalTarget(string sourcePath, string target, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(target)
            || target.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
            || target.StartsWith('#')
            || target.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var targetParts = target.Split('#', 2);
        var withoutFragment = targetParts[0];
        var resolved = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, withoutFragment));
        if (!File.Exists(resolved) && !Directory.Exists(resolved))
        {
            errors.Add($"{sourcePath}: local documentation link does not exist: {target}");
        }
        else if (targetParts.Length == 2
            && File.Exists(resolved)
            && Path.GetExtension(resolved).Equals(".md", StringComparison.OrdinalIgnoreCase)
            && !HasMarkdownFragment(CISupport.ReadText(resolved, true), targetParts[1]))
        {
            errors.Add($"{sourcePath}: local documentation fragment does not exist: {target}");
        }
    }

    private static bool HasMarkdownFragment(string text, string fragment)
    {
        var decoded = Uri.UnescapeDataString(fragment).Trim();
        if (decoded.Length == 0)
        {
            return true;
        }

        var explicitAnchor = $"(?:id|name)=[\"']{Regex.Escape(decoded)}[\"']";
        if (Regex.IsMatch(text, explicitAnchor, RegexOptions.IgnoreCase))
        {
            return true;
        }

        return text.Split('\n')
            .Select(static line => line.TrimEnd('\r'))
            .Where(static line => Regex.IsMatch(line, "^#{1,6}\\s+"))
            .Select(static line => Regex.Replace(line, "^#{1,6}\\s+", string.Empty).Trim().TrimEnd('#').Trim())
            .Select(ToGitHubHeadingSlug)
            .Any(slug => string.Equals(slug, decoded, StringComparison.OrdinalIgnoreCase));
    }

    private static string ToGitHubHeadingSlug(string heading)
    {
        var withoutMarkup = Regex.Replace(heading, "[`*_~]", string.Empty).ToLowerInvariant();
        var withoutPunctuation = Regex.Replace(withoutMarkup, "[^\\p{L}\\p{N} _-]", string.Empty);
        return Regex.Replace(withoutPunctuation.Trim().Replace(' ', '-'), "-+", "-");
    }

    private static IEnumerable<string> DocumentationPaths(string root)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };

        return Directory.EnumerateFiles(root, "*.md", options)
            .Concat(ProductDocs.Select(relative => Path.Combine(root, relative)))
            .Where(path => File.Exists(path))
            .Where(path => !IsBuildArtifact(path, root))
            .Distinct(StringComparer.Ordinal);
    }

    private static bool IsBuildArtifact(string path, string root)
    {
        var relative = Path.GetRelativePath(root, path);
        return relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is "bin" or "obj" or ".git" or ".flatpak-builder" or "node_modules");
    }
}
