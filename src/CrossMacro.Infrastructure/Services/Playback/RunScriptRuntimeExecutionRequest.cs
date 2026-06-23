using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CrossMacro.Core.Models;

namespace CrossMacro.Infrastructure.Services.Playback;

internal sealed class RunScriptRuntimeExecutionRequest
{
    public RunScriptRuntimeExecutionRequest(
        IReadOnlyList<string> scriptSteps,
        double speedMultiplier,
        Func<MacroEvent, CancellationToken, Task> executeEventAsync,
        Func<int, bool, int, int, int> resolveDelayMs)
    {
        ScriptSteps = scriptSteps ?? throw new ArgumentNullException(nameof(scriptSteps));
        SpeedMultiplier = speedMultiplier;
        ExecuteEventAsync = executeEventAsync ?? throw new ArgumentNullException(nameof(executeEventAsync));
        ResolveDelayMs = resolveDelayMs ?? throw new ArgumentNullException(nameof(resolveDelayMs));
    }

    public IReadOnlyList<string> ScriptSteps { get; }

    public double SpeedMultiplier { get; }

    public Func<MacroEvent, CancellationToken, Task> ExecuteEventAsync { get; }

    public Func<int, bool, int, int, int> ResolveDelayMs { get; }
}
