using System.Collections.Generic;
using System.Linq;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;

namespace CrossMacro.Cli.Services;

internal sealed class RunStepCompiler
{
    private readonly RunScriptCompiler _runScriptCompiler;

    public RunStepCompiler(IKeyCodeMapper keyCodeMapper)
    {
        _runScriptCompiler = new RunScriptCompiler(keyCodeMapper);
    }

    public RunStepCompileResult Compile(IReadOnlyList<RunStepEntry> steps)
    {
        var scriptSteps = steps
            .Select(step => new RunScriptStep(step.Step, step.FileLineNumber, step.SourceIndex))
            .ToList();
        var compileResult = _runScriptCompiler.Compile(scriptSteps);
        if (!compileResult.Success)
        {
            return RunStepCompileResult.Fail(compileResult.ErrorMessage);
        }

        return RunStepCompileResult.Ok(
            compileResult.Sequence!,
            compileResult.InitialDelayMs,
            compileResult.InitialHasRandomDelay,
            compileResult.InitialRandomDelayMinMs,
            compileResult.InitialRandomDelayMaxMs);
    }
}

internal sealed class RunStepCompileResult
{
    private RunStepCompileResult()
    {
    }

    public bool Success { get; private init; }
    public MacroSequence? Sequence { get; private init; }
    public string ErrorMessage { get; private init; } = string.Empty;
    public int InitialDelayMs { get; private init; }
    public bool InitialHasRandomDelay { get; private init; }
    public int InitialRandomDelayMinMs { get; private init; }
    public int InitialRandomDelayMaxMs { get; private init; }

    public static RunStepCompileResult Ok(
        MacroSequence sequence,
        int initialDelayMs,
        bool initialHasRandomDelay = false,
        int initialRandomDelayMinMs = 0,
        int initialRandomDelayMaxMs = 0)
    {
        return new RunStepCompileResult
        {
            Success = true,
            Sequence = sequence,
            InitialDelayMs = initialDelayMs,
            InitialHasRandomDelay = initialHasRandomDelay,
            InitialRandomDelayMinMs = initialRandomDelayMinMs,
            InitialRandomDelayMaxMs = initialRandomDelayMaxMs
        };
    }

    public static RunStepCompileResult Fail(string errorMessage)
    {
        return new RunStepCompileResult
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}
