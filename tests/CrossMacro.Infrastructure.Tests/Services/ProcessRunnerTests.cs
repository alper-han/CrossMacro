namespace CrossMacro.Infrastructure.Tests.Services;

using System;
using CrossMacro.Infrastructure.Services;

public class ProcessRunnerTests
{
    [Fact]
    public async Task CheckCommandAsync_WhenCommandDoesNotExist_ReturnsFalse()
    {
        var runner = new ProcessRunner();
        var fakeCommand = $"crossmacro_nonexistent_{Guid.NewGuid():N}";

        var exists = await runner.CheckCommandAsync(fakeCommand);

        Assert.False(exists);
    }
}
