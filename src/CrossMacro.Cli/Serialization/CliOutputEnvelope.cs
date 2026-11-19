using System.Text.Json.Nodes;

namespace CrossMacro.Cli.Serialization;

public sealed record CliOutputEnvelope(
    string Status,
    int Code,
    string Message,
    JsonNode? Data,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors
);
