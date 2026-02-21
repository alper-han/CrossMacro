namespace CrossMacro.Core.Tests.Services;

using CrossMacro.Core.Models;
using CrossMacro.Core.Services;

public class TaskExecutedEventArgsTests
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
}
