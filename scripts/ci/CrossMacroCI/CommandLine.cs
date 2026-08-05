using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace CrossMacro.CI;

internal sealed class ParsedArguments
{
    private Dictionary<string, string?> _options = new(StringComparer.OrdinalIgnoreCase);

    public string? Command { get; init; }

    public bool ShowHelp { get; init; }

    public bool Has(string name) => _options.ContainsKey(name);

    public string? Get(string name) => _options.GetValueOrDefault(name);

    public string GetRequired(string name)
    {
        var value = Get(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"missing required option --{name}");
        }

        return value;
    }

    public static ParsedArguments Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            return new ParsedArguments { ShowHelp = true };
        }

        var index = 0;
        string? command = null;
        if (!args[0].StartsWith("-", StringComparison.Ordinal))
        {
            command = args[0];
            index++;
        }

        var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var showHelp = false;

        while (index < args.Count)
        {
            var token = args[index++];
            if (token is "--help" or "-h")
            {
                showHelp = true;
                continue;
            }

            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"unexpected argument '{token}'");
            }

            var option = token[2..];
            var equals = option.IndexOf('=');
            if (equals >= 0)
            {
                var name = option[..equals];
                options[name] = option[(equals + 1)..];
                continue;
            }

            if (index < args.Count && !args[index].StartsWith("-", StringComparison.Ordinal))
            {
                options[option] = args[index++];
            }
            else
            {
                options[option] = null;
            }
        }

        return new ParsedArguments
        {
            Command = command,
            ShowHelp = showHelp,
            _options = options,
        };
    }

    private ParsedArguments()
    {
    }

}

internal static class CICommandLine
{
    private static readonly string[] Commands =
    [
        "verify-artifacts",
        "expected-artifacts",
        "verify-cwd",
        "verify-docs",
        "verify-flatpak",
        "verify-package",
        "verify-publish",
        "verify-reusable",
        "verify-security",
        "verify-triggers",
    ];

    public static int Run(string[] args)
    {
        try
        {
            var parsed = ParsedArguments.Parse(args);
            if (parsed.ShowHelp && parsed.Command is null)
            {
                PrintRootHelp();
                return 0;
            }

            if (parsed.Command is null)
            {
                PrintRootHelp();
                return 2;
            }

            if (!Commands.Contains(parsed.Command, StringComparer.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine($"Unknown command: {parsed.Command}");
                PrintRootHelp(Console.Error);
                return 2;
            }

            if (parsed.ShowHelp)
            {
                PrintCommandHelp(parsed.Command);
                return 0;
            }

            return parsed.Command.ToLowerInvariant() switch
            {
                "verify-artifacts" => ArtifactContracts.ValidateCommand(parsed),
                "expected-artifacts" => ArtifactContracts.ExpectedArtifactsCommand(parsed),
                "verify-cwd" => CwdContracts.ValidateCommand(parsed),
                "verify-docs" => DocumentationContracts.ValidateCommand(parsed),
                "verify-flatpak" => FlatpakContracts.ValidateCommand(parsed),
                "verify-package" => PackageContracts.ValidateCommand(parsed),
                "verify-publish" => PublishContracts.ValidateCommand(parsed),
                "verify-reusable" => WorkflowContracts.ValidateReusableCommand(parsed),
                "verify-security" => WorkflowContracts.ValidateSecurityCommand(parsed),
                "verify-triggers" => WorkflowContracts.ValidateTriggersCommand(parsed),
                _ => 2,
            };
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"FAIL: {exception.Message}");
            return 2;
        }
    }

    private static void PrintRootHelp(TextWriter? writer = null)
    {
        writer ??= Console.Out;
        writer.WriteLine("CrossMacro CI contract tool (.NET 10 file-based app)");
        writer.WriteLine("Usage: dotnet run --file scripts/ci/CrossMacroCI.cs -- <command> [options]");
        writer.WriteLine();
        writer.WriteLine("Commands:");
        foreach (var command in Commands)
        {
            writer.WriteLine($"  {command}");
        }
        writer.WriteLine();
        writer.WriteLine("Every command accepts --help. Paths default to the repository discovered from the current directory.");
    }

    private static void PrintCommandHelp(string command)
    {
        Console.WriteLine($"Usage: dotnet run --file scripts/ci/CrossMacroCI.cs -- {command} [options]");
        Console.WriteLine();
        switch (command.ToLowerInvariant())
        {
            case "verify-artifacts":
                Console.WriteLine("Validate staged release files against the expected asset manifest.");
                Console.WriteLine("Options: --directory <path> [--manifest <path>] [--version <version>] [--attach-flatpak <bool>] [--attach-msix <bool>]");
                break;
            case "expected-artifacts":
                Console.WriteLine("Print expected release asset filenames, one per line, for checksum generation.");
                Console.WriteLine("Options: [--repo-root <path>] [--manifest <path>] [--version <version>] [--attach-flatpak <bool>] [--attach-msix <bool>]");
                break;
            case "verify-cwd":
                Console.WriteLine("Validate package script path anchoring, wrappers and Bash syntax.");
                Console.WriteLine("Options: [--repo-root <path>]");
                break;
            case "verify-docs":
                Console.WriteLine("Validate documentation links and product-channel contracts.");
                Console.WriteLine("Options: [--repo-root <path>]");
                break;
            case "verify-flatpak":
                Console.WriteLine("Validate local and Flathub Flatpak manifest invariants.");
                Console.WriteLine("Options: [--repo-root <path>]");
                break;
            case "verify-package":
                Console.WriteLine("Validate daemon/package source and optional archive payload contracts.");
                Console.WriteLine("Options: [--repo-root <path>] [--static-only] [--package <path> --kind <deb|rpm|arch>]");
                break;
            case "verify-publish":
                Console.WriteLine("Validate the central AOT/trim publish policy.");
                Console.WriteLine("Options: [--repo-root <path>]");
                break;
            case "verify-reusable":
                Console.WriteLine("Validate reusable workflow placement and workflow_call-only triggers.");
                Console.WriteLine("Options: [--repo-root <path>]");
                break;
            case "verify-security":
                Console.WriteLine("Validate workflow permissions, action pinning and publish gates.");
                Console.WriteLine("Options: [--repo-root <path>] [--workflow <path>]");
                break;
            case "verify-triggers":
                Console.WriteLine("Validate workflow trigger policy.");
                Console.WriteLine("Options: [--repo-root <path>] [--workflow <path>]");
                break;
        }
    }
}
