namespace CrossMacro.Mcp.Tests;

public sealed class McpProfileToolsTests
{
    [Fact]
    public async Task ProfileTools_ShouldMapCliProfileResultsToStructuredProfiles()
    {
        var service = new TestProfileCliService
        {
            ListResult = CliCommandExecutionResult.Ok(
                "2 profile(s).",
                new ProfileListData(
                [
                    new ProfileData("default", "Default", DateTime.UnixEpoch, true),
                    new ProfileData("work", "Work", DateTime.UnixEpoch.AddDays(1), false),
                ],
                "default")),
        };
        var tools = McpToolTestFactory.CreateProfileTools(profileCliService: service);

        var result = await tools.ListProfilesAsync(CancellationToken.None);

        Assert.True(result.Outcome.Success);
        Assert.Equal("default", result.ActiveProfileId);
        Assert.Equal(["default", "work"], result.Profiles.Select(static profile => profile.Id));
        Assert.Equal(1, service.ListCallCount);
    }

    [Fact]
    public async Task ProfileMutation_ShouldRequireProfileManageCapability()
    {
        var policy = new McpCapabilityPolicy(new TestSettingsService(new AppSettings()));
        policy.SetRestricted(true);
        var tools = McpToolTestFactory.CreateProfileTools(capabilityPolicy: policy);

        var result = await tools.CreateProfileAsync("Work", CancellationToken.None);

        Assert.False(result.Outcome.Success);
        Assert.Equal("capability_denied", Assert.Single(result.Outcome.Errors).Code);
    }
}
