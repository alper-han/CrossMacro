namespace CrossMacro.Mcp.Tools;

public sealed class McpProfileTools(IProfileOperations profileOperations, McpToolAuthorization authorization)
{
    private readonly IProfileOperations _profileOperations = profileOperations ?? throw new ArgumentNullException(nameof(profileOperations));
    private readonly McpToolAuthorization _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));

    [McpServerTool(Name = "profile.list", Title = "List profiles", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpProfilesResult))]
    public Task<McpProfilesResult> ListProfilesAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync("list", new ProfileOperationRequest(ProfileOperationKind.List), cancellationToken);

    [McpServerTool(Name = "profile.current", Title = "Get current profile", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpProfilesResult))]
    public Task<McpProfilesResult> GetCurrentProfileAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync("current", new ProfileOperationRequest(ProfileOperationKind.Current), cancellationToken);

    [McpServerTool(Name = "profile.create", Title = "Create a profile", ReadOnly = false, Destructive = true, Idempotent = false, UseStructuredContent = true, OutputSchemaType = typeof(McpProfilesResult))]
    public Task<McpProfilesResult> CreateProfileAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);
        return ExecuteAsync("create", new ProfileOperationRequest(ProfileOperationKind.Create, DisplayName: name), cancellationToken);
    }

    [McpServerTool(Name = "profile.switch", Title = "Switch profile", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpProfilesResult))]
    public Task<McpProfilesResult> SwitchProfileAsync(string profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return ExecuteAsync("switch", new ProfileOperationRequest(ProfileOperationKind.Switch, Identifier: profile), cancellationToken);
    }

    [McpServerTool(Name = "profile.rename", Title = "Rename a profile", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpProfilesResult))]
    public Task<McpProfilesResult> RenameProfileAsync(string profile, string newName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(newName);
        return ExecuteAsync("rename", new ProfileOperationRequest(ProfileOperationKind.Rename, profile, newName), cancellationToken);
    }

    [McpServerTool(Name = "profile.delete", Title = "Delete a profile", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpProfilesResult))]
    public Task<McpProfilesResult> DeleteProfileAsync(string profile, bool force = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return ExecuteAsync("delete", new ProfileOperationRequest(ProfileOperationKind.Delete, Identifier: profile, Force: force), cancellationToken);
    }

    private async Task<McpProfilesResult> ExecuteAsync(string action, ProfileOperationRequest request, CancellationToken cancellationToken)
    {
        var capability = _authorization.Require(McpCapability.ProfileManage);
        if (capability is not null)
        {
            return new McpProfilesResult(action, capability, [], ActiveProfileId: null);
        }

        var result = await _profileOperations.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        return McpProfileOperationMapper.ToToolResult(action, request.Operation, result);
    }
}
