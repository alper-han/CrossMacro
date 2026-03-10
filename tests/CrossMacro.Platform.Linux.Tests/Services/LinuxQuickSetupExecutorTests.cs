using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Platform.Linux.Services.QuickSetup;
using Xunit;

namespace CrossMacro.Platform.Linux.Tests.Services;

public sealed class LinuxQuickSetupExecutorTests
{
    [Fact]
    public async Task RunAsync_WhenLauncherUnavailable_ShouldReturnLauncherMessage()
    {
        var executor = new LinuxQuickSetupExecutor(
            new LinuxQuickSetupIdentityResolver(() => "alice", () => 1000),
            new LinuxQuickSetupScriptBuilder(),
            (_, _) => Task.FromResult((0, string.Empty, string.Empty)));

        var result = await executor.RunAsync(
            new FakeLauncher(isAvailable: false, failureMessage: "pkexec is missing"),
            LinuxQuickSetupScriptOptions.Lenient,
            "TestQuickSetup",
            "unexpected");

        Assert.False(result.Success);
        Assert.Contains("pkexec is missing", result.Message);
    }

    [Fact]
    public async Task RunAsync_WhenLauncherSucceeds_ShouldReturnSuccess()
    {
        ProcessStartInfo? capturedStartInfo = null;
        var executor = new LinuxQuickSetupExecutor(
            new LinuxQuickSetupIdentityResolver(() => "alice", () => 1000),
            new LinuxQuickSetupScriptBuilder(),
            (startInfo, _) =>
            {
                capturedStartInfo = startInfo;
                return Task.FromResult((0, string.Empty, string.Empty));
            });

        var result = await executor.RunAsync(
            new FakeLauncher(),
            LinuxQuickSetupScriptOptions.Strict,
            "TestQuickSetup",
            "unexpected");

        Assert.True(result.Success);
        Assert.NotNull(capturedStartInfo);
        Assert.Equal("fake-launcher", capturedStartInfo!.FileName);
        Assert.Equal("1000", capturedStartInfo.ArgumentList[^1]);
        Assert.Contains("uinput_ok=0", capturedStartInfo.ArgumentList[2]);
        Assert.Contains("event_ok=0", capturedStartInfo.ArgumentList[2]);
    }

    private sealed class FakeLauncher : IPrivilegedHostCommandLauncher
    {
        private readonly bool _isAvailable;
        private readonly string _failureMessage;

        public FakeLauncher(bool isAvailable = true, string failureMessage = "")
        {
            _isAvailable = isAvailable;
            _failureMessage = failureMessage;
        }

        public bool IsAvailable(out string failureMessage)
        {
            failureMessage = _failureMessage;
            return _isAvailable;
        }

        public ProcessStartInfo CreateStartInfo(string hostScript, LinuxQuickSetupIdentity identity)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "fake-launcher"
            };

            startInfo.ArgumentList.Add("sh");
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(hostScript);
            startInfo.ArgumentList.Add(identity.Specifier);
            return startInfo;
        }
    }
}
