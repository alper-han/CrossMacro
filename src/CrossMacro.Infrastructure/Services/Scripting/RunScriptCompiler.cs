
namespace CrossMacro.Infrastructure.Services.Scripting;

/// <summary>
/// Compiles run-script steps (set/repeat/if/while/for + event commands) into an executable
/// <see cref="MacroSequence"/>. The pipeline is: read the steps into a node tree
/// (<see cref="RunScriptTreeReader"/>); scripts that need services or live values stay runtime-backed and are
/// validated in place (<see cref="RunScriptRuntimeValidator"/>); every other script is expanded by the shared
/// interpreter (<see cref="RunScriptStaticExpander"/>) into plain commands that become input events
/// (<see cref="RunScriptEventCompiler"/>).
/// </summary>
public sealed class RunScriptCompiler
{
    private readonly RunScriptEventCompiler _eventCompiler;
    private readonly RunScriptRuntimeValidator _runtimeValidator;

    public RunScriptCompiler(IKeyCodeMapper keyCodeMapper)
    {
        _eventCompiler = new RunScriptEventCompiler(keyCodeMapper);
        _runtimeValidator = new RunScriptRuntimeValidator(_eventCompiler);
    }

    public RunScriptCompileResult Compile(IReadOnlyList<RunScriptStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);

        var parseResult = RunScriptTreeReader.ParseScriptNodes(steps);
        if (!parseResult.Success)
        {
            return RunScriptCompileResult.Fail(parseResult.ErrorMessage);
        }

        var nodes = parseResult.Nodes!;
        if (RunScriptRuntimeValidator.ContainsRuntimeServiceNode(nodes))
        {
            return CompileRuntimeBackedSteps(steps, nodes);
        }

        var expansion = RunScriptStaticExpander.Expand(nodes);
        if (!expansion.Success)
        {
            return RunScriptCompileResult.Fail(expansion.ErrorMessage);
        }

        var expandedSteps = expansion.Steps;
        var onlyDelays = expandedSteps.Count > 0
            && expandedSteps.TrueForAll(static step => step.Step.TrimStart().StartsWith("delay ", StringComparison.OrdinalIgnoreCase));
        var onlyRuntimeSteps = expandedSteps.Count is 0 && RunScriptRuntimeValidator.ContainsRuntimeBackedNode(nodes);
        return onlyDelays || onlyRuntimeSteps
            ? CompileRuntimeBackedSteps(steps, nodes)
            : _eventCompiler.Compile(expandedSteps);
    }

    private RunScriptCompileResult CompileRuntimeBackedSteps(IReadOnlyList<RunScriptStep> steps, IReadOnlyList<RunScriptNode> nodes)
    {
        var error = _runtimeValidator.Validate(nodes, loopDepth: 0);
        if (error is not null)
        {
            return RunScriptCompileResult.Fail(error);
        }

        var sequence = new MacroSequence
        {
            Name = "Run Script",
            IsAbsoluteCoordinates = false,
            SkipInitialZeroZero = true,
        };
        sequence.ReplaceScriptSteps(steps
            .Select(step => step.Step.Trim())
            .Where(step => !string.IsNullOrWhiteSpace(step))
            .ToList());

        return RunScriptCompileResult.Ok(sequence, initialDelayMicroseconds: 0);
    }
}
