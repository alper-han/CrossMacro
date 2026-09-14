namespace CrossMacro.Mcp.Tools;

/// <summary>
/// Adapts shared profile-operation outcomes to MCP command and tool envelopes.
/// </summary>
internal static class McpProfileOperationMapper
{
    public static ProfileOperationRequest ToRequest(ProfileCliOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.Action switch
        {
            ProfileCliAction.List => new(ProfileOperationKind.List),
            ProfileCliAction.Current => new(ProfileOperationKind.Current),
            ProfileCliAction.Create => new(ProfileOperationKind.Create, DisplayName: options.ProfileIdentifier),
            ProfileCliAction.Switch => new(ProfileOperationKind.Switch, Identifier: options.ProfileIdentifier),
            ProfileCliAction.Rename => new(ProfileOperationKind.Rename, options.ProfileIdentifier, options.NewName),
            ProfileCliAction.Delete => new(ProfileOperationKind.Delete, Identifier: options.ProfileIdentifier, Force: options.Force),
            _ => throw new ArgumentOutOfRangeException(nameof(options), options.Action, "Unknown profile CLI action."),
        };
    }

    public static McpProfilesResult ToToolResult(string action, ProfileOperationKind operation, ProfileOperationResult result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentNullException.ThrowIfNull(result);
        if (!result.Success)
        {
            return new McpProfilesResult(action, ToOutcome(result), [], ActiveProfileId: null);
        }

        var snapshot = result.Snapshot ?? throw new InvalidOperationException("A successful profile operation returned no snapshot.");
        if (operation is ProfileOperationKind.List)
        {
            var profiles = snapshot.Profiles
                .Select(profile => ToProfile(profile, string.Equals(profile.Id, snapshot.ActiveProfileId, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
            return new McpProfilesResult(action, ToOutcome(result), profiles, snapshot.ActiveProfileId);
        }

        var profile = result.AffectedProfile ?? throw new InvalidOperationException("A successful profile operation returned no affected profile.");
        var isActive = GetIsActive(operation, profile, snapshot);
        return new McpProfilesResult(action, ToOutcome(result), [ToProfile(profile, isActive)], isActive ? profile.Id : null);
    }

    public static McpToolOutcome ToOutcome(ProfileOperationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Success)
        {
            return McpToolOutcomeMapper.Success(result.Message);
        }

        return result.Failure is ProfileOperationFailureKind.SwitchFailed
            ? McpToolOutcomeMapper.RuntimeError(result.Message)
            : McpToolOutcomeMapper.InvalidArguments(result.Message);
    }

    private static McpProfile ToProfile(ProfileInfo profile, bool isActive) =>
        new(profile.Id, profile.Name, profile.CreatedAt, isActive);

    private static bool GetIsActive(ProfileOperationKind operation, ProfileInfo profile, ProfileResult snapshot)
    {
        if (operation is ProfileOperationKind.Create)
        {
            return true;
        }

        if (operation is ProfileOperationKind.Delete)
        {
            return false;
        }

        if (operation is ProfileOperationKind.Current or ProfileOperationKind.Switch or ProfileOperationKind.Rename)
        {
            return string.Equals(profile.Id, snapshot.ActiveProfileId, StringComparison.OrdinalIgnoreCase);
        }

        throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unexpected profile operation.");
    }
}
