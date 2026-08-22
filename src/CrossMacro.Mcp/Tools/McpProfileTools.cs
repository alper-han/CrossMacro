namespace CrossMacro.Mcp.Tools;

public sealed class McpProfileTools(IProfileCliService profileCliService, McpToolAuthorization authorization)
{
    private readonly IProfileCliService _profileCliService = profileCliService;
    private readonly McpToolAuthorization _authorization = authorization;

    [McpServerTool(Name = "profile.list", Title = "List profiles", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpProfilesResult))]
    public async Task<McpProfilesResult> ListProfilesAsync(CancellationToken cancellationToken = default) =>
        await ExecuteAsync("list", token => _profileCliService.ListAsync(token), cancellationToken).ConfigureAwait(false);

    [McpServerTool(Name = "profile.current", Title = "Get current profile", ReadOnly = true, Destructive = false, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpProfilesResult))]
    public async Task<McpProfilesResult> GetCurrentProfileAsync(CancellationToken cancellationToken = default) =>
        await ExecuteAsync("current", token => _profileCliService.CurrentAsync(token), cancellationToken).ConfigureAwait(false);

    [McpServerTool(Name = "profile.create", Title = "Create a profile", ReadOnly = false, Destructive = true, Idempotent = false, UseStructuredContent = true, OutputSchemaType = typeof(McpProfilesResult))]
    public async Task<McpProfilesResult> CreateProfileAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(name);
        return await ExecuteAsync("create", token => _profileCliService.CreateAsync(name, token), cancellationToken).ConfigureAwait(false);
    }

    [McpServerTool(Name = "profile.switch", Title = "Switch profile", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpProfilesResult))]
    public async Task<McpProfilesResult> SwitchProfileAsync(string profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return await ExecuteAsync("switch", token => _profileCliService.SwitchAsync(profile, token), cancellationToken).ConfigureAwait(false);
    }

    [McpServerTool(Name = "profile.rename", Title = "Rename a profile", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpProfilesResult))]
    public async Task<McpProfilesResult> RenameProfileAsync(string profile, string newName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(newName);
        return await ExecuteAsync("rename", token => _profileCliService.RenameAsync(profile, newName, token), cancellationToken).ConfigureAwait(false);
    }

    [McpServerTool(Name = "profile.delete", Title = "Delete a profile", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true, OutputSchemaType = typeof(McpProfilesResult))]
    public async Task<McpProfilesResult> DeleteProfileAsync(string profile, bool force = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return await ExecuteAsync("delete", token => _profileCliService.DeleteAsync(profile, force, token), cancellationToken).ConfigureAwait(false);
    }

    private async Task<McpProfilesResult> ExecuteAsync(string action, Func<CancellationToken, Task<CliCommandExecutionResult>> executeAsync, CancellationToken cancellationToken)
    {
        var capability = _authorization.Require(McpCapability.ProfileManage);
        if (capability is not null)
        {
            return new McpProfilesResult(action, capability, [], ActiveProfileId: null);
        }

        var result = await executeAsync(cancellationToken).ConfigureAwait(false);
        var profiles = new List<McpProfile>();
        string? activeProfileId = null;
        if (result.Data is ProfileListData list)
        {
            activeProfileId = list.ActiveProfileId;
            profiles.AddRange(list.Profiles.Select(static profile => new McpProfile(profile.Id, profile.Name, profile.CreatedAt, profile.IsActive)));
        }
        else if (result.Data is ProfileData profile)
        {
            activeProfileId = profile.IsActive ? profile.Id : null;
            profiles.Add(new(profile.Id, profile.Name, profile.CreatedAt, profile.IsActive));
        }

        return new(action, McpToolOutcomeMapper.FromCliResultRedactingErrorDetails(result), profiles, activeProfileId);
    }
}
