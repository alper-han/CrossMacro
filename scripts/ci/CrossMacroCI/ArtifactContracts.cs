using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace CrossMacro.CI;

internal static class ArtifactContracts
{
    public static int ValidateCommand(ParsedArguments arguments)
    {
        var root = CISupport.FindRepositoryRoot(arguments.Get("repo-root"));
        var manifestArgument = arguments.Get("manifest");
        var manifest = CISupport.ResolvePath(
            manifestArgument ?? Path.Combine(root, "scripts/ci/expected-release-assets.json"),
            Directory.GetCurrentDirectory());
        var directory = CISupport.ResolvePath(arguments.GetRequired("directory"), Directory.GetCurrentDirectory());
        var attachFlatpak = arguments.Get("attach-flatpak") is null
            || CISupport.ParseBoolean(arguments.Get("attach-flatpak"), "attach-flatpak");
        var attachMsix = arguments.Get("attach-msix") is null
            || CISupport.ParseBoolean(arguments.Get("attach-msix"), "attach-msix");
        var version = string.IsNullOrWhiteSpace(arguments.Get("version")) ? null : arguments.Get("version");
        var errors = Validate(manifest, directory, attachFlatpak, attachMsix, version);
        return CISupport.PrintResult(
            "release artifact directory contains every required enabled asset",
            "release artifact validation failed",
            errors);
    }

    public static int ExpectedArtifactsCommand(ParsedArguments arguments)
    {
        var root = CISupport.FindRepositoryRoot(arguments.Get("repo-root"));
        var manifestArgument = arguments.Get("manifest");
        var manifestPath = CISupport.ResolvePath(
            manifestArgument ?? Path.Combine(root, "scripts/ci/expected-release-assets.json"),
            Directory.GetCurrentDirectory());
        var attachFlatpak = arguments.Get("attach-flatpak") is null
            || CISupport.ParseBoolean(arguments.Get("attach-flatpak"), "attach-flatpak");
        var attachMsix = arguments.Get("attach-msix") is null
            || CISupport.ParseBoolean(arguments.Get("attach-msix"), "attach-msix");

        var manifest = LoadManifest(manifestPath);
        var version = string.IsNullOrWhiteSpace(arguments.Get("version")) ? null : arguments.Get("version");
        manifest = RewriteManifestForVersion(manifest, version);
        foreach (var asset in ExpectedAssets(manifest, attachFlatpak, attachMsix))
        {
            var file = asset["file"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(file) && !string.Equals(file, "SHA256SUMS", StringComparison.Ordinal))
            {
                Console.WriteLine(file);
            }
        }

        return 0;
    }

    public static List<string> Validate(
        string manifestPath,
        string directory,
        bool attachFlatpak,
        bool attachMsix,
        string? version)
    {
        var errors = new List<string>();
        JsonObject manifest;
        try
        {
            manifest = RewriteManifestForVersion(LoadManifest(manifestPath), version);
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException)
        {
            return [exception.Message];
        }

        if (File.Exists(directory))
        {
            errors.Add($"artifact path is not a directory: {directory}");
            return errors;
        }

        if (!Directory.Exists(directory))
        {
            errors.Add($"artifact directory not found: {directory}");
            return errors;
        }

        var expectedNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var asset in ExpectedAssets(manifest, attachFlatpak, attachMsix))
        {
            var fileName = asset["file"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(fileName))
            {
                errors.Add($"manifest asset missing non-empty file field: {asset.ToJsonString()}");
                continue;
            }

            expectedNames.Add(fileName);
            if (!File.Exists(Path.Combine(directory, fileName)))
            {
                var details = new List<string>();
                foreach (var key in new[] { "kind", "platform", "arch" })
                {
                    var value = asset[key]?.GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        details.Add($"{key}={value}");
                    }
                }

                var suffix = details.Count == 0 ? string.Empty : $" ({string.Join(", ", details)})";
                errors.Add($"missing artifact: {fileName}{suffix}");
            }
        }

        foreach (var actual in Directory.EnumerateFiles(directory).Select(Path.GetFileName).Where(name => name is not null).Cast<string>())
        {
            if (!expectedNames.Contains(actual))
            {
                errors.Add($"unexpected artifact not in manifest: {actual}");
            }
        }

        return errors;
    }

    private static JsonObject LoadManifest(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"manifest not found: {path}", path);
        }

        var manifest = JsonNode.Parse(CISupport.ReadText(path)) as JsonObject
            ?? throw new InvalidDataException("manifest root must be a JSON object");
        if (manifest["assets"] is not JsonArray)
        {
            throw new InvalidDataException("manifest must contain an assets array");
        }

        return manifest;
    }

    private static JsonObject RewriteManifestForVersion(JsonObject manifest, string? version)
    {
        if (version is null)
        {
            return manifest;
        }

        var sampleVersion = manifest["sampleVersion"]?.GetValue<string>() ?? string.Empty;
        if (string.Equals(sampleVersion, version, StringComparison.Ordinal))
        {
            return manifest;
        }

        var rewritten = JsonNode.Parse(manifest.ToJsonString())!.AsObject();
        rewritten["sampleVersion"] = version;
        if (rewritten["releaseTag"]?.GetValue<string>() is { } releaseTag
            && releaseTag.StartsWith('v'))
        {
            rewritten["releaseTag"] = $"v{version}";
        }

        if (rewritten["assets"] is JsonArray assets)
        {
            foreach (var node in assets)
            {
                if (node is not JsonObject asset || asset["file"]?.GetValue<string>() is not { } fileName)
                {
                    continue;
                }

                asset["file"] = RewriteAssetFileName(asset, fileName, sampleVersion, version);
            }
        }

        return rewritten;
    }

    private static string RewriteAssetFileName(JsonObject asset, string fileName, string sampleVersion, string version)
    {
        var kind = asset["kind"]?.GetValue<string>();
        return kind switch
        {
            "deb" => fileName.Replace(ToDebVersion(sampleVersion), ToDebVersion(version), StringComparison.Ordinal),
            "rpm" => fileName.Replace(
                $"{ToRpmVersion(sampleVersion)}-{ToRpmRelease(sampleVersion)}",
                $"{ToRpmVersion(version)}-{ToRpmRelease(version)}",
                StringComparison.Ordinal),
            _ => fileName.Replace(ToFilenameVersion(sampleVersion), ToFilenameVersion(version), StringComparison.Ordinal),
        };
    }

    private static List<JsonObject> ExpectedAssets(JsonObject manifest, bool attachFlatpak, bool attachMsix)
    {
        var result = new List<JsonObject>();
        var toggles = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["attach_flatpak"] = attachFlatpak,
            ["attach_msix"] = attachMsix,
        };

        if (manifest["assets"] is not JsonArray assets)
        {
            return result;
        }

        foreach (var node in assets)
        {
            if (node is not JsonObject asset
                || asset["enabledByDefault"]?.GetValue<bool>() is not true)
            {
                continue;
            }

            var manualInput = asset["manualInput"]?.GetValue<string>();
            if (manualInput is not null && toggles.TryGetValue(manualInput, out var enabled) && !enabled)
            {
                continue;
            }

            result.Add(asset);
        }

        return result;
    }

    private static (string Base, string Prerelease) ParseSemverParts(string version)
    {
        var baseVersion = Regex.Split(version, "[-+]")[0];
        if (!Regex.IsMatch(baseVersion, "^[0-9]+\\.[0-9]+\\.[0-9]+$"))
        {
            throw new InvalidDataException($"invalid semantic base version '{baseVersion}' from '{version}'");
        }

        var prerelease = string.Empty;
        var dash = version.IndexOf('-');
        if (dash >= 0)
        {
            prerelease = version[(dash + 1)..].Split('+', 2)[0];
        }

        return (baseVersion, prerelease);
    }

    private static string NormalizeToken(string token, string kind)
    {
        var value = kind switch
        {
            "deb" => Regex.Replace(token, "[^0-9A-Za-z.+~-]", "."),
            "rpm" or "aur" => Regex.Replace(Regex.Replace(token, "[-+]", "."), "[^0-9A-Za-z._]", "."),
            "filename" => Regex.Replace(token, "[^0-9A-Za-z._+-]", "."),
            _ => token,
        };
        return Regex.Replace(value, "\\.+", ".").Trim('.');
    }

    private static string ToDebVersion(string version)
    {
        var (baseVersion, prerelease) = ParseSemverParts(version);
        return string.IsNullOrEmpty(prerelease) ? baseVersion : $"{baseVersion}~{NormalizeToken(prerelease, "deb") switch { "" => "pre", var value => value }}";
    }

    private static string ToRpmVersion(string version) => ParseSemverParts(version).Base;

    private static string ToRpmRelease(string version)
    {
        var prerelease = ParseSemverParts(version).Prerelease;
        return string.IsNullOrEmpty(prerelease) ? "1" : $"0.1.{NormalizeToken(prerelease, "rpm") switch { "" => "pre", var value => value }}";
    }

    private static string ToFilenameVersion(string version)
    {
        var normalized = NormalizeToken(version, "filename");
        return string.IsNullOrEmpty(normalized)
            ? throw new InvalidDataException($"failed to normalize filename version from '{version}'")
            : normalized;
    }
}
