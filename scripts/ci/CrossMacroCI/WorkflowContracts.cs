using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CrossMacro.CI;

internal static class WorkflowContracts
{
    private static readonly HashSet<string> ForbiddenTriggers =
    ["push", "pull_request", "pull_request_target", "workflow_dispatch"];

    private static readonly Dictionary<string, HashSet<string>> TargetWorkflows = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ci.yml"] = ["push", "pull_request"],
        ["package-linux.yml"] = ["push", "pull_request"],
        ["package-windows.yml"] = ["push", "pull_request"],
        ["package-macos.yml"] = ["push", "pull_request"],
        ["release.yml"] = ["workflow_dispatch"],
    };

    private const string ReleaseWorkflow = "release.yml";
    private const string CiWorkflow = "ci.yml";
    private const string PagesWorkflow = "pages.yml";
    private const string ReleaseWriteJob = "create-release";
    private const string AurFingerprint = "SHA256:RFzBCUItH9LZS0cKB5UE6ceAYhBD5C8GeOBip8Z11+4";

    private static readonly Dictionary<string, string> PublishJobGates = new(StringComparer.Ordinal)
    {
        ["update-aur"] = "publish_aur",
        ["publish-winget"] = "publish_winget",
    };

    private static readonly string[] ExternalPublishInputs = ["publish_release", "publish_existing_release"];

    public static int ValidateTriggersCommand(ParsedArguments arguments)
    {
        var root = CISupport.FindRepositoryRoot(arguments.Get("repo-root"));
        var errors = new List<string>();
        var workflowOption = arguments.Get("workflow");
        if (!string.IsNullOrWhiteSpace(workflowOption))
        {
            var path = ResolveWorkflow(root, workflowOption);
            if (path is null)
            {
                return CISupport.PrintResult("workflow trigger policy validated", "workflow trigger policy violations found", [$"workflow not found: {workflowOption}"]);
            }

            TargetWorkflows.TryGetValue(Path.GetFileName(path), out var expected);
            errors.AddRange(ValidateTriggers(path, expected));
        }
        else
        {
            foreach (var (name, expected) in TargetWorkflows)
            {
                var path = Path.Combine(root, ".github", "workflows", name);
                if (!File.Exists(path))
                {
                    errors.Add($"{path}: missing target workflow required by CI/CD contract");
                    continue;
                }

                errors.AddRange(ValidateTriggers(path, expected));
            }

            foreach (var path in DiscoverWorkflows(root).Where(path => !TargetWorkflows.ContainsKey(Path.GetFileName(path))))
            {
                errors.AddRange(ValidateTriggers(path, null));
            }
        }

        return CISupport.PrintResult("workflow trigger policy validated", "workflow trigger policy violations found", errors);
    }

    public static int ValidateReusableCommand(ParsedArguments arguments)
    {
        var root = CISupport.FindRepositoryRoot(arguments.Get("repo-root"));
        var workflowDirectory = Path.Combine(root, ".github", "workflows");
        var errors = new List<string>();
        if (!Directory.Exists(workflowDirectory))
        {
            errors.Add($"{workflowDirectory}: workflow directory not found");
        }
        else
        {
            foreach (var path in Directory.EnumerateFiles(workflowDirectory, "_*.yml", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(workflowDirectory, "_*.yaml", SearchOption.AllDirectories)))
            {
                if (!string.Equals(Path.GetDirectoryName(path), workflowDirectory, StringComparison.Ordinal))
                {
                    errors.Add($"{path}: reusable workflows must live directly under .github/workflows, not nested folders");
                    continue;
                }

                var triggers = ExtractTriggers(CISupport.ReadText(path).Split('\n').Select(line => line.TrimEnd('\r')).ToList());
                if (!triggers.SetEquals(["workflow_call"]))
                {
                    errors.Add($"{path}: reusable workflows must use only workflow_call, found [{string.Join(", ", triggers.OrderBy(value => value))}]");
                }

                var forbidden = triggers.Intersect(ForbiddenTriggers).OrderBy(value => value).ToArray();
                if (forbidden.Length > 0)
                {
                    errors.Add($"{path}: reusable workflow has forbidden trigger(s): {string.Join(", ", forbidden)}");
                }
            }
        }

        return CISupport.PrintResult("reusable workflow policy validated", "reusable workflow policy violations found", errors);
    }

    public static int ValidateSecurityCommand(ParsedArguments arguments)
    {
        var root = CISupport.FindRepositoryRoot(arguments.Get("repo-root"));
        var errors = new List<string>();
        var workflowOption = arguments.Get("workflow");
        IEnumerable<string> workflows;
        if (!string.IsNullOrWhiteSpace(workflowOption))
        {
            var path = ResolveWorkflow(root, workflowOption);
            if (path is null)
            {
                return CISupport.PrintResult("workflow security policy validated", "workflow security policy violations found", [$"workflow not found: {workflowOption}"]);
            }

            workflows = [path];
        }
        else
        {
            workflows = DiscoverWorkflows(root);
        }

        if (!workflows.Any())
        {
            errors.Add($"no workflows found under {Path.Combine(root, ".github", "workflows")}");
        }
        else
        {
            foreach (var workflow in workflows)
            {
                errors.AddRange(ValidateSecurity(workflow));
            }
        }

        return CISupport.PrintResult("workflow security policy validated", "workflow security policy violations found", errors);
    }

    private static IEnumerable<string> DiscoverWorkflows(string root)
    {
        var directory = Path.Combine(root, ".github", "workflows");
        if (!Directory.Exists(directory))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateFiles(directory, "*.yml")
            .Concat(Directory.EnumerateFiles(directory, "*.yaml"))
            .OrderBy(path => path, StringComparer.Ordinal);
    }

    private static string? ResolveWorkflow(string root, string path)
    {
        var resolved = CISupport.ResolvePath(path, Directory.GetCurrentDirectory());
        if (File.Exists(resolved))
        {
            return resolved;
        }

        var underRoot = Path.Combine(root, ".github", "workflows", path);
        return File.Exists(underRoot) ? underRoot : null;
    }

    private static List<string> ValidateTriggers(string path, HashSet<string>? expected)
    {
        var errors = new List<string>();
        var text = CISupport.ReadText(path);
        var triggers = ExtractTriggers(text.Split('\n').Select(line => line.TrimEnd('\r')).ToList());
        if (triggers.Count == 0)
        {
            errors.Add($"{path}: missing top-level 'on' workflow triggers");
            return errors;
        }

        var forbidden = triggers.Intersect(["pull_request_target"]).OrderBy(value => value).ToArray();
        if (forbidden.Length > 0)
        {
            errors.Add($"{path}: forbidden trigger(s): {string.Join(", ", forbidden)}");
        }

        if (expected is not null && !triggers.SetEquals(expected))
        {
            errors.Add($"{path}: expected triggers [{string.Join(", ", expected.OrderBy(value => value))}], found [{string.Join(", ", triggers.OrderBy(value => value))}]");
        }

        var fileName = Path.GetFileName(path);
        if (expected is not null && fileName.Equals("release.yml", StringComparison.OrdinalIgnoreCase) && triggers.Contains("push"))
        {
            errors.Add($"{path}: release.yml must be workflow_dispatch only; tag push cannot create releases");
        }

        if (expected is not null
            && fileName is "ci.yml" or "package-linux.yml" or "package-windows.yml" or "package-macos.yml"
            && triggers.Contains("push"))
        {
            if (!PushUsesAllBranches(text))
            {
                errors.Add($"{path}: branch push validation must run on all branches with branches: ['**']");
            }

            if (!triggers.Contains("pull_request"))
            {
                errors.Add($"{path}: branch push validation workflows must also run on pull_request");
            }
        }

        if (PushHasTags(text) && WorkflowCreatesRelease(text))
        {
            errors.Add($"{path}: tag push path appears able to create or upload release assets");
        }

        return errors;
    }

    private static List<string> ValidateSecurity(string path)
    {
        var text = CISupport.ReadText(path);
        var lines = text.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
        var errors = new List<string>();
        var triggers = ExtractTriggers(lines);
        var (permissions, hasPermissions) = PermissionsAtIndent(lines, 0);

        if (text.Contains("pull_request_target", StringComparison.Ordinal))
        {
            errors.Add($"{path}: pull_request_target is forbidden");
        }

        if (!hasPermissions)
        {
            errors.Add($"{path}: missing top-level permissions; expected contents: read");
        }
        else if (!MapsEqual(permissions, new Dictionary<string, string>(StringComparer.Ordinal) { ["contents"] = "read" }))
        {
            errors.Add($"{path}: top-level permissions must be exactly contents: read, found {FormatMap(permissions)}");
        }

        if (Regex.IsMatch(text, "(?m)^\\s*secrets\\s*:\\s*inherit\\s*$"))
        {
            errors.Add($"{path}: broad secrets: inherit is forbidden");
        }

        errors.AddRange(ActionReferenceErrors(text, path));
        if ((Path.GetFileName(path).Equals(ReleaseWorkflow, StringComparison.OrdinalIgnoreCase)
            || Path.GetFileName(path).Equals(CiWorkflow, StringComparison.OrdinalIgnoreCase))
            && text.Contains("ssh-keyscan", StringComparison.Ordinal)
            && !text.Contains(AurFingerprint, StringComparison.Ordinal))
        {
            errors.Add($"{path}: AUR ssh-keyscan must verify the pinned ed25519 fingerprint");
        }

        foreach (var (jobName, start, end, blockLines) in JobBlocks(lines))
        {
            var jobText = string.Join('\n', blockLines);
            var releaseWriteJob = IsReleaseWriteJob(path, jobName, jobText, triggers);
            var pagesDeployJob = IsPagesDeployJob(path, jobName, jobText, triggers, blockLines);
            var secretPublishJob = IsSecretPublishJob(path, jobName, jobText, triggers);
            if (HasWritePermission(jobText) && !releaseWriteJob && !pagesDeployJob)
            {
                errors.Add($"{path}: job '{jobName}' requests write permissions outside the gated create-release job");
            }

            if (jobText.Contains("secrets.", StringComparison.Ordinal) && !secretPublishJob)
            {
                errors.Add($"{path}: job '{jobName}' uses secrets outside a gated manual release/publish job");
            }

            if (PublishJobGates.TryGetValue(jobName, out var publishInput) && !secretPublishJob)
            {
                errors.Add($"{path}: job '{jobName}' must be gated by publish_release=true or publish_existing_release=true, plus {publishInput}=true");
            }

            if (secretPublishJob && jobName.Equals("publish-winget", StringComparison.Ordinal))
            {
                if (!HasReleaseDraftGuard(jobText))
                {
                    errors.Add($"{path}: job '{jobName}' must not publish against draft GitHub releases");
                }

                if (!HasVerifiedWingetCreateDownload(jobText))
                {
                    errors.Add($"{path}: job '{jobName}' must download WinGetCreate from a versioned release and verify SHA256");
                }
            }
        }

        return errors;
    }

    private static List<string> ActionReferenceErrors(string text, string path)
    {
        var errors = new List<string>();
        foreach (Match match in Regex.Matches(text, "(?m)^\\s*uses\\s*:\\s*([^\\s#]+)\\s*(?:#.*)?$"))
        {
            var reference = match.Groups[1].Value.Trim().Trim('\'', '"');
            if (reference.StartsWith("./", StringComparison.Ordinal))
            {
                continue;
            }

            if (!reference.Contains('@'))
            {
                errors.Add($"{path}: external action reference must include an explicit ref: uses: {reference}");
            }
            else if (!Regex.IsMatch(reference, "@[0-9a-fA-F]{40}\\s*(?:#.*)?$"))
            {
                errors.Add($"{path}: action reference must be pinned to a full commit SHA: uses: {reference}");
            }
        }

        return errors;
    }

    private static bool IsReleaseWriteJob(string path, string jobName, string text, HashSet<string> triggers) =>
        Path.GetFileName(path).Equals(ReleaseWorkflow, StringComparison.OrdinalIgnoreCase)
        && jobName.Equals(ReleaseWriteJob, StringComparison.Ordinal)
        && triggers.Contains("workflow_dispatch")
        && HasManualInputGate(text, "publish_release");

    private static bool IsPagesDeployJob(string path, string jobName, string text, HashSet<string> triggers, IReadOnlyList<string> blockLines)
    {
        var jobIndent = blockLines.Count == 0 ? 0 : YamlText.Indent(YamlText.StripComments(blockLines[0]).TrimEnd());
        var (permissions, hasPermissions) = PermissionsAtIndent(blockLines, jobIndent + 2);
        return Path.GetFileName(path).Equals(PagesWorkflow, StringComparison.OrdinalIgnoreCase)
            && jobName.Equals("deploy", StringComparison.Ordinal)
            && triggers.Contains("workflow_dispatch")
            && hasPermissions
            && MapsEqual(
                permissions,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["contents"] = "read",
                    ["pages"] = "write",
                    ["id-token"] = "write",
                });
    }

    private static bool IsSecretPublishJob(string path, string jobName, string text, HashSet<string> triggers)
    {
        if (Path.GetFileName(path).Equals(CiWorkflow, StringComparison.OrdinalIgnoreCase)
            && jobName.Equals("update-aur-git", StringComparison.Ordinal)
            && triggers.Contains("push"))
        {
            return text.Contains("github.event_name == 'push'", StringComparison.Ordinal)
                && text.Contains("github.ref == 'refs/heads/dev'", StringComparison.Ordinal)
                && text.Contains("needs.source-linux.result == 'success'", StringComparison.Ordinal)
                && text.Contains("needs.source-windows.result == 'success'", StringComparison.Ordinal)
                && text.Contains("needs.source-macos.result == 'success'", StringComparison.Ordinal);
        }

        if (!Path.GetFileName(path).Equals(ReleaseWorkflow, StringComparison.OrdinalIgnoreCase) || !triggers.Contains("workflow_dispatch"))
        {
            return false;
        }

        if (jobName.Equals(ReleaseWriteJob, StringComparison.Ordinal))
        {
            return HasManualInputGate(text, "publish_release");
        }

        return PublishJobGates.TryGetValue(jobName, out var publishInput)
            && ExternalPublishInputs.Any(input => HasManualInputGate(text, input))
            && HasManualInputGate(text, publishInput);
    }

    private static bool HasManualInputGate(string text, string inputName) =>
        Regex.IsMatch(text, $"github\\.event\\.inputs\\.{Regex.Escape(inputName)}\\s*==\\s*['\"]true['\"]");

    private static bool HasReleaseDraftGuard(string text) =>
        (text.Contains("github.event.inputs.draft != 'true'", StringComparison.Ordinal)
            || text.Contains("github.event.inputs.draft != \"true\"", StringComparison.Ordinal))
        && (text.Contains("needs.verify-existing-release.outputs.is_draft != 'true'", StringComparison.Ordinal)
            || text.Contains("needs.verify-existing-release.outputs.is_draft != \"true\"", StringComparison.Ordinal));

    private static bool HasVerifiedWingetCreateDownload(string text) =>
        text.Contains("github.com/microsoft/winget-create/releases/download/", StringComparison.Ordinal)
        && text.Contains("WINGETCREATE_SHA256", StringComparison.Ordinal)
        && text.Contains("Get-FileHash", StringComparison.Ordinal)
        && !text.Contains("aka.ms/wingetcreate/latest", StringComparison.Ordinal);

    private static bool HasWritePermission(string text) =>
        Regex.IsMatch(text, "(?m)^\\s+[A-Za-z-]+\\s*:\\s*write\\s*$") || text.Contains("write-all", StringComparison.Ordinal);

    private static string FormatMap(IReadOnlyDictionary<string, string> values) =>
        "{" + string.Join(", ", values.Select(pair => $"{pair.Key}={pair.Value}")) + "}";

    private static bool MapsEqual(IReadOnlyDictionary<string, string> actual, IReadOnlyDictionary<string, string> expected) =>
        actual.Count == expected.Count
        && expected.All(pair => actual.TryGetValue(pair.Key, out var value) && string.Equals(value, pair.Value, StringComparison.Ordinal));

    private static (Dictionary<string, string> Values, bool Found) PermissionsAtIndent(IReadOnlyList<string> lines, int baseIndent)
    {
        var block = YamlText.FindBlock(lines, "permissions", baseIndent);
        if (block is null)
        {
            return (new Dictionary<string, string>(StringComparer.Ordinal), false);
        }

        var (start, end, indent, inline) = block.Value;
        if (!string.IsNullOrWhiteSpace(inline))
        {
            return (new Dictionary<string, string>(StringComparer.Ordinal) { ["__inline__"] = inline.Trim() }, true);
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = start + 1; index < end; index++)
        {
            var line = YamlText.StripComments(lines[index]).TrimEnd();
            var stripped = line.Trim();
            if (string.IsNullOrWhiteSpace(stripped) || YamlText.Indent(line) != indent + 2 || !stripped.Contains(':'))
            {
                continue;
            }

            var separator = stripped.IndexOf(':');
            values[stripped[..separator].Trim()] = stripped[(separator + 1)..].Trim().Trim('\'', '"');
        }

        return (values, true);
    }

    private static HashSet<string> ExtractTriggers(IReadOnlyList<string> lines)
    {
        var block = YamlText.FindOnBlock(lines);
        if (block is null)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var (start, end, indent, inline) = block.Value;
        if (!string.IsNullOrWhiteSpace(inline))
        {
            return YamlText.ParseInlineSet(inline);
        }

        var triggers = new HashSet<string>(StringComparer.Ordinal);
        for (var index = start + 1; index < end; index++)
        {
            var line = YamlText.StripComments(lines[index]).TrimEnd();
            var stripped = line.Trim();
            if (string.IsNullOrWhiteSpace(stripped) || YamlText.Indent(line) != indent + 2)
            {
                continue;
            }

            var trigger = stripped.StartsWith('-')
                ? stripped[1..].Trim().Trim('\'', '"')
                : stripped.Split(':', 2)[0].Trim().Trim('\'', '"');
            if (!string.IsNullOrWhiteSpace(trigger))
            {
                triggers.Add(trigger);
            }
        }

        return triggers;
    }

    private static bool PushHasTags(string text) => Regex.IsMatch(text, "(?ms)^\\s{2,}push\\s*:\\s*$.*?^\\s{4,}tags\\s*:");

    private static bool PushUsesAllBranches(string text) => Regex.IsMatch(text, "(?ms)^\\s{2,}push\\s*:\\s*$.*?^\\s{4,}branches\\s*:\\s*\\[\\s*['\"]\\*\\*['\"]\\s*\\]");

    private static bool WorkflowCreatesRelease(string text) =>
        Regex.IsMatch(text, "softprops/action-gh-release|gh\\s+release\\s+(?:create|upload|edit)|actions/create-release|ncipollo/release-action|github\\.rest\\.repos\\.(?:createRelease|uploadReleaseAsset|updateRelease)");

    private static List<(string Name, int Start, int End, List<string> Lines)> JobBlocks(IReadOnlyList<string> lines)
    {
        var jobs = YamlText.FindBlock(lines, "jobs", 0);
        if (jobs is null)
        {
            return [];
        }

        var (_, jobsEnd, jobsIndent, _) = jobs.Value;
        var result = new List<(string, int, int, List<string>)>();
        var index = jobs.Value.Start + 1;
        while (index < jobsEnd)
        {
            var line = YamlText.StripComments(lines[index]).TrimEnd();
            var stripped = line.Trim();
            if (string.IsNullOrWhiteSpace(stripped) || YamlText.Indent(line) != jobsIndent + 2 || !stripped.Contains(':'))
            {
                index++;
                continue;
            }

            var separator = stripped.IndexOf(':');
            var name = stripped[..separator].Trim().Trim('\'', '"');
            var end = jobsEnd;
            for (var next = index + 1; next < jobsEnd; next++)
            {
                var nextLine = YamlText.StripComments(lines[next]).TrimEnd();
                if (!string.IsNullOrWhiteSpace(nextLine) && YamlText.Indent(nextLine) <= jobsIndent + 2)
                {
                    end = next;
                    break;
                }
            }

            result.Add((name, index, end, lines.Skip(index).Take(end - index).ToList()));
            index = end;
        }

        return result;
    }
}

internal static class YamlText
{
    internal readonly record struct Block(int Start, int End, int Indent, string Inline);

    public static string StripComments(string line)
    {
        var inSingle = false;
        var inDouble = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '\'' && !inDouble)
            {
                inSingle = !inSingle;
            }
            else if (character == '"' && !inSingle)
            {
                inDouble = !inDouble;
            }
            else if (character == '#' && !inSingle && !inDouble)
            {
                return line[..index];
            }
        }

        return line;
    }

    public static int Indent(string line) => line.Length - line.TrimStart(' ').Length;

    public static Block? FindOnBlock(IReadOnlyList<string> lines)
    {
        foreach (var spelling in new[] { "on", "'on'", "\"on\"" })
        {
            var block = FindBlock(lines, spelling, 0);
            if (block is not null)
            {
                return block;
            }
        }

        return null;
    }

    public static Block? FindBlock(IReadOnlyList<string> lines, string key, int baseIndent)
    {
        var pattern = new Regex($"^(?<indent>\\s*){Regex.Escape(key)}\\s*:\\s*(?<inline>.*)$");
        for (var index = 0; index < lines.Count; index++)
        {
            var line = StripComments(lines[index]).TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var match = pattern.Match(line);
            if (!match.Success || match.Groups["indent"].Value.Length != baseIndent)
            {
                continue;
            }

            var indent = match.Groups["indent"].Value.Length;
            var inline = match.Groups["inline"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(inline))
            {
                return new Block(index, index + 1, indent, inline);
            }

            var end = lines.Count;
            for (var next = index + 1; next < lines.Count; next++)
            {
                var nextLine = StripComments(lines[next]).TrimEnd();
                if (!string.IsNullOrWhiteSpace(nextLine) && Indent(nextLine) <= indent)
                {
                    end = next;
                    break;
                }
            }

            return new Block(index, end, indent, string.Empty);
        }

        return null;
    }

    public static HashSet<string> ParseInlineSet(string value)
    {
        value = value.Trim();
        if (value.StartsWith('[') && value.EndsWith(']'))
        {
            return value[1..^1]
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim().Trim('\'', '"'))
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToHashSet(StringComparer.Ordinal);
        }

        return string.IsNullOrWhiteSpace(value)
            ? new HashSet<string>(StringComparer.Ordinal)
            : [value.Trim('\'', '"')];
    }
}
