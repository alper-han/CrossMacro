namespace CrossMacro.Infrastructure.Services.Scripting;

/// <summary>
/// Runs the node tree through the interpreter ahead of playback and collects the commands it produces,
/// with variable references already substituted.
/// </summary>
internal static class RunScriptStaticExpander
{
    public sealed record Result(bool Success, List<RunScriptStep> Steps, string ErrorMessage)
    {
        public static Result Expanded(List<RunScriptStep> steps) => new(
            Success: true,
            Steps: steps,
            ErrorMessage: string.Empty);

        public static Result Failed(List<RunScriptStep> steps, string error) => new(
            Success: false,
            Steps: steps,
            ErrorMessage: error);
    }

    public static Result Expand(IReadOnlyList<RunScriptNode> nodes)
    {
        var dialect = RunScriptDialect.Expansion;
        var variables = new RunScriptVariables();
        var interpreter = new RunScriptInterpreter(nodes, variables, dialect);
        var steps = new List<RunScriptStep>();
        while (true)
        {
            switch (interpreter.MoveNext(out var command))
            {
                case RunScriptInterpreter.Status.Command:
                    var source = command!.Source;
                    if (!variables.TryResolveText(source.Step, dialect, out var resolved, out var error))
                    {
                        return Result.Failed(steps, dialect.FormatFailure(source, error.Message));
                    }

                    steps.Add(source with { Step = resolved });
                    break;
                case RunScriptInterpreter.Status.Completed:
                    return Result.Expanded(steps);
                default:
                    return Result.Failed(steps, interpreter.FailureMessage);
            }
        }
    }
}
