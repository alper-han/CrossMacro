namespace CrossMacro.Application.Tests.Profiles;

public sealed class ProfileOperationsTests
{
    [Fact]
    public async Task SwitchAsync_ResolvesTheDisplayNameBeforeCallingTheApplicationPort()
    {
        var defaultProfile = new ProfileInfo { Id = "default", Name = "Default", CreatedAt = DateTime.UnixEpoch };
        var workProfile = new ProfileInfo { Id = "work", Name = "Work", CreatedAt = DateTime.UnixEpoch.AddDays(1) };
        var profiles = new[] { defaultProfile, workProfile };
        var manageProfile = Substitute.For<IManageProfile>();
        _ = manageProfile.ListAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new ProfileResult(
            Profile: null,
            Profiles: profiles,
            ActiveProfileId: defaultProfile.Id)));
        _ = manageProfile.SwitchAsync(Arg.Any<ProfileRequest>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(new ProfileResult(
            Profile: workProfile,
            Profiles: profiles,
            ActiveProfileId: workProfile.Id)));
        var operations = new ProfileOperations(manageProfile);

        var result = await operations.ExecuteAsync(
            new ProfileOperationRequest(ProfileOperationKind.Switch, Identifier: "WORK"),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Switched to profile: Work (work).", result.Message);
        Assert.Equal(workProfile, result.AffectedProfile);
        _ = manageProfile.Received(1).SwitchAsync(
            Arg.Is<ProfileRequest>(request => request.Identifier == workProfile.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WithoutForce_ReturnsTypedConfirmationFailureWithoutCallingThePort()
    {
        var manageProfile = Substitute.For<IManageProfile>();
        var operations = new ProfileOperations(manageProfile);

        var result = await operations.ExecuteAsync(
            new ProfileOperationRequest(ProfileOperationKind.Delete, Identifier: "work", Force: false),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ProfileOperationFailureKind.ForceRequired, result.Failure);
        Assert.Equal("profile delete requires --force.", result.Message);
        _ = manageProfile.DidNotReceive().ListAsync(Arg.Any<CancellationToken>());
        _ = manageProfile.DidNotReceive().DeleteAsync(Arg.Any<ProfileRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SwitchAsync_WhenTheProfileDoesNotExist_ReturnsTypedNotFoundFailure()
    {
        var manageProfile = Substitute.For<IManageProfile>();
        _ = manageProfile.ListAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new ProfileResult(
            Profile: null,
            Profiles: [],
            ActiveProfileId: "default")));
        var operations = new ProfileOperations(manageProfile);

        var result = await operations.ExecuteAsync(
            new ProfileOperationRequest(ProfileOperationKind.Switch, Identifier: "missing"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ProfileOperationFailureKind.ProfileNotFound, result.Failure);
        Assert.Equal("Profile not found.", result.Message);
        Assert.Equal("Unknown profile: missing", result.ErrorDetail);
        _ = manageProfile.DidNotReceive().SwitchAsync(Arg.Any<ProfileRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SwitchAsync_WhenThePortFails_ReturnsTypedRuntimeFailure()
    {
        var workProfile = new ProfileInfo { Id = "work", Name = "Work", CreatedAt = DateTime.UnixEpoch };
        var manageProfile = Substitute.For<IManageProfile>();
        _ = manageProfile.ListAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new ProfileResult(
            Profile: null,
            Profiles: [workProfile],
            ActiveProfileId: "default")));
        _ = manageProfile.SwitchAsync(Arg.Any<ProfileRequest>(), Arg.Any<CancellationToken>()).Returns(Task.FromException<ProfileResult>(new IOException("backend unavailable")));
        var operations = new ProfileOperations(manageProfile);

        var result = await operations.ExecuteAsync(
            new ProfileOperationRequest(ProfileOperationKind.Switch, Identifier: workProfile.Id),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ProfileOperationFailureKind.SwitchFailed, result.Failure);
        Assert.Equal("Failed to switch profile.", result.Message);
        Assert.Equal("backend unavailable", result.ErrorDetail);
    }
}
