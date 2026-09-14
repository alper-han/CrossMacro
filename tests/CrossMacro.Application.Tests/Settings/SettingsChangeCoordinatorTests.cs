using CrossMacro.Application.Settings;
namespace CrossMacro.Application.Tests.Settings;

public sealed class SettingsChangeCoordinatorTests
{
    [Fact]
    public async Task FailedCoalescedSave_RestoresAllFieldsToValuesBeforeBurst()
    {
        var current = new AppSettings { Theme = "Mocha", EnableTrayIcon = false };
        var save = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = Substitute.For<ISettingsService>();
        _ = service.Current.Returns(current);
        _ = service.SaveAfterIdleAsync().Returns(save.Task);
        var coordinator = new SettingsChangeCoordinator(service);
        var firstBefore = AppSettingsSnapshot.Copy(current);
        var firstAfter = AppSettingsSnapshot.Copy(current);
        firstAfter.Theme = "Nord";
        var first = coordinator.CommitAsync(new(firstBefore, firstAfter), SettingsSaveMode.AfterIdle, cancellationToken: CancellationToken.None);
        var secondBefore = AppSettingsSnapshot.Copy(current);
        var secondAfter = AppSettingsSnapshot.Copy(current);
        secondAfter.Theme = "Classic";
        secondAfter.EnableTrayIcon = true;
        var second = coordinator.CommitAsync(new(secondBefore, secondAfter), SettingsSaveMode.AfterIdle, cancellationToken: CancellationToken.None);
        save.SetException(new IOException("disk full"));
        _ = await Assert.ThrowsAsync<IOException>(() => first);
        _ = await Assert.ThrowsAsync<IOException>(() => second);
        Assert.Equal("Mocha", current.Theme, StringComparer.Ordinal);
        Assert.False(current.EnableTrayIcon);
    }

    [Fact]
    public async Task FailedRuntimeEffect_AfterSuccessfulSave_PreservesPersistedSettingsAndReportsError()
    {
        var current = new AppSettings { EnableTextExpansion = false };
        var service = Substitute.For<ISettingsService>();
        _ = service.Current.Returns(current);
        AppSettings? persisted = null;
        _ = service.SaveAsync().Returns(_ =>
        {
            persisted = AppSettingsSnapshot.Copy(current);
            return Task.CompletedTask;
        });
        var coordinator = new SettingsChangeCoordinator(service);
        var after = AppSettingsSnapshot.Copy(current);
        after.EnableTextExpansion = true;
        var effectError = new InvalidOperationException("runtime unavailable");

        var observed = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.CommitAsync(new(current, after), SettingsSaveMode.Immediate,
                () => Task.FromException(effectError), cancellationToken: CancellationToken.None));

        Assert.Same(effectError, observed);
        Assert.True(current.EnableTextExpansion);
        Assert.NotNull(persisted);
        Assert.True(persisted.EnableTextExpansion);
        await service.Received(1).SaveAsync();
    }

    [Fact]
    public async Task FailedOlderSave_DoesNotOverwriteNewerCommittedField()
    {
        var current = new AppSettings { Theme = "Mocha" };
        var save = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = Substitute.For<ISettingsService>();
        _ = service.Current.Returns(current);
        _ = service.SaveAsync().Returns(save.Task, Task.CompletedTask);
        var coordinator = new SettingsChangeCoordinator(service);
        var firstAfter = AppSettingsSnapshot.Copy(current);
        firstAfter.Theme = "Nord";
        var first = coordinator.CommitAsync(new(AppSettingsSnapshot.Copy(current), firstAfter), cancellationToken: CancellationToken.None);
        var secondAfter = AppSettingsSnapshot.Copy(current);
        secondAfter.Theme = "Classic";
        await coordinator.CommitAsync(new(AppSettingsSnapshot.Copy(current), secondAfter), cancellationToken: CancellationToken.None);
        save.SetException(new IOException("late failure"));
        _ = await Assert.ThrowsAsync<IOException>(() => first);
        Assert.Equal("Classic", current.Theme, StringComparer.Ordinal);
    }

    [Fact]
    public async Task ProfileChange_InvalidatesRollbackEvenWhenValuesMatch()
    {
        var current = new AppSettings { EnableTextExpansion = false };
        var save = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = Substitute.For<ISettingsService>();
        _ = service.Current.Returns(current);
        _ = service.SaveAsync().Returns(save.Task);
        var coordinator = new SettingsChangeCoordinator(service);
        var after = AppSettingsSnapshot.Copy(current);
        after.EnableTextExpansion = true;
        var operation = coordinator.CommitAsync(new(AppSettingsSnapshot.Copy(current), after), cancellationToken: CancellationToken.None);
        coordinator.InvalidatePendingChanges();
        save.SetException(new IOException("old profile"));
        _ = await Assert.ThrowsAsync<IOException>(() => operation);
        Assert.True(current.EnableTextExpansion);
    }

    [Fact]
    public async Task CanceledRequest_DoesNotMutateOrSave()
    {
        var service = Substitute.For<ISettingsService>();
        var current = new AppSettings();
        _ = service.Current.Returns(current);
        var after = AppSettingsSnapshot.Copy(current);
        after.Theme = "Nord";
        var coordinator = new SettingsChangeCoordinator(service);
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => coordinator.CommitAsync(new(current, after), new CancellationToken(canceled: true)));
        Assert.NotEqual("Nord", current.Theme, StringComparer.Ordinal);
        await service.DidNotReceive().SaveAsync();
    }

    [Fact]
    public void Snapshot_DoesNotShareSecurityObjects()
    {
        var source = new AppSettings();
        var copied = AppSettingsSnapshot.Copy(source);
        copied.McpSecurity.AllowShellExecute = false;
        copied.McpSecurity.Paths = copied.McpSecurity.Paths.WithRoots(McpPathSetting.FileRead, ["/tmp/allowed"]);
        Assert.True(source.McpSecurity.AllowShellExecute);
        Assert.Empty(source.McpSecurity.Paths.FileReadRoots);
    }
    [Fact]
    public async Task LateSuccessfulSave_AfterProfileSwitch_DoesNotApplyRuntimeEffect()
    {
        var service = Substitute.For<ISettingsService>();
        var current = new AppSettings { EnableTextExpansion = false };
        _ = service.Current.Returns(current);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = service.SaveAsync().Returns(completion.Task);
        var coordinator = new SettingsChangeCoordinator(service);
        var after = AppSettingsSnapshot.Copy(current);
        after.EnableTextExpansion = true;
        var effects = 0;
        var operation = coordinator.CommitAsync(new(current, after), SettingsSaveMode.Immediate,
            () => { effects++; return Task.CompletedTask; }, cancellationToken: CancellationToken.None);
        coordinator.InvalidatePendingChanges();
        completion.SetResult();
        await operation;
        Assert.Equal(0, effects);
    }

    [Fact]
    public async Task OlderSuccessfulSave_DoesNotApplyEffectForNewerUnpersistedValue()
    {
        var service = Substitute.For<ISettingsService>();
        var current = new AppSettings { EnableTextExpansion = false };
        _ = service.Current.Returns(current);
        var firstSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = service.SaveAsync().Returns(firstSave.Task, secondSave.Task);
        var coordinator = new SettingsChangeCoordinator(service);
        var firstAfter = AppSettingsSnapshot.Copy(current);
        firstAfter.EnableTextExpansion = true;
        var effects = new List<bool>();
        var first = coordinator.CommitAsync(new(current, firstAfter), SettingsSaveMode.Immediate,
            () => { effects.Add(true); return Task.CompletedTask; }, cancellationToken: CancellationToken.None);
        var secondAfter = AppSettingsSnapshot.Copy(current);
        secondAfter.EnableTextExpansion = false;
        var second = coordinator.CommitAsync(new(current, secondAfter), SettingsSaveMode.Immediate,
            () => { effects.Add(false); return Task.CompletedTask; }, cancellationToken: CancellationToken.None);
        firstSave.SetResult();
        await first;
        Assert.Empty(effects);
        secondSave.SetResult();
        await second;
        Assert.Equal([false], effects);
    }

}
