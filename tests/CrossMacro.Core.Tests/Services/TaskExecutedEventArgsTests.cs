namespace CrossMacro.Core.Tests.Services;


public sealed class TaskExecutedEventArgsTests
{
    [Fact]
    public void Constructor_ShouldAssignAllProperties()
    {
        var task = new ScheduledTask { Name = "nightly" };

        var args = new TaskExecutedEventArgs(task, success: true, message: "ok");

        Assert.Same(task, args.Task);
        Assert.True(args.Success);
        Assert.Equal("ok", args.Message);
    }

    [Fact]
    public void Constructor_WhenExecutionFailsWithoutMessage_LeavesOptionalMessageNull()
    {
        var task = new ScheduledTask { Name = "nightly" };

        var args = new TaskExecutedEventArgs(task, success: false);

        Assert.Same(task, args.Task);
        Assert.False(args.Success);
        Assert.Null(args.Message);
    }
}
