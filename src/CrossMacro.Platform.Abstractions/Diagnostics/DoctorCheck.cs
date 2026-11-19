using System.Text.Json.Nodes;

namespace CrossMacro.Platform.Abstractions.Diagnostics;

/// <summary>One line of the doctor report; platform backends contribute checks of their own through <see cref="IPlatformDoctorChecks"/>.</summary>
public sealed class DoctorCheck
{
    public required string Name { get; init; }

    public required DoctorCheckStatus Status { get; init; }

    public required string Message { get; init; }

    public JsonObject? Details { get; init; }
}
