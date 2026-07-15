
namespace CrossMacro.Infrastructure.Tests.Services;

public sealed class FlatpakDisabledShellCommandRunnerTests
{
    [Fact]
    public async Task RunAsync_AlwaysFailsClosed()
    {
        var runner = new FlatpakDisabledShellCommandRunner();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.RunAsync(new ShellCommandRequest("printf ok"), timeout: null));

        Assert.Contains("disabled in Flatpak", exception.Message, StringComparison.Ordinal);
    }
}
