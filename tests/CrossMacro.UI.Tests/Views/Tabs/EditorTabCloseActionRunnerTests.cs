using CrossMacro.UI.Views.Tabs;

namespace CrossMacro.UI.Tests.Views.Tabs;

public sealed class EditorTabCloseActionRunnerTests
{
    [Fact]
    public async Task ExecuteAsync_WhenCloseActionFails_ReportsTheFailure()
    {
        var expected = new InvalidOperationException("close failed");
        Exception? reported = null;

        await EditorTabCloseActionRunner.ExecuteAsync(
            () => Task.FromException(expected),
            exception => reported = exception);

        Assert.Same(expected, reported);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCloseActionSucceeds_DoesNotReportAFailure()
    {
        var reportCount = 0;

        await EditorTabCloseActionRunner.ExecuteAsync(
            () => Task.CompletedTask,
            _ => reportCount++);

        Assert.Equal(0, reportCount);
    }
}
