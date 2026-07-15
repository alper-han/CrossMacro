using System;
using System.Threading;
using System.Threading.Tasks;

namespace CrossMacro.Application.Runtime;

public sealed record RuntimeLifecycleStep(
    string Name,
    Func<CancellationToken, Task> StartAsync,
    Func<CancellationToken, Task> StopAsync);
