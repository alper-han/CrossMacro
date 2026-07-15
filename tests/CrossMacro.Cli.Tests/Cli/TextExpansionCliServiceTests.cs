using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Cli;
using CrossMacro.Cli.Services;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;
using CrossMacro.Infrastructure.Services;
using NSubstitute;

namespace CrossMacro.Cli.Tests;

public class TextExpansionCliServiceTests
{
    [Fact]
    public async Task AddAsync_WithDuplicateTrigger_ReturnsInvalidArguments()
    {
        var storage = Substitute.For<ITextExpansionStorageService>();
        storage.LoadAsync().Returns(new List<TextExpansion> { new(":mail", "old") });
        var service = new TextExpansionCliService(storage, CreateProfileManager());

        var result = await service.AddAsync(
            ":MAIL",
            "new",
            PasteMethod.CtrlV,
            TextInsertionMode.Paste,
            DirectTypingMethod.FastBatch,
            profileIdentifier: null,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal((int)CliExitCode.InvalidArguments, result.ExitCode);
        await storage.DidNotReceive().SaveAsync(Arg.Any<IEnumerable<TextExpansion>>());
    }

    [Fact]
    public async Task TestAsync_WithExistingTrigger_ReturnsExpansionWithoutSaving()
    {
        var storage = Substitute.For<ITextExpansionStorageService>();
        storage.LoadAsync().Returns(new List<TextExpansion> { new(":mail", "me@example.com") });
        var service = new TextExpansionCliService(storage, CreateProfileManager());

        var result = await service.TestAsync(":mail", profileIdentifier: null, CancellationToken.None);

        Assert.True(result.Success);
        var data = Assert.IsType<TextExpansionTestData>(result.Data);
        Assert.True(data.Found);
        Assert.Equal("me@example.com", data.Expansion?.Replacement);
        await storage.DidNotReceive().SaveAsync(Arg.Any<IEnumerable<TextExpansion>>());
    }

    [Fact]
    public async Task ListAsync_WithProfile_ReloadsProfileThenRestoresActiveProfile()
    {
        var storage = Substitute.For<ITextExpansionStorageService>();
        storage.LoadAsync().Returns(new List<TextExpansion>());
        var profileManager = CreateProfileManager();
        var service = new TextExpansionCliService(storage, profileManager);

        var result = await service.ListAsync("Work", CancellationToken.None);

        Assert.True(result.Success);
        Received.InOrder(() =>
        {
            storage.ReloadAsync("/profiles/work");
            storage.LoadAsync();
            storage.ReloadAsync("/profiles/default");
        });
    }

    private static IProfileManager CreateProfileManager()
    {
        var active = new ProfileInfo { Id = "default", Name = "Default", CreatedAt = DateTime.UnixEpoch };
        var profiles = new List<ProfileInfo>
        {
            active,
            new() { Id = "work", Name = "Work", CreatedAt = DateTime.UnixEpoch.AddDays(1) },
        };

        var profileManager = Substitute.For<IProfileManager>();
        profileManager.ActiveProfile.Returns(active);
        profileManager.Profiles.Returns(profiles);
        profileManager.GetProfileDirectory("default").Returns("/profiles/default");
        profileManager.GetProfileDirectory("work").Returns("/profiles/work");
        return profileManager;
    }
}
