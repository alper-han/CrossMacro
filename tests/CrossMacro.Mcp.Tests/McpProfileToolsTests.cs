namespace CrossMacro.Mcp.Tests;

public sealed class McpProfileToolsTests
{
    [Fact]
    public async Task ProfileTools_ShouldMapSharedProfileOperationResultsToStructuredProfiles()
    {
        var defaultProfile = new ProfileInfo { Id = "default", Name = "Default", CreatedAt = DateTime.UnixEpoch };
        var workProfile = new ProfileInfo { Id = "work", Name = "Work", CreatedAt = DateTime.UnixEpoch.AddDays(1) };
        var operations = new TestProfileOperations
        {
            Result = new ProfileOperationResult(
                new ProfileResult(
                    Profile: null,
                    Profiles: [defaultProfile, workProfile],
                    ActiveProfileId: defaultProfile.Id),
                AffectedProfile: null,
                Message: "2 profile(s)."),
        };
        var tools = McpToolTestFactory.CreateProfileTools(profileOperations: operations);

        var result = await tools.ListProfilesAsync(CancellationToken.None);

        Assert.True(result.Outcome.Success);
        Assert.Equal("default", result.ActiveProfileId);
        Assert.Equal(["default", "work"], result.Profiles.Select(static profile => profile.Id), StringComparer.Ordinal);
        Assert.Equal(1, operations.CallCount);
        Assert.Equal(ProfileOperationKind.List, operations.LastRequest?.Operation);
    }

    [Fact]
    public async Task ProfileMutation_ShouldRequireProfileManageCapabilityBeforeCallingTheApplicationPort()
    {
        var policy = new McpCapabilityPolicy(new TestSettingsService(new AppSettings()));
        policy.SetRestricted(restricted: true);
        var operations = new TestProfileOperations();
        var tools = McpToolTestFactory.CreateProfileTools(profileOperations: operations, capabilityPolicy: policy);

        var result = await tools.CreateProfileAsync("Work", CancellationToken.None);

        Assert.False(result.Outcome.Success);
        Assert.Equal("capability_denied", Assert.Single(result.Outcome.Errors).Code);
        Assert.Equal(0, operations.CallCount);
    }
}
