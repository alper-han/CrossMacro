namespace CrossMacro.Cli.Services;

public sealed class DoctorCheck
{
    public required string Name { get; init; }

    public required DoctorCheckStatus Status { get; init; }

    public required string Message { get; init; }

    public System.Text.Json.Nodes.JsonObject? Details { get; init; }
}
