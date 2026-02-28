using System.Collections.Generic;
using CrossMacro.Core.Models;
using CrossMacro.Core.Services;

namespace CrossMacro.Cli.Services;

internal sealed class RunStepCompiler
{
    private readonly RunEventSequenceCompiler _runEventSequenceCompiler;

    public RunStepCompiler(IKeyCodeMapper keyCodeMapper)
    {
        _runEventSequenceCompiler = new RunEventSequenceCompiler(keyCodeMapper);
    }

    public RunStepCompileResult Compile(IReadOnlyList<RunStepEntry> steps)
    {
        var scriptExpansion = RunScriptExpander.Expand(steps);
        if (!scriptExpansion.Success)
        {
            return RunStepCompileResult.Fail(scriptExpansion.ErrorMessage);
        }

        return _runEventSequenceCompiler.Compile(scriptExpansion.Steps);
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

    public static RunStepCompileResult Ok(MacroSequence sequence, int initialDelayMs)
    {
        return new RunStepCompileResult
        {
            Success = true,
            Sequence = sequence,
            InitialDelayMs = initialDelayMs
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
