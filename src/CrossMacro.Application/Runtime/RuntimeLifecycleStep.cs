
namespace CrossMacro.Application.Runtime;

public sealed record class RuntimeLifecycleStep(
    string Name,
    Func<CancellationToken, Task> StartAsync,
    Func<CancellationToken, Task> StopAsync);
