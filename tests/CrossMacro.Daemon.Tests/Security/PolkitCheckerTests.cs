using System.Globalization;
using CrossMacro.Daemon.Contracts.Security;

namespace CrossMacro.Daemon.Tests.Security;

public sealed class PolkitCheckerTests : IDisposable
{
    private const uint TestUid = 1000;
    private static readonly TimeSpan GenerousTimeout = TimeSpan.FromSeconds(30);
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"crossmacro-pkcheck-{Guid.NewGuid():N}");

    public PolkitCheckerTests()
    {
        _ = Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public async Task IsAuthorizedAsync_WhenPkcheckGrants_ChecksTheDaemonOwnedActionForTheExactProcess()
    {
        var pkcheck = CreatePkcheck("exit 0");

        var authorized = await pkcheck.Checker.IsAuthorizedAsync(TestUid, Environment.ProcessId, CancellationToken.None);

        Assert.True(authorized);
        var invocation = Assert.Single(pkcheck.ReadInvocations());
        var processId = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
        Assert.StartsWith($"--action-id {PolkitActions.InputCapture} --process {processId},", invocation, StringComparison.Ordinal);
        Assert.EndsWith($",{TestUid} --allow-user-interaction", invocation, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(126)]
    [InlineData(3)]
    [InlineData(127)]
    public async Task IsAuthorizedAsync_WhenPkcheckDoesNotAuthorize_Denies(int exitCode)
    {
        var pkcheck = CreatePkcheck("exit " + exitCode.ToString(CultureInfo.InvariantCulture));

        var authorized = await pkcheck.Checker.IsAuthorizedAsync(TestUid, Environment.ProcessId, CancellationToken.None);

        Assert.False(authorized);
        _ = Assert.Single(pkcheck.ReadInvocations());
    }

    [Fact]
    public async Task IsAuthorizedAsync_WhenProcessStartTimeIsUnknown_DeniesWithoutRunningPkcheck()
    {
        var pkcheck = CreatePkcheck("exit 0");

        var authorized = await pkcheck.Checker.IsAuthorizedAsync(TestUid, pid: int.MaxValue, CancellationToken.None);

        Assert.False(authorized);
        Assert.Empty(pkcheck.ReadInvocations());
    }

    [Fact]
    public async Task IsAuthorizedAsync_WhenPkcheckIsMissing_DeniesAndStaysUsableForLaterChecks()
    {
        var missing = new PolkitChecker(Path.Combine(_directory, "missing-pkcheck"), GenerousTimeout, TimeProvider.System);
        var present = CreatePkcheck("exit 0");

        var denied = await missing.IsAuthorizedAsync(TestUid, Environment.ProcessId, CancellationToken.None);
        var authorized = await present.Checker.IsAuthorizedAsync(TestUid, Environment.ProcessId, CancellationToken.None);

        Assert.False(denied);
        Assert.True(authorized);
    }

    [Fact]
    public async Task IsAuthorizedAsync_WhenPkcheckHangs_DeniesAfterTimeout()
    {
        var pkcheck = CreatePkcheck("exec sleep 60", TimeSpan.FromMilliseconds(300));

        var authorized = await pkcheck.Checker.IsAuthorizedAsync(TestUid, Environment.ProcessId, CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(20), TimeProvider.System, CancellationToken.None);

        Assert.False(authorized);
    }

    [Fact]
    public async Task IsAuthorizedAsync_WhenCallerCancels_PropagatesCancellation()
    {
        var pkcheck = CreatePkcheck("exec sleep 60");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => pkcheck.Checker.IsAuthorizedAsync(TestUid, Environment.ProcessId, cancellation.Token));
    }

    [Fact]
    public async Task IsAuthorizedAsync_WhenPkcheckReportsTransientSubjectError_RetriesTheSameAction()
    {
        var pkcheck = CreatePkcheck(
            "if [ ! -e \"$0.seen\" ]; then : > \"$0.seen\"; echo 'No such process' >&2; exit 1; fi\nexit 0");

        var authorized = await pkcheck.Checker.IsAuthorizedAsync(TestUid, Environment.ProcessId, CancellationToken.None);

        Assert.True(authorized);
        Assert.Equal(2, pkcheck.ReadInvocations().Length);
    }

    [Fact]
    public async Task IsAuthorizedAsync_WhenPkcheckWritesALotToStandardOutput_StillCompletes()
    {
        // More than a pipe buffer: an unread redirected stdout would block pkcheck forever.
        var pkcheck = CreatePkcheck("i=0; while [ $i -lt 4000 ]; do echo 'polkit\\56result=yes padding padding padding'; i=$((i+1)); done\nexit 0");

        var authorized = await pkcheck.Checker.IsAuthorizedAsync(TestUid, Environment.ProcessId, CancellationToken.None);

        Assert.True(authorized);
    }

    private PkcheckFixture CreatePkcheck(string scriptBody, TimeSpan? timeout = null)
    {
        var path = Path.Combine(_directory, $"pkcheck-{Guid.NewGuid():N}");
        File.WriteAllText(path, $"#!/bin/sh\necho \"$@\" >> \"$0.log\"\n{scriptBody}\n");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return new PkcheckFixture(path, new PolkitChecker(path, timeout ?? GenerousTimeout, TimeProvider.System));
    }

    private sealed record PkcheckFixture(string Path, PolkitChecker Checker)
    {
        public string[] ReadInvocations()
        {
            var log = Path + ".log";
            return File.Exists(log) ? File.ReadAllLines(log) : [];
        }
    }
}
