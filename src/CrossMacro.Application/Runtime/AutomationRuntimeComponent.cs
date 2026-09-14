namespace CrossMacro.Application.Runtime;

public sealed record AutomationRuntimeComponent(
    string Name,
    Func<bool> IsRunning,
    Func<CancellationToken, Task> StartAsync,
    Func<CancellationToken, Task> StopAsync,
    Func<bool>? IsQuiescent = null,
    Func<bool>? IsEnabled = null);
