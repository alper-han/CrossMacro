namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class LinuxQuickSetupIdentityTests
{
    [Theory]
    [InlineData(0u, "0 1000 1", 1000u)]
    [InlineData(4242u, "4242 1000 1", 1000u)]
    [InlineData(1000u, "1000 1000 1", 4242u)]
    [InlineData(1000u, "1000 1000 1", null)]
    [InlineData(1000u, "1000 1000 1\n1000 4242 1", 1000u)]
    [InlineData(1000u, "not a mapping", 1000u)]
    [InlineData(1000u, null, 1000u)]
    public async Task DirectSetup_WhenHostIdentityIsRemappedOrUnproven_RefusesBeforeAuthorization(uint uid, string? map, uint? hostUid)
    {
        var resolver = new LinuxQuickSetupIdentityResolver(() => uid, () => map, _ => ValueTask.FromResult(hostUid));
        var attempts = 0;
        var executor = new LinuxQuickSetupExecutor(resolver, (_, _) => { attempts++; return Task.FromResult((0, string.Empty, string.Empty)); });
        var result = await executor.RunAsync(QuickSetupReadinessTests.CreateLauncher(), LinuxQuickSetupScriptOptions.Strict, "Test", "unexpected", TestContext.Current.CancellationToken);
        Assert.Equal(QuickSetupOutcome.Failed, result.Outcome);
        Assert.Equal(0, attempts);
    }

    [Theory]
    [InlineData("0 0 4294967295", null)]
    [InlineData("1000 1000 1", 1000u)]
    public async Task DirectSetup_WhenNativeOrHostAttestedUidPreservingMapping_UsesNumericIdentity(string map, uint? hostUid)
    {
        var resolver = new LinuxQuickSetupIdentityResolver(() => 1000, () => map, _ => ValueTask.FromResult(hostUid));
        var identity = await resolver.ResolveAsync(TestContext.Current.CancellationToken);
        Assert.Equal("1000", identity?.Specifier);
    }
}
