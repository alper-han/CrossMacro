using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Cli;
using CrossMacro.Cli.Commands;
using CrossMacro.Cli.Services;
using NSubstitute;
using Xunit;

namespace CrossMacro.Cli.Tests;

public class ShortcutListCommandHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_LoadsAndReturnsTaskList()
    {
        var shortcutCliService = Substitute.For<IShortcutCliService>();
        shortcutCliService.ListAsync(Arg.Any<CancellationToken>())
            .Returns(CliCommandExecutionResult.Ok("Loaded 1 shortcut task(s)."));

        var handler = new ShortcutListCommandHandler(shortcutCliService);
        var result = await handler.ExecuteAsync(new ShortcutListCliOptions(JsonOutput: true), CancellationToken.None);

        Assert.True(result.Success);
        await shortcutCliService.Received(1).ListAsync(Arg.Any<CancellationToken>());
    }
}
