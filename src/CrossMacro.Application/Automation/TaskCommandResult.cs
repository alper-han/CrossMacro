namespace CrossMacro.Application.Automation;

public sealed record TaskCommandResult<TTask>(bool Success, string Message, IReadOnlyList<string> Errors,
    IReadOnlyList<TTask>? Tasks = null, TTask? Task = default, bool WasRun = false) where TTask : class;

public static class TaskCommandResult
{
    public static TaskCommandResult<TTask> Ok<TTask>(string message, TTask? task = null) where TTask : class => new(Success: true, message, [], Task: task);
    public static TaskCommandResult<TTask> Fail<TTask>(string message, IReadOnlyList<string>? errors = null) where TTask : class => new(Success: false, message, errors ?? []);
}
